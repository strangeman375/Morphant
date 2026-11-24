using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.MappingRegistrationTests;

[TestFixture]
internal sealed class OpenExtensionIntersectionTests
{
    [Test]
    public void Editing_a_constraint_in_a_separate_file_updates_both_open_mappers()
    {
        var maps = new RegistrationSourceFile("A.cs", """
#nullable enable
using Morphant;
internal class Triple<A,B,C> { }
internal class Dto { }
[MorphantMapper] internal partial class A<U> : TypeMapper where U : class, new()
{
    protected override void Configure(MapperBuilder b) => b.Map<Triple<int,U,Ref<U>>,Dto>();
}
""");
        var other = new RegistrationSourceFile("B.cs", """
#nullable enable
using Morphant;
[MorphantMapper] internal partial class B<T,V> : TypeMapper
{
    protected override void Configure(MapperBuilder b) => b.Map<Triple<T,int,V>,Dto>();
}
""");
        var bound = new RegistrationSourceFile("Bound.cs", "internal class Ref<T> where T : class { }");
        var initial = MappingRegistrationGeneratorTest.Run([maps, other, bound], LanguageVersion.CSharp9);
        var driver = initial.Driver;
        var original = initial.OutputCompilation.RemoveSyntaxTrees(driver.GetRunResult().GeneratedTrees);
        var tree = original.SyntaxTrees.Single(t => t.FilePath == bound.Name);
        var edited = bound with { Source = bound.Source.Replace(" : class", " : new()") };
        var changed = original.ReplaceSyntaxTree(tree, tree.WithChangedText(SourceText.From(edited.Source, Encoding.UTF8)));
        var fresh = MappingRegistrationGeneratorTest.Run([maps, other, edited], LanguageVersion.CSharp9);
        Assert.That(initial.Diagnostics, Is.Empty);
        Assert.That(fresh.Diagnostics.Select(d => d.Id), Is.EqualTo(new[] { "MORPH0064" }));
        CheckIncrementalRun(ref driver, changed, fresh);
        CheckIncrementalRun(ref driver, original, initial);
    }

    [TestCase(ReportDiagnostic.Default)]
    [TestCase(ReportDiagnostic.Error)]
    public void Warning_promotion_preserves_constructible_open_intersections(ReportDiagnostic warnings)
    {
        const string source = """
#nullable enable
#pragma warning disable CS0618
using Morphant;
internal class Box<A,B,C> { }
[System.Obsolete("Legacy")] internal class Ref<T> where T : class { }
[MorphantMapper] internal partial class A<X,P> : TypeMapper where X : class
{
    protected override void Configure(MapperBuilder b) => b.Map<Box<Ref<X>,int,P>,int>().Convert(_ => 1);
}
[MorphantMapper] internal partial class B<Y,Q> : TypeMapper where Y : class
{
    protected override void Configure(MapperBuilder b) => b.Map<Box<Ref<Y>,Q,string>,int>().Convert(_ => 2);
}
""";
        var result = GeneratorTestDriver.Run("Consumer", source, LanguageVersion.CSharp9,
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable, generalDiagnosticOption: warnings));
        Assert.That(result.Diagnostics.Select(d => d.Id), Is.EqualTo(new[] { "MORPH0064" }));
        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
    }

    [Test]
    public void Adding_a_constrained_type_to_a_new_file_uses_the_current_compilation()
    {
        const string existing = """
#nullable enable
using Morphant;
internal sealed class Triple<A, B, C> { }
internal sealed class Dto { }
[MorphantMapper] internal partial class Existing<T, U> : TypeMapper
{
    protected override void Configure(MapperBuilder b) => b.Map<Triple<T, int, U>, Dto>();
}
""";
        const string added = """
#nullable enable
using Morphant;
internal sealed class Ref<T> where T : class { }
[MorphantMapper] internal partial class Added<X> : TypeMapper where X : class
{
    protected override void Configure(MapperBuilder b) => b.Map<Triple<int, X, Ref<X>>, Dto>();
}
""";
        var oldFile = new RegistrationSourceFile("Existing.cs", existing);
        var newFile = new RegistrationSourceFile("Added.cs", added);
        var initial = MappingRegistrationGeneratorTest.Run([oldFile], LanguageVersion.CSharp9);
        var driver = initial.Driver;
        var input = initial.OutputCompilation.RemoveSyntaxTrees(driver.GetRunResult().GeneratedTrees);
        var addedTree = CSharpSyntaxTree.ParseText(SourceText.From(added, Encoding.UTF8),
            (CSharpParseOptions)input.SyntaxTrees.Single().Options, newFile.Name);
        var fresh = MappingRegistrationGeneratorTest.Run([oldFile, newFile], LanguageVersion.CSharp9);
        Assert.That(fresh.Diagnostics, Is.Empty);
        Assert.That(fresh.CompilerWarningsAndErrors, Is.Empty);
        driver = driver.RunGeneratorsAndUpdateCompilation(input.AddSyntaxTrees(addedTree), out var output, out var diagnostics);
        Assert.That(diagnostics, Is.Empty);
        Assert.That(output.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty);
        Assert.That(driver.GetRunResult().Results.Single().GeneratedSources.Select(f => (f.HintName, f.SourceText.ToString())),
            Is.EquivalentTo(fresh.GeneratedSources.Select(f => (f.HintName, f.SourceText.ToString()))));
    }

    [TestCase("class", "int", false)]
    [TestCase("class", "string", true)]
    [TestCase("struct", "string", false)]
    [TestCase("struct", "int", true)]
    [TestCase("unmanaged", "Cell<string>", false)]
    [TestCase("unmanaged", "Cell<int>", true)]
    [TestCase("System.IDisposable", "int", false)]
    [TestCase("System.IDisposable", "System.IO.MemoryStream", true)]
    [TestCase("new()", "string", false)]
    [TestCase("new()", "object", true)]
    public void Only_constructible_intersections_conflict(string requirement, string argument, bool conflicts)
    {
        var source = $$"""
#nullable enable
using Morphant;
internal sealed class Triple<A, B, C> { }
internal sealed class Bound<T> where T : {{requirement}} { }
internal struct Cell<T> { public T Value { get; set; } }
internal sealed class Dto { }
[MorphantMapper] internal partial class First<T> : TypeMapper where T : {{requirement}}
{
    protected override void Configure(MapperBuilder b) => b.Map<Triple<T, int, Bound<T>>, Dto>();
}
[MorphantMapper] internal partial class Second<U, V> : TypeMapper
{
    protected override void Configure(MapperBuilder b) => b.Map<Triple<{{argument}}, U, V>, Dto>();
}
""";
        var result = MappingRegistrationGeneratorTest.Run(source, LanguageVersion.CSharp9);
        Assert.That(result.Diagnostics.Select(d => d.Id), Is.EqualTo(conflicts ? new[] { "MORPH0064" } : Array.Empty<string>()));
        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
    }

    [TestCase("T", "int", "", true)]
    [TestCase("T[]", "int", "", false)]
    [TestCase("T[]", "int[]", "", true)]
    [TestCase("T", "int", "[]", true)]
    [TestCase("T[]", "int", "[]", false)]
    public void An_invariant_interface_can_fix_a_remaining_type_parameter(string implemented, string required, string suffix, bool conflicts)
    {
        var source = $$"""
#nullable enable
using Morphant;
internal sealed class Triple<A,B,C> { }
internal interface IInv<T> { }
internal sealed class Box<T> : IInv<{{implemented}}> { }
internal sealed class Rel<A,B> where A : B { }
internal sealed class Dto { }
[MorphantMapper] internal partial class First<A,B> : TypeMapper where A : B
{
 protected override void Configure(MapperBuilder b) => b.Map<Triple<A,B,Rel<A,B>>,Dto>();
}
[MorphantMapper] internal partial class Second<T,Z> : TypeMapper
{
 protected override void Configure(MapperBuilder b) => b.Map<Triple<Box<T>{{suffix}},IInv<{{required}}>{{suffix}},Z>,Dto>();
}
""";
        var result = MappingRegistrationGeneratorTest.Run(source, LanguageVersion.CSharp9);
        Assert.That(result.Diagnostics.Select(d => d.Id), Is.EqualTo(conflicts ? new[] { "MORPH0064" } : Array.Empty<string>()));
        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
    }

    [TestCase("struct", false)]
    [TestCase("System.IDisposable", true)]
    public void A_remaining_free_parameter_must_satisfy_both_definitions(string requirement, bool conflicts)
    {
        var source = $$"""
#nullable enable
using Morphant;
internal sealed class Triple<A, B, C> { }
internal sealed class Ref<T> where T : class { }
internal sealed class Bound<T> where T : {{requirement}} { }
internal sealed class Dto { }
[MorphantMapper] internal partial class First<T, X> : TypeMapper where T : class
{
    protected override void Configure(MapperBuilder b) => b.Map<Triple<T, Ref<T>, X>, Dto>();
}
[MorphantMapper] internal partial class Second<U, Y> : TypeMapper where U : {{requirement}}
{
    protected override void Configure(MapperBuilder b) => b.Map<Triple<U, Y, Bound<U>>, Dto>();
}
""";
        var result = MappingRegistrationGeneratorTest.Run(source, LanguageVersion.CSharp9);
        Assert.That(result.Diagnostics.Select(d => d.Id), Is.EqualTo(conflicts ? new[] { "MORPH0064" } : Array.Empty<string>()));
        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
    }

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
        CheckIncrementalRun(ref driver, changed, fresh);
        CheckIncrementalRun(ref driver, original, initial);
    }

    private static void CheckIncrementalRun(ref GeneratorDriver driver, Compilation compilation,
        MappingRegistrationGeneratorResult expected)
    {
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        Assert.That(diagnostics.Select(d => (d.Id, d.GetMessage(), d.Location.GetLineSpan())),
            Is.EquivalentTo(expected.Diagnostics.Select(d => (d.Id, d.GetMessage(), d.Location.GetLineSpan()))));
        Assert.That(diagnostics.SelectMany(d => d.AdditionalLocations).Select(l => l.GetLineSpan()),
            Is.EquivalentTo(expected.Diagnostics.SelectMany(d => d.AdditionalLocations).Select(l => l.GetLineSpan())));
        Assert.That(output.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty);
        var generated = driver.GetRunResult().Results.Single().GeneratedSources;
        Assert.That(generated.Select(f => (f.HintName, f.SourceText.ToString())),
            Is.EquivalentTo(expected.GeneratedSources.Select(f => (f.HintName, f.SourceText.ToString()))));
    }
}
