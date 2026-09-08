using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class DslIsolationUsageTests
{
    private static readonly string[] Callbacks =
    [
        ".Construct(s => new(s.Id))",
        ".Construct((s, c) => new(s.Id))",
        ".Resolve((s, p) => new(s.Id))",
        ".Resolve((s, p, c) => new(s.Id))",
        ".ConstructUsing(s => new __DESTINATION__(s.Id))",
        ".ConstructUsing((s, c) => new __DESTINATION__(s.Id))",
        ".ResolveUsing((s, p) => new __DESTINATION__(s.Id))",
        ".ResolveUsing((s, p, c) => new __DESTINATION__(s.Id))",
        ".Convert(s => new __DESTINATION__(s!.Id))",
        ".Convert((s, p) => new __DESTINATION__(s!.Id))",
        ".Convert((s, p, c) => new __DESTINATION__(s!.Id))",
        ".Members(s => new() { Id = s.Id })",
        ".Members((s, p) => new() { Id = s.Id })",
        ".Members((s, p, r) => new() { Id = s.Id })",
        ".Members((s, p, r, c) => new() { Id = s.Id })"
    ];

    private static IEnumerable<TestCaseData> FamilyCases =>
        from callback in Callbacks
        from constraint in new[] { "class", "class, new()" }
        from nested in new[] { false, true }
        select new TestCaseData(callback, constraint, nested);

    private static IEnumerable<TestCaseData> AssemblyCases =>
        from callback in Callbacks
        from referenceKind in new[] { "source", "dll", "ref" }
        from shape in new[] { "ordinary", "nullable", "tuple", "family", "distinct-source" }
        from friend in new[] { false, true }
        select new TestCaseData(callback, referenceKind, shape, friend);

    [TestCaseSource(nameof(FamilyCases))]
    public void Related_non_partial_families_select_their_own_callbacks_without_base_call(
        string callback,
        string constraint,
        bool nested)
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
public sealed class Source<T> { public int Id { get; set; } }
public sealed class Destination<T>
{
    public Destination(int id) => Id = id;
    public int Id { get; set; }
}
public sealed class Payload { }
public abstract class Root<TMapper, T> : TypeMapper<TMapper>
    where TMapper : Root<TMapper, T>
    where T : class
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source<T>, Destination<T>>()__CALLBACK__;
}
public abstract class Derived<TMapper, T> : Root<TMapper, T>
    where TMapper : Derived<TMapper, T>
    where T : __CONSTRAINT__
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source<__DERIVED_ARGUMENT__>, Destination<__DERIVED_ARGUMENT__>>()__DERIVED_CALLBACK__;
}
[MorphantMapper]
public partial class RootMapper : Root<RootMapper, Payload>
{
    protected override void Configure(MapperBuilder builder) => base.Configure(builder);
}
[MorphantMapper]
public partial class DerivedMapper : Derived<DerivedMapper, Payload>
{
    protected override void Configure(MapperBuilder builder) => base.Configure(builder);
}
""";
        var result = GeneratorTestDriver.Run(
            "RelatedFamilies",
            source.Replace("__CALLBACK__", callback.Replace("__DESTINATION__", "Destination<T>"))
                .Replace("__DERIVED_CALLBACK__", callback.Replace("__DESTINATION__", "Destination<__DERIVED_ARGUMENT__>"))
                .Replace("__DERIVED_ARGUMENT__", nested ? "System.Collections.Generic.List<T>" : "T")
                .Replace("__CONSTRAINT__", constraint),
            LanguageVersion.CSharp9);
        AssertClean(result);

        var syntaxTree = result.OutputCompilation.SyntaxTrees.First();
        var semanticModel = result.OutputCompilation.GetSemanticModel(syntaxTree);
        var owners = syntaxTree.GetRoot().DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(invocation => semanticModel.GetSymbolInfo(invocation).Symbol)
            .OfType<IMethodSymbol>()
            .Where(method => method.ReducedFrom is not null)
            .Select(method => ((INamedTypeSymbol)((INamedTypeSymbol)method.ReceiverType!)
                .TypeArguments[0]).Name);
        Assert.That(owners, Is.EqualTo(new[] { "Root", "Derived" }));
    }

    [TestCaseSource(nameof(AssemblyCases))]
    public void Referenced_assembly_extensions_do_not_compete_with_local_callbacks(
        string callback,
        string referenceKind,
        string shape,
        bool friend)
    {
        // lang=c#
        const string producerTemplate =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
__FRIEND_ATTRIBUTE__
namespace Shared
{
    public sealed class Source { public int Id { get; set; } }
    public sealed class Destination
    {
        public Destination(int id) => Id = id;
        public int Id { get; set; }
    }
}
[MorphantMapper]
public partial class ProducerMapper : TypeMapper<ProducerMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Shared.Source, Shared.Destination>();
}
""";
        // lang=c#
        const string consumerTemplate =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
[MorphantMapper]
public partial class ConsumerMapper : TypeMapper<ConsumerMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Shared.Source, Shared.Destination>()__CALLBACK__;
}
""";
        var producerSource = producerTemplate.Replace("__FRIEND_ATTRIBUTE__", friend
            ? "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"DslConsumer\")]" : "");
        var consumerSource = consumerTemplate;
        var destination = "Shared.Destination";
        switch (shape)
        {
            case "nullable":
                producerSource = producerSource.Replace("Map<Shared.Source, Shared.Destination>",
                    "Map<Shared.Source?, Shared.Destination?>");
                consumerSource = consumerSource.Replace("Map<Shared.Source, Shared.Destination>",
                    "Map<Shared.Source?, Shared.Destination?>");
                break;
            case "tuple":
                producerSource = producerSource.Replace("Map<Shared.Source, Shared.Destination>",
                    "Map<Shared.Source, (int Id, int Other)>");
                consumerSource = consumerSource.Replace("Map<Shared.Source, Shared.Destination>",
                    "Map<Shared.Source, (int Id, int Other)>");
                callback = callback.Replace("new(s.Id)", "new(s.Id, s.Id + 1)")
                    .Replace("new __DESTINATION__(s.Id)", "(s.Id, s.Id + 1)")
                    .Replace("new __DESTINATION__(s!.Id)", "(s!.Id, s.Id + 1)");
                break;
            case "family":
                producerSource = producerSource
                    .Replace("class Source", "class Source<T>")
                    .Replace("class Destination", "class Destination<T>")
                    .Replace("Map<Shared.Source, Shared.Destination>", "Map<Shared.Source<T>, Shared.Destination<T>>")
                    .Replace("ProducerMapper : TypeMapper<ProducerMapper>",
                        "ProducerMapper<TMapper, T> : TypeMapper<TMapper>\n" +
                        "    where TMapper : ProducerMapper<TMapper, T> where T : class");
                consumerSource = consumerSource
                    .Replace("Map<Shared.Source, Shared.Destination>", "Map<Shared.Source<T>, Shared.Destination<T>>")
                    .Replace("ConsumerMapper : TypeMapper<ConsumerMapper>",
                        "ConsumerMapper<TMapper, T> : TypeMapper<TMapper>\n" +
                        "    where TMapper : ConsumerMapper<TMapper, T> where T : class, new()");
                destination = "Shared.Destination<T>";
                break;
            case "distinct-source":
                consumerSource = consumerSource
                    .Replace("[MorphantMapper]", "public sealed class LocalSource { public int Id { get; set; } }\n[MorphantMapper]")
                    .Replace("Map<Shared.Source,", "Map<LocalSource,");
                break;
        }
        var producer = GeneratorTestDriver.Run(
            "DslProducer", producerSource, LanguageVersion.CSharp9);
        AssertClean(producer);
        MetadataReference reference;

        if (referenceKind == "source")
        {
            reference = producer.OutputCompilation.ToMetadataReference();
        }
        else
        {
            using var stream = new MemoryStream();
            var emit = producer.OutputCompilation.Emit(stream,
                options: new EmitOptions(
                    metadataOnly: referenceKind == "ref",
                    includePrivateMembers: referenceKind != "ref"));
            Assert.That(emit.Diagnostics, Is.Empty);
            reference = MetadataReference.CreateFromImage(stream.ToArray());
        }

        var consumer = GeneratorTestDriver.Run(
            "DslConsumer",
            consumerSource.Replace("__CALLBACK__",
                callback.Replace("__DESTINATION__", destination)),
            LanguageVersion.CSharp9,
            additionalReferences: [reference]);
        AssertClean(consumer);
    }

    [TestCaseSource(nameof(Callbacks))]
    public void Invalid_derived_tuple_callback_is_rejected_and_recovers_after_edit(string callback)
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
public sealed class Destination
{
    public Destination(int id) => Id = id;
    public int Id { get; set; }
}
public abstract class Root<TMapper> : TypeMapper<TMapper>
    where TMapper : Root<TMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<(int Id, int Other), Destination>()
            .Convert(s => new Destination(s.Id));
}
public abstract class Derived<TMapper> : Root<TMapper>
    where TMapper : Derived<TMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<(int Code, int Other), Destination>()
            __CALLBACK__;
}
[MorphantMapper]
public partial class RootMapper : Root<RootMapper>
{
    protected override void Configure(MapperBuilder builder) => base.Configure(builder);
}
[MorphantMapper]
public partial class DerivedMapper : Derived<DerivedMapper>
{
    protected override void Configure(MapperBuilder builder) => base.Configure(builder);
}
""";
        var invalidSource = source.Replace("__CALLBACK__",
            callback.Replace("__DESTINATION__", "Destination"));
        var invalid = GeneratorTestDriver.Run(
            "InvalidFamilyCallback", invalidSource, LanguageVersion.CSharp9);
        AssertRejected(invalid);

        var repairedSource = source.Replace("__CALLBACK__",
            callback.Replace("__DESTINATION__", "Destination").Replace(".Id", ".Code"));
        var repaired = GeneratorTestDriver.Run(
            "InvalidFamilyCallback", repairedSource, LanguageVersion.CSharp9,
            driver: invalid.Driver);
        AssertClean(repaired);

        var invalidAgain = GeneratorTestDriver.Run(
            "InvalidFamilyCallback", invalidSource, LanguageVersion.CSharp9,
            driver: repaired.Driver);
        AssertRejected(invalidAgain);

        void AssertRejected(GeneratorTestDriverResult result)
        {
            Assert.Multiple(() =>
            {
                Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
                Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Id),
                    Is.EqualTo(new[] { "MORPH0018" }));
                Assert.That(result.Diagnostics.Select(diagnostic =>
                        GeneratorTestDriver.GetSourceText(diagnostic.Location)),
                    Is.EqualTo(new[] { callback[1..callback.IndexOf('(')] }));
            });
        }
    }

    private static void AssertClean(GeneratorTestDriverResult result)
    {
        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
