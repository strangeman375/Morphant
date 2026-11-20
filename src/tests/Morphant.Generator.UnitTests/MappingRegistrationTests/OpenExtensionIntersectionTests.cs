using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.UnitTests.MappingRegistrationTests;

[TestFixture]
internal sealed class OpenExtensionIntersectionTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void Extensions_from_the_same_mapper_or_enclosing_type_have_independent_parameters(bool sameMapper)
    {
        const string declarations = """
#nullable enable
using Morphant;
internal sealed class Box<X, Y> { }
internal sealed class Dto { }
""";
        const string first = "b.Map<Box<T, int>, Dto>().Convert(_ => new Dto());";
        const string second = "b.Map<Box<string, T>, Dto>().Convert(_ => new Dto());";
        var scenario = sameMapper ? $$"""

internal abstract class Base<T> : TypeMapper
{
    protected override void Configure(MapperBuilder b)
    {
        {{first}}
        {{second}}
    }
}
[MorphantMapper] internal partial class Closed : Base<string>
{
    protected override void Configure(MapperBuilder b)
    {
        base.Configure(b);
        b.Map<Box<string, int>, Dto>().Convert(_ => new Dto());
    }
}
""" : $$"""

internal partial class Family<T>
{
    [MorphantMapper] internal partial class First : TypeMapper
    {
        protected override void Configure(MapperBuilder b) => {{first}}
    }
    [MorphantMapper] internal partial class Second : TypeMapper
    {
        protected override void Configure(MapperBuilder b) => {{second}}
    }
}
""";
        var result = MappingRegistrationGeneratorTest.Run(declarations + scenario, LanguageVersion.CSharp9);
        Assert.That(result.Diagnostics.Select(d => d.Id), Is.EqualTo(new[] { "MORPH0064" }));
        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        var diagnostic = result.Diagnostics.Single();
        Assert.That(diagnostic.AdditionalLocations, Has.Count.EqualTo(1));
        Assert.That(new[] { diagnostic.Location, diagnostic.AdditionalLocations[0] }
            .Select(location => location.SourceTree!.GetText().Lines.GetLineFromPosition(location.SourceSpan.Start).ToString()),
            Is.EquivalentTo(new[] { first, second }.Select(call =>
                scenario.Split('\n').Single(line => line.Contains(call, StringComparison.Ordinal)).TrimEnd('\r'))));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Editing_or_adding_one_file_finds_the_same_intersection_as_a_clean_build(bool add)
    {
        const string first = """
#nullable enable
using Morphant;
internal class Box<X, Y> { }
internal class Dto { }
[MorphantMapper] internal partial class First<T> : TypeMapper
{
    protected override void Configure(MapperBuilder b) => b.Map<Box<T, int>, Dto>();
}
""";
        const string second = """
#nullable enable
using Morphant;
[MorphantMapper] internal partial class Second<U> : TypeMapper
{
    protected override void Configure(MapperBuilder b) => b.Map<Box<U, long>, Dto>();
}
""";
        var firstFile = new RegistrationSourceFile("First.cs", first);
        var secondFile = new RegistrationSourceFile("Second.cs", second);
        var initial = MappingRegistrationGeneratorTest.Run(add ? [firstFile] : [firstFile, secondFile], LanguageVersion.CSharp9);
        Assert.That(initial.Diagnostics, Is.Empty);
        Assert.That(initial.CompilerWarningsAndErrors, Is.Empty);
        var driver = initial.Driver;
        var original = initial.OutputCompilation.RemoveSyntaxTrees(driver.GetRunResult().GeneratedTrees);
        var edited = second.Replace("Box<U, long>", "Box<string, U>");
        var editedTree = CSharpSyntaxTree.ParseText(SourceText.From(edited, Encoding.UTF8),
            (CSharpParseOptions)original.SyntaxTrees.First().Options, secondFile.Name);
        var changed = add ? original.AddSyntaxTrees(editedTree) :
            original.ReplaceSyntaxTree(original.SyntaxTrees.Single(tree => tree.FilePath == secondFile.Name), editedTree);
        var fresh = MappingRegistrationGeneratorTest.Run([firstFile, secondFile with { Source = edited }], LanguageVersion.CSharp9);
        Assert.That(fresh.Diagnostics.Select(diagnostic => diagnostic.Id), Is.EqualTo(new[] { "MORPH0064" }));
        Check(changed, fresh);
        Check(original, initial);

        void Check(Compilation compilation, MappingRegistrationGeneratorResult expected)
        {
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Assert.That(diagnostics.Select(d => (d.Id, d.GetMessage(), d.Location.GetLineSpan())),
                Is.EquivalentTo(expected.Diagnostics.Select(d => (d.Id, d.GetMessage(), d.Location.GetLineSpan()))));
            Assert.That(output.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty);
            var generated = driver.GetRunResult().Results.Single().GeneratedSources;
            Assert.That(generated.Select(f => (f.HintName, f.SourceText.ToString())),
                Is.EquivalentTo(expected.GeneratedSources.Select(f => (f.HintName, f.SourceText.ToString()))));
        }
    }
}