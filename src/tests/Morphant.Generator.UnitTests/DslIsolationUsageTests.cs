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
        select new TestCaseData(callback, constraint);

    private static IEnumerable<TestCaseData> AssemblyCases =>
        from callback in Callbacks
        from referenceKind in new[] { "source", "dll", "ref" }
        select new TestCaseData(callback, referenceKind);

    [TestCaseSource(nameof(FamilyCases))]
    public void Related_non_partial_families_select_their_own_callbacks_without_base_call(
        string callback,
        string constraint)
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
        builder.Map<Source<T>, Destination<T>>()__CALLBACK__;
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
    public void Friend_assembly_extensions_do_not_compete_with_local_callbacks(
        string callback,
        string referenceKind)
    {
        // lang=c#
        const string producerSource =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("DslConsumer")]
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
        const string consumerSource =
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
                callback.Replace("__DESTINATION__", "Shared.Destination")),
            LanguageVersion.CSharp9,
            additionalReferences: [reference]);
        AssertClean(consumer);
    }

    [Test]
    public void Invalid_derived_tuple_callback_cannot_fall_back_to_the_base_family()
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
            .Convert(s => new Destination(s.Id));
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
        var result = GeneratorTestDriver.Run(
            "InvalidFamilyCallback", source, LanguageVersion.CSharp9);
        Assert.Multiple(() =>
        {
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(result.Diagnostics.Select(diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0018" }));
            Assert.That(result.Diagnostics.Select(diagnostic =>
                    GeneratorTestDriver.GetSourceText(diagnostic.Location)),
                Is.EqualTo(new[] { "Convert" }));
        });
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
