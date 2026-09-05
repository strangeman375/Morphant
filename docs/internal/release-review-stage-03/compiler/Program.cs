using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Reflection;

var outputRoot = Path.GetFullPath(args[0]);
Directory.CreateDirectory(outputRoot);
var parse = new CSharpParseOptions(LanguageVersion.CSharp9, DocumentationMode.Diagnose);
var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
    .Where(p => Path.GetFileName(p) != "Morphant.dll")
    .Select(p => MetadataReference.CreateFromFile(p)).Cast<MetadataReference>()
    .Append(MetadataReference.CreateFromFile(typeof(Morphant.TypeMapper<>).Assembly.Location)).ToArray();
var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
    nullableContextOptions: NullableContextOptions.Enable);
var mode = args.Length > 1 ? args[1] : "shared";
var calls = new Dictionary<string, string>
{
    ["Bare"] = "",
    ["Construct"] = ".Construct(s => new(s.Id))",
    ["ConstructContext"] = ".Construct((s, c) => new(s.Id))",
    ["Resolve"] = ".Resolve((s, p) => new(s.Id))",
    ["ResolveContext"] = ".Resolve((s, p, c) => new(s.Id))",
    ["ConstructUsing"] = ".ConstructUsing(s => new Shared.Destination(s.Id))",
    ["ConstructUsingContext"] = ".ConstructUsing((s, c) => new Shared.Destination(s.Id))",
    ["ResolveUsing"] = ".ResolveUsing((s, p) => new Shared.Destination(s.Id))",
    ["ResolveUsingContext"] = ".ResolveUsing((s, p, c) => new Shared.Destination(s.Id))",
    ["Convert"] = ".Convert(s => new Shared.Destination(s!.Id))",
    ["ConvertPrevious"] = ".Convert((s, p) => new Shared.Destination(s!.Id))",
    ["ConvertContext"] = ".Convert((s, p, c) => new Shared.Destination(s!.Id))",
    ["Members"] = ".Members(s => new() { Id = s.Id })",
    ["MembersPrevious"] = ".Members((s, p) => new() { Id = s.Id })",
    ["MembersResult"] = ".Members((s, p, r) => new() { Id = s.Id })",
    ["MembersContext"] = ".Members((s, p, r, c) => new() { Id = s.Id })",
    ["ExplicitConstruct"] = ".Construct(s => new Morphant.Generated.Types.A_AuditConsumer.N_Shared.Plans.DestinationConstruction(s.Id))",
    ["ExplicitMembers"] = ".Members(s => new Morphant.Generated.Types.A_AuditConsumer.N_Shared.Plans.DestinationMembers() { Id = s.Id })"
};
var analyzer = new AnalyzerFileReference(Path.GetFullPath("src/Morphant.Generator/bin/Release/netstandard2.0/Morphant.Generator.dll"), new Loader());
var generators = analyzer.GetGenerators(LanguageNames.CSharp);
if (generators.IsEmpty) throw new InvalidOperationException("Generator was not loaded");
var summaries = new List<object>();
if (mode == "custom")
{
    var result = Generate("AuditCustom", File.ReadAllText(args[2]), Array.Empty<MetadataReference>());
    var ds = result.Run.Diagnostics.Concat(result.Output.GetDiagnostics()).Where(d => d.Severity >= DiagnosticSeverity.Warning).ToArray();
    foreach (var f in result.Run.GeneratedSources) File.WriteAllText(Path.Combine(outputRoot, f.HintName), f.SourceText.ToString());
    File.WriteAllLines(Path.Combine(outputRoot, "diagnostics.txt"), ds.Select(d => d.ToString()));
    File.WriteAllText(Path.Combine(outputRoot, "summary.json"), JsonSerializer.Serialize(new { generatorException = result.Run.Exception?.ToString(), diagnostics = ds.GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.Count()), files = result.Run.GeneratedSources.Length }));
    Console.WriteLine(string.Join(",", ds.Select(d => d.Id).Distinct()));
    return;
}
foreach (var friend in new[] { false, true })
{
    if (mode.StartsWith("same-") && friend) continue;
    string producerSource = "#nullable enable\n#pragma warning disable CS1591\nusing Morphant;\n" +
        (friend ? "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"AuditConsumer\")]\n" : "") + """
namespace Shared
{
    public sealed class Source { public int Id { get; set; } }
    public sealed class Destination
    {
        public Destination(int id) => Id = id;
        public int Id { get; set; }
    }
}
namespace Producer
{
    [MorphantMapper]
    public partial class ProducerMapper : TypeMapper<ProducerMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Shared.Source, Shared.Destination>();
    }
}
""";
    producerSource = Adapt(producerSource, true);
    var producer = Generate("AuditProducer", producerSource, Array.Empty<MetadataReference>());
    if (producer.Output.GetDiagnostics().Any(d => d.Severity >= DiagnosticSeverity.Warning))
        throw new InvalidOperationException(string.Join("\n", producer.Output.GetDiagnostics()));
    using var dll = new MemoryStream();
    using var reference = new MemoryStream();
    var implementationEmit = producer.Output.Emit(dll);
    var referenceEmit = producer.Output.Emit(reference, options: new EmitOptions(metadataOnly: true, includePrivateMembers: false));
    if (!implementationEmit.Success || !referenceEmit.Success) throw new InvalidOperationException("Producer emit failed");
    var references = new Dictionary<string, MetadataReference>
    {
        ["source"] = producer.Output.ToMetadataReference(),
        ["dll"] = MetadataReference.CreateFromImage(dll.ToArray()),
        ["ref"] = MetadataReference.CreateFromImage(reference.ToArray())
    };
    foreach (var shape in references)
    foreach (var call in calls)
    {
        if (mode != "shared" && call.Key.StartsWith("Explicit")) continue;
        if (mode.StartsWith("same-") && shape.Key != "source") continue;
        var name = $"{mode}-{(friend ? "friend" : "isolated")}-{shape.Key}-{call.Key}";
        string consumerSource = "#nullable enable\n#pragma warning disable CS1591\nusing Morphant;\n" + $$"""
namespace Consumer
{
    [MorphantMapper]
    public partial class ConsumerMapper : TypeMapper<ConsumerMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Shared.Source, Shared.Destination>(){{call.Value}};
    }
}
""";
        consumerSource = Adapt(consumerSource, false);
        var consumer = mode.StartsWith("same-")
            ? Generate("AuditConsumer", producerSource + "\n" + consumerSource[consumerSource.IndexOf("namespace Consumer", StringComparison.Ordinal)..], Array.Empty<MetadataReference>())
            : Generate("AuditConsumer", consumerSource, new[] { shape.Value });
        var diagnostics = consumer.Run.Diagnostics.Concat(consumer.Output.GetDiagnostics())
            .Where(d => d.Severity >= DiagnosticSeverity.Warning).ToArray();
        var dir = Path.Combine(outputRoot, name); Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Producer.cs"), producerSource);
        File.WriteAllText(Path.Combine(dir, "Consumer.cs"), consumerSource);
        File.WriteAllLines(Path.Combine(dir, "diagnostics.txt"), diagnostics.Select(d => d.ToString()));
        foreach (var file in consumer.Run.GeneratedSources)
            File.WriteAllText(Path.Combine(dir, file.HintName), file.SourceText.ToString());
        summaries.Add(new { name, generatorException = consumer.Run.Exception?.ToString(),
            diagnostics = diagnostics.GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.Count()),
            files = consumer.Run.GeneratedSources.Length,
            planFiles = consumer.Run.GeneratedSources.Count(file =>
                file.HintName.Contains(".Construction.", StringComparison.Ordinal) ||
                file.HintName.Contains(".Member.", StringComparison.Ordinal)),
            extensionMethods = consumer.Run.GeneratedSources
                .Where(file => file.HintName.Contains(".MappingExtension.", StringComparison.Ordinal) ||
                    file.HintName.Contains(".MemberExtension.", StringComparison.Ordinal))
                .Sum(file => CSharpSyntaxTree.ParseText(file.SourceText, parse).GetRoot()
                    .DescendantNodes().OfType<MethodDeclarationSyntax>().Count()) });
        Console.WriteLine(name + " " + string.Join(",", diagnostics.Select(d => d.Id).Distinct()));
    }
}
File.WriteAllText(Path.Combine(outputRoot, "summary.json"), JsonSerializer.Serialize(summaries, new JsonSerializerOptions { WriteIndented = true }));

string Adapt(string source, bool producer)
{
    if (mode == "nullable")
        return source.Replace("Map<Shared.Source, Shared.Destination>()", "Map<Shared.Source?, Shared.Destination?>()");
    if (mode == "tuple")
        return source.Replace("public sealed class Source { public int Id { get; set; } }", "public sealed class Source { public int Id { get; set; } public int Other { get; set; } }")
            .Replace("Map<Shared.Source, Shared.Destination>()", "Map<Shared.Source, (int Id, int Other)>()")
            .Replace("new(s.Id)", "new(s.Id, s.Id + 1)")
            .Replace("new Shared.Destination(s.Id)", "(s.Id, s.Id + 1)")
            .Replace("new Shared.Destination(s!.Id)", "(s!.Id, s.Id + 1)");
    if (mode.EndsWith("family", StringComparison.Ordinal))
    {
        source = source.Replace("class Source {", "class Source<T> {")
            .Replace("class Destination\n", "class Destination<T>\n")
            .Replace("Shared.Source, Shared.Destination", "Shared.Source<T>, Shared.Destination<T>")
            .Replace("new Shared.Destination(", "new Shared.Destination<T>(");
        var name = producer ? "ProducerMapper" : "ConsumerMapper";
        source = source.Replace($"class {name} : TypeMapper<{name}>",
            $"class {name}<TMapper, T> : TypeMapper<TMapper>\n        where TMapper : {name}<TMapper, T>\n        where T : class" + (producer ? "" : ", new()"));
        if (!producer && mode == "same-related-family")
            source = source.Replace(": TypeMapper<TMapper>", ": Producer.ProducerMapper<TMapper, T>");
        if (!producer && mode == "same-nested-family")
            source = source.Replace("Shared.Source<T>", "Shared.Source<System.Collections.Generic.List<T>>")
                .Replace("Shared.Destination<T>", "Shared.Destination<System.Collections.Generic.List<T>>");
        return source;
    }
    if (mode == "distinct-source" && !producer)
        return source.Replace("namespace Consumer\n{", "namespace Consumer\n{\n    public sealed class LocalSource { public int Id { get; set; } }")
            .Replace("Map<Shared.Source,", "Map<LocalSource,");
    return source;
}

(CSharpCompilation Output, GeneratorRunResult Run) Generate(string assembly, string source, IEnumerable<MetadataReference> extra)
{
    var compilation = CSharpCompilation.Create(assembly, new[] { CSharpSyntaxTree.ParseText(source, parse, "Input.cs") }, refs.Concat(extra), options);
    GeneratorDriver driver = CSharpGeneratorDriver.Create(generators, parseOptions: parse);
    driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
    return ((CSharpCompilation)output, driver.GetRunResult().Results.Single());
}

sealed class Loader : IAnalyzerAssemblyLoader
{
    public void AddDependencyLocation(string fullPath) { }
    public Assembly LoadFromPath(string fullPath) => Assembly.LoadFrom(fullPath);
}
