using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.MappingRegistrationTests;

[TestFixture]
internal sealed class FriendExtensionUsageTests
{
    private const string Library = """
#nullable enable
using Morphant;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Consumer")]
internal class Box<A,B> { public int Id { get; set; } }
internal class Dto { public int Id { get; set; } }
[MorphantMapper] internal partial class Foreign<U> : TypeMapper
{
    protected override void Configure(MapperBuilder b)
    {
        b.Map<Box<string,U>,Dto>().Convert(_ => new Dto());
        b.Map<Dto,Dto>();
    }
}
""";

    [TestCase(false)]
    [TestCase(true)]
    public void A_namespace_alias_does_not_import_friend_extensions(bool projectReference)
    {
        var reference = CreateLibrary(projectReference);
        const string source = """
#nullable enable
using M = global::Morphant;
[M.MorphantMapper] internal partial class Local : M.TypeMapper
{
    protected override void Configure(MapperBuilder b) => b.Map<Dto,Dto>();
}
""";
        var hidden = GeneratorTestDriver.Run("Consumer", source, LanguageVersion.CSharp9,
            additionalReferences: [reference]);
        var visible = GeneratorTestDriver.Run("Consumer", source.Replace("using M =", "using Morphant;\nusing M ="),
            LanguageVersion.CSharp9, driver: hidden.Driver, additionalReferences: [reference]);
        var recovered = GeneratorTestDriver.Run("Consumer", source, LanguageVersion.CSharp9,
            driver: visible.Driver, additionalReferences: [reference]);
        Assert.That(hidden.Diagnostics, Is.Empty);
        Assert.That(hidden.CompilerWarningsAndErrors, Is.Empty);
        Assert.That(visible.Diagnostics.Select(d => d.Id), Is.EqualTo(new[] { "MORPH0065" }));
        Assert.That(visible.CompilerWarningsAndErrors, Is.Empty);
        Assert.That(recovered.Diagnostics, Is.Empty);
        Assert.That(recovered.CompilerWarningsAndErrors, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void A_closed_registration_selects_its_callback_over_intersecting_local_and_friend_extensions(bool projectReference)
    {
        const string source = """
#nullable enable
using Morphant;
internal abstract class Base<T> : TypeMapper
{
    protected override void Configure(MapperBuilder b) => b.Map<Box<T,int>,Dto>().Convert(_ => new Dto());
}
[MorphantMapper] internal partial class Derived : Base<string>
{
    protected override void Configure(MapperBuilder b)
    {
        base.Configure(b);
        b.Map<Box<string,int>,Dto>().Convert(s => new Dto { Id = (s?.Id ?? 0) + 1 });
    }
}
""";
        var result = GeneratorTestDriver.Run("Consumer", source, LanguageVersion.CSharp9,
            additionalReferences: [CreateLibrary(projectReference)]);
        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);

        var tree = result.OutputCompilation.SyntaxTrees.First();
        var call = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.ValueText == "Derived").DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(i => i.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Convert" });
        var bound = (IMethodSymbol)result.OutputCompilation.GetSemanticModel(tree).GetSymbolInfo(call).Symbol!;
        Assert.That(bound.ContainingAssembly.Name, Is.EqualTo("Consumer"));
        Assert.That(bound.ReducedFrom!.Arity, Is.Zero);
    }

    private static MetadataReference CreateLibrary(bool projectReference)
    {
        var result = GeneratorTestDriver.Run("Library", Library, LanguageVersion.CSharp9);
        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        if (projectReference) return result.OutputCompilation.ToMetadataReference();
        using var stream = new MemoryStream();
        var emitted = result.OutputCompilation.Emit(stream);
        Assert.That(emitted.Success, Is.True, string.Join(Environment.NewLine, emitted.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }
}
