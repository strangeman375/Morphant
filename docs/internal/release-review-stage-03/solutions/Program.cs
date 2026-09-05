using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using System.Text.Json;

// Compiler experiment only. Audit builders model the proposed receiver;
// callbacks use real Morphant delegates. No generator or runtime mapping runs.
var outputRoot = Path.GetFullPath(args[0]);
Directory.CreateDirectory(outputRoot);
var parse = new CSharpParseOptions(LanguageVersion.CSharp9, DocumentationMode.Diagnose);
var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
    .Where(p => Path.GetFileName(p) != "Morphant.dll")
    .Select(p => MetadataReference.CreateFromFile(p)).Cast<MetadataReference>()
    .Append(MetadataReference.CreateFromFile(typeof(Morphant.TypeMapper<>).Assembly.Location)).ToArray();
var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
    nullableContextOptions: NullableContextOptions.Enable);
const string header = "#nullable enable\n#pragma warning disable CS1591\nusing Audit;\n";
const string runtimeSource = """
namespace Audit
{
    public interface IMappingBuilder<out TOwner, TBuilder> { }
    public abstract class MapperBuilderBase<TBuilder>
    {
        public TBuilder Setting() => throw new System.NotSupportedException();
    }
    public abstract class TypeMapper<TMapper> where TMapper : TypeMapper<TMapper>
    {
        protected sealed class MapperBuilder
        {
            public MappingBuilder<TMapper, S, D> Map<S, D>() => throw new System.NotSupportedException();
        }
        protected abstract void Configure(MapperBuilder builder);
    }
    public sealed class MappingBuilder<TMapper, S, D> :
        MapperBuilderBase<MappingBuilder<TMapper, S, D>>,
        IMappingBuilder<TMapper, MappingBuilder<TMapper, S, D>>
        where TMapper : TypeMapper<TMapper> { }
}
""";
var runtime = Compile("AuditRuntime", header + runtimeSource);
EnsureClean(runtime);
var runtimeRef = Emit(runtime, false);
var results = new List<Observation>();
var overloads = new (string Name, string Delegate, string Lambda)[]
{
    ("Construct", "Construct<$S, $C>", "s => new(s.Id)"),
    ("Construct", "Construct<$S, $CTX, $C>", "(s, c) => new(s.Id)"),
    ("Resolve", "Resolve<$S, $P, $C>", "(s, p) => new(s.Id)"),
    ("Resolve", "Resolve<$S, $P, $CTX, $C>", "(s, p, c) => new(s.Id)"),
    ("ConstructUsing", "ConstructUsing<$S, $D>", "s => $NEW"),
    ("ConstructUsing", "ConstructUsing<$S, $CTX, $D>", "(s, c) => $NEW"),
    ("ResolveUsing", "ResolveUsing<$S, $P, $D>", "(s, p) => $NEW"),
    ("ResolveUsing", "ResolveUsing<$S, $P, $CTX, $D>", "(s, p, c) => $NEW"),
    ("Convert", "Convert<$MS, $D>", "s => $MANUAL"),
    ("Convert", "Convert<$MS, $P, $D>", "(s, p) => $MANUAL"),
    ("Convert", "Convert<$MS, $P, $CTX, $D>", "(s, p, c) => $MANUAL"),
    ("Members", "Members<$S, $M>", "s => new() { Id = s.Id }"),
    ("Members", "Members<$S, $P, $M>", "(s, p) => new() { Id = s.Id }"),
    ("Members", "Members<$S, $P, $D, $M>", "(s, p, r) => new() { Id = s.Id }"),
    ("Members", "Members<$S, $P, $D, $CTX, $M>", "(s, p, r, c) => new() { Id = s.Id }")
};
const string models = """
namespace Shared
{
    public sealed class Source { public int Id { get; set; } }
    public sealed class Destination { public Destination(int id) { } public int Id { get; set; } }
    public sealed class Source<T> { public int Id { get; set; } }
    public sealed class Destination<T> { public Destination(int id) { } public int Id { get; set; } }
}
""";

foreach (var variant in new[] { "baseline", "names-only", "marker-only", "selected" })
foreach (var mode in new[] { "shared", "nullable", "tuple", "family" })
{
    if (variant != "selected" && mode != "shared") continue;
    foreach (var friend in variant == "selected" ? new[] { false, true } : new[] { true })
    {
        var producer = Compile("AuditProducer", CrossSource("Producer", mode, variant, friend), runtimeRef);
        EnsureClean(producer);
        foreach (var shape in new[] { "source", "dll", "ref" })
        {
            var producerRef = shape == "source" ? producer.ToMetadataReference() : Emit(producer, shape == "ref");
            var consumer = Compile("AuditConsumer", CrossSource("Consumer", mode, variant, false), runtimeRef, producerRef);
            var expected = mode == "shared" ? "object" : mode == "family" ? "Audit.ConsumerMapper<TMapper, T>" : "Audit.ConsumerMapper";
            Observe($"cross-{variant}-{mode}-{(friend ? "friend" : "isolated")}-{shape}", consumer, expected);
        }
    }
}
var legacyProducer = Compile("AuditProducer", CrossSource("Producer", "shared", "baseline", true), runtimeRef);
EnsureClean(legacyProducer);
foreach (var shape in new[] { "source", "dll", "ref" })
{
    var producerRef = shape == "source" ? legacyProducer.ToMetadataReference() : Emit(legacyProducer, shape == "ref");
    Observe("old-producer-selected-consumer-" + shape,
        Compile("AuditConsumer", CrossSource("Consumer", "shared", "selected", false), runtimeRef, producerRef), "object");
}
foreach (var mode in new[] { "generic", "generic-nested", "same-constraints", "tuple", "dynamic", "nullable" })
foreach (var variant in new[] { "baseline", "deduplicate", "nominal" })
{
    if (variant == "deduplicate" && mode != "tuple") continue;
    var family = Compile("AuditFamily", FamilySource(mode, variant), runtimeRef);
    Observe($"related-{mode}-{variant}", family, "by-context");
}
var invalidDerivedTuple = Compile("AuditInvalidTuple", FamilySource("tuple", "nominal")
    .Replace("(s.Item1)", "(s.Id)"), runtimeRef);
Observe("invalid-derived-tuple-falls-back-to-base", invalidDerivedTuple, "by-context");
var mixedWithoutLocal = Compile("AuditMixedWithoutLocal", MixedSource(includeLocal: false), runtimeRef);
Observe("shared-derived-without-local-surface", mixedWithoutLocal, "by-context");
var mixed = Compile("AuditMixed", MixedSource(), runtimeRef);
Observe("non-partial-base-shared-and-local-override", mixed, "by-context");
var missingRequired = Compile("AuditRequired", CrossSource("Consumer", "shared", "selected", false)
    .Replace("new(s.Id)", "new()") + models, runtimeRef);
Observe("required-construction-argument", missingRequired, "object");

var unexpected = results.Where(r =>
    r.Name.StartsWith("cross-selected-") || r.Name.EndsWith("-nominal") ||
    r.Name == "non-partial-base-shared-and-local-override")
    .Where(r => r.Diagnostics.Count != 0 || r.BindingMismatches != 0).Select(r => r.Name).ToArray();
File.WriteAllText(Path.Combine(outputRoot, "summary.json"), JsonSerializer.Serialize(new
{
    Language = "C# 9", Roslyn = typeof(CSharpCompilation).Assembly.GetName().Version?.ToString(),
    Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    Unexpected = unexpected,
    Cases = results.Select(r => new { r.Name, r.Diagnostics, Bindings = r.Bindings.Length, r.BindingMismatches })
}, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(Path.Combine(outputRoot, "results.json"), JsonSerializer.Serialize(results,
    new JsonSerializerOptions { WriteIndented = true }));
foreach (var result in results)
    Console.WriteLine($"{result.Name}: {string.Join(",", result.Diagnostics.Keys)}; bindings={result.Bindings.Length}; mismatches={result.BindingMismatches}");

if (unexpected.Length != 0) throw new InvalidOperationException("Unexpected results: " + string.Join(", ", unexpected));

CSharpCompilation Compile(string name, string source, params MetadataReference[] extra) =>
    CSharpCompilation.Create(name, new[] { CSharpSyntaxTree.ParseText(source, parse, "Input.cs") }, refs.Concat(extra), options);

MetadataReference Emit(CSharpCompilation compilation, bool reference)
{
    using var stream = new MemoryStream();
    var emit = compilation.Emit(stream, options: reference ? new EmitOptions(metadataOnly: true, includePrivateMembers: false) : null);
    if (!emit.Success) throw new InvalidOperationException(string.Join("\n", emit.Diagnostics));
    return MetadataReference.CreateFromImage(stream.ToArray());
}
void EnsureClean(CSharpCompilation compilation)
{
    var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning).ToArray();
    if (diagnostics.Length != 0) throw new InvalidOperationException(string.Join("\n", diagnostics.Select(d => d.ToString())));
}
void Observe(string name, CSharpCompilation compilation, string expected)
{
    var dir = Path.Combine(outputRoot, name);
    Directory.CreateDirectory(dir);
    var tree = compilation.SyntaxTrees.Single();
    var model = compilation.GetSemanticModel(tree);
    var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning).ToArray();
    var bindings = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
        .Where(x => x.Expression is MemberAccessExpressionSyntax member && overloads.Any(o => o.Name == member.Name.Identifier.ValueText))
        .Select(x =>
        {
            var context = x.Ancestors().OfType<ClassDeclarationSyntax>().First().Identifier.ValueText;
            var method = model.GetSymbolInfo(x).Symbol as IMethodSymbol;
            var definition = method?.ReducedFrom ?? method;
            var receiver = definition?.Parameters[0].Type as INamedTypeSymbol;
            var owner = receiver?.Name == "IMappingBuilder" ? receiver.TypeArguments[0].ToDisplayString() : "UNSCOPED";
            var expectedOwner = expected == "by-context" ? context switch
            {
                "Root" => (name.Contains("generic") || name.Contains("same-constraints")) ? "Audit.Root<TMapper, T>" : "Audit.Root<TMapper>",
                "Derived" => name.Contains("generic") || name.Contains("same-constraints") ? "Audit.Derived<TMapper, T>" : "Audit.Derived<TMapper>",
                "Local" => "Audit.Local",
                _ => "object"
            } : expected;
            return new Binding(context, method?.Name ?? "UNBOUND", owner,
                definition?.Parameters[1].Type.ToDisplayString() ?? "UNBOUND", expectedOwner);
        }).ToArray();
    File.WriteAllText(Path.Combine(dir, "Input.cs"), tree.ToString());
    File.WriteAllLines(Path.Combine(dir, "diagnostics.txt"), diagnostics.Select(d => d.ToString()));
    results.Add(new Observation(name, diagnostics.GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.Count()),
        bindings.Count(b => b.Owner != b.ExpectedOwner), bindings));
}
string Plans(string ns, bool generic = false, string field = "Id") => $$"""
namespace {{ns}}
{
    internal sealed class DestinationConstruction{{(generic ? "<T>" : "")}}
    { public DestinationConstruction(int {{field}}) { } }
    internal sealed class DestinationMembers{{(generic ? "<T>" : "")}}
    { public int {{field}} { get; set; } }
}
""";
string Extensions(string name, string mapper, string owner, string source, string destination, string plan,
    string generic, string constraints, bool nominal, string field = "Id")
{
    var builder = $"MappingBuilder<{mapper}, {source}, {destination}>";
    var receiver = nominal ? $"IMappingBuilder<{owner}, {builder}>" : owner == "object" ? $"MapperBuilderBase<{builder}>" : builder;
    var planArguments = generic.Contains(", T") ? destination["Shared.Destination".Length..] : "";
    var construction = plan + ".DestinationConstruction" + planArguments;
    var members = plan + ".DestinationMembers" + planArguments;
    return "namespace Audit { internal static partial class " + name + " {\n" + string.Join("\n", overloads.Select(o =>
    {
        var callback = o.Delegate.Replace("$MS", source.StartsWith("(") ? source : source.TrimEnd('?') + "?")
            .Replace("$S", source.TrimEnd('?')).Replace("$P", destination.TrimEnd('?'))
            .Replace("$CTX", o.Name is "Construct" or "Resolve" or "Members" ? "Morphant.Context.MappingContextMarker" : "Morphant.Context.MappingContext")
            .Replace("$D", destination).Replace("$C", construction).Replace("$M", members);
        return $"public static {builder} {o.Name}{generic}(this {receiver} builder, Morphant.Delegates.{callback} callback) {constraints} => throw new System.NotSupportedException();";
    })) + "\n} }\n";
}
string Calls(string source, string destination, string plan, bool explicitNames = false, string field = "Id")
{
    var tuple = destination.StartsWith("(");
    var create = tuple ? "(s.Item1, s.Item2)" : "new " + destination.TrimEnd('?') + "(s.Id)";
    var calls = overloads.Select(o => $"builder.Map<{source}, {destination}>().Setting().{o.Name}(" +
        o.Lambda.Replace("$NEW", create).Replace("$MANUAL", create.Replace("s.Id", "s!.Id"))
            .Replace("s!.Id", source.StartsWith("(") ? "s.Item1" : "s!.Id")
            .Replace("s.Id", source.StartsWith("(") ? "s.Item1" : "s.Id")
            .Replace("{ Id =", "{ " + field + " =") + ").Setting();");
    var result = string.Join("\n", calls);
    if (explicitNames)
        result += $"\nbuilder.Map<{source}, {destination}>().Construct(s => new {plan}.DestinationConstruction(s.Id));\n" +
            $"builder.Map<{source}, {destination}>().Members(s => new {plan}.DestinationMembers {{ Id = s.Id }});";
    return result;
}
string CrossSource(string side, string mode, string variant, bool friend)
{
    var family = mode == "family";
    var names = variant is "selected" or "names-only";
    var nominal = variant is "selected" or "marker-only";
    var scope = "Audit.Generated.A_" + side;
    var plan = names ? scope + ".Plans" : "Audit.Generated.Plans";
    var source = family ? "Shared.Source<T>" : mode == "tuple" ? "(int Id, int Other)" : "Shared.Source" + (mode == "nullable" ? "?" : "");
    var destination = family ? "Shared.Destination<T>" : mode == "tuple" ? "(int Id, int Other)" : "Shared.Destination" + (mode == "nullable" ? "?" : "");
    var mapper = mode == "shared" || family ? "TMapper" : side + "Mapper";
    var owner = mode == "shared" ? "object" : side + "Mapper" + (family ? "<TMapper, T>" : "");
    var generic = family ? "<TMapper, T>" : mode == "shared" ? "<TMapper>" : "";
    var constraints = family ? $"where TMapper : {side}Mapper<TMapper, T> where T : class" + (side == "Consumer" ? ", new()" : "")
        : mode == "shared" ? $"where TMapper : TypeMapper<TMapper>" + (nominal ? $", {scope}.LocalMapper" : "") : "";
    return header + (friend ? "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"AuditConsumer\")]\n" : "") +
        (side == "Producer" ? models : "") + Plans(plan, family) +
        $"namespace {scope} {{ internal interface LocalMapper {{ }} }}\n" +
        $"namespace Audit {{ public partial class {side}Mapper{(family ? "<TMapper, T>" : "")} : TypeMapper<{(family ? "TMapper" : side + "Mapper")}>, {scope}.LocalMapper " +
        (family ? constraints : "") + " { protected override void Configure(MapperBuilder builder) {\n" +
        Calls(source, destination, plan, mode == "shared") + "\n} } }\n" +
        Extensions("Extensions", mapper, owner, source, destination, plan, generic, constraints, nominal || mode != "shared");
}
string FamilySource(string mode, string variant)
{
    var generic = mode.StartsWith("generic") || mode == "same-constraints";
    var familyArgs = generic ? "<TMapper, T>" : "<TMapper>";
    var source = generic ? "Shared.Source<T>" : mode == "tuple" ? "(int Id, int Other)" : mode == "dynamic" ? "Shared.Source<object>" : "Shared.Source<string>";
    var destination = generic ? "Shared.Destination<T>" : "Shared.Destination";
    var localDestination = mode == "generic-nested" ? "Shared.Destination<System.Collections.Generic.List<T>>" : destination;
    var localSource = mode == "generic-nested" ? "Shared.Source<System.Collections.Generic.List<T>>" : mode == "tuple" ? "(int Code, int Other)" : mode == "dynamic" ? "Shared.Source<dynamic>" : mode == "nullable" ? "Shared.Source<string?>" : source;
    var rootConstraints = "where TMapper : Root" + familyArgs + (generic ? " where T : class" : "");
    var derivedConstraints = "where TMapper : Derived" + familyArgs + (generic ? " where T : class" + (mode.StartsWith("generic") ? ", new()" : "") : "");
    var plan = "Audit.Generated.Plans";
    var result = header + models + Plans(plan, generic) +
        $"namespace Audit {{ public abstract class Root{familyArgs} : TypeMapper<TMapper> {rootConstraints} {{ protected override void Configure(MapperBuilder builder) {{ " + Calls(source, destination, plan) + " } }\n" +
        $"public abstract class Derived{familyArgs} : Root{(mode == "generic-nested" ? "<TMapper, System.Collections.Generic.List<T>>" : familyArgs)} {derivedConstraints} {{ protected override void Configure(MapperBuilder builder) {{ " + Calls(localSource, localDestination, plan) + " } } }\n";
    result += Extensions(variant == "baseline" ? "RootExtensions" : "Extensions", "TMapper", "Root" + familyArgs, source, destination, plan, familyArgs, rootConstraints, variant != "baseline");
    if (variant != "deduplicate") result += Extensions(variant == "baseline" ? "DerivedExtensions" : "Extensions", "TMapper", "Derived" + familyArgs, localSource, localDestination, plan, familyArgs, derivedConstraints, variant != "baseline");
    return result;
}
string MixedSource(bool includeLocal = true)
{
    const string plan = "Audit.Generated.Plans";
    var result = header + models + Plans(plan) + "namespace Audit { internal interface LocalMapper { }\n" +
        "public abstract class Root<TMapper> : TypeMapper<TMapper> where TMapper : Root<TMapper> { protected override void Configure(MapperBuilder builder) { " + Calls("Shared.Source?", "Shared.Destination?", plan) + " } }\n" +
        "public partial class Local : Root<Local>, LocalMapper { protected override void Configure(MapperBuilder builder) { " + Calls("Shared.Source", "Shared.Destination", plan, true) + " } }\n" +
        "public partial class Direct : TypeMapper<Direct>, LocalMapper { protected override void Configure(MapperBuilder builder) { " + Calls("Shared.Source", "Shared.Destination", plan, true) + " } } }\n";
    result += Extensions("Extensions", "TMapper", "object", "Shared.Source", "Shared.Destination", plan, "<TMapper>", "where TMapper : TypeMapper<TMapper>, LocalMapper", true);
    result += Extensions("Extensions", "TMapper", "Root<TMapper>", "Shared.Source?", "Shared.Destination?", plan, "<TMapper>", "where TMapper : Root<TMapper>", true);
    if (includeLocal) result += Extensions("Extensions", "Local", "Local", "Shared.Source", "Shared.Destination", plan, "", "", true);
    return result;
}
record Binding(string Context, string Method, string Owner, string Callback, string ExpectedOwner);
record Observation(string Name, Dictionary<string, int> Diagnostics, int BindingMismatches, Binding[] Bindings);
