using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.UnitTests.MappingRegistrationTests;

[TestFixture]
internal sealed class InheritedExtensionPresentationTests
{
    [Test]
    public void Closed_descendants_check_the_presentation_of_the_shared_open_extension()
    {
        const string source = """
#nullable enable
using Morphant;
internal abstract class A<T> : TypeMapper
{
    protected override void Configure(MapperBuilder b) => b.Map<(T Value, int Count), int>().Convert(_ => 1);
}
internal abstract class B<U> : TypeMapper
{
    protected override void Configure(MapperBuilder b) => b.Map<(U Key, int Size), int>().Convert(_ => 2);
}
[MorphantMapper] internal partial class First : A<string>
{
    protected override void Configure(MapperBuilder b)
    {
        base.Configure(b);
        b.Map<(string Value, int Count), int>().IncludeBase<(string Value, int Count), int>();
    }
}
[MorphantMapper] internal partial class Second : B<int>
{
    protected override void Configure(MapperBuilder b)
    {
        base.Configure(b);
        b.Map<(int Key, int Size), int>().IncludeBase<(int Key, int Size), int>();
    }
}
""";
        var result = MappingRegistrationGeneratorTest.Run(source, LanguageVersion.CSharp9);
        var diagnostic = result.Diagnostics.Single();
        Assert.That(diagnostic.Id, Is.EqualTo("MORPH0056"));
        Assert.That(diagnostic.Location.SourceSpan.Start, Is.EqualTo(source.IndexOf("Map<(U Key", StringComparison.Ordinal)));
        Assert.That(diagnostic.AdditionalLocations.Single().SourceSpan.Start,
            Is.EqualTo(source.IndexOf("Map<(T Value", StringComparison.Ordinal)));
        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        var repaired = MappingRegistrationGeneratorTest.Run(source.Replace("Key", "Value").Replace("Size", "Count"),
            LanguageVersion.CSharp9, driver: result.Driver);
        Assert.That(repaired.Diagnostics, Is.Empty);
        Assert.That(repaired.CompilerWarningsAndErrors, Is.Empty);
    }

    [Test]
    public void A_base_template_cannot_transfer_ownership_to_a_later_conflicting_mapper()
    {
        const string derived = """
#nullable enable
using Morphant;
[MorphantMapper] internal partial class Derived : Base
{
    protected override void Configure(MapperBuilder b)
    {
        base.Configure(b);
        b.Map<(int Value, int Count), int>().Convert(s => s.Value);
    }
}
""";
        const string other = """
#nullable enable
using Morphant;
[MorphantMapper] internal partial class Other : TypeMapper
{
    protected override void Configure(MapperBuilder b) => b.Map<(int Key, int Size), int>().Convert(_ => 0);
}
""";
        const string template = """
#nullable enable
using Morphant;
internal abstract class Base : TypeMapper
{
    protected override void Configure(MapperBuilder b) => b.Map<(int Value, int Count), int>().Convert(s => s.Value);
}
""";
        var files = new RegistrationSourceFile[] { new("B_Other.cs", other), new("Z_Base.cs", template), new("A_Derived.cs", derived) };
        var result = MappingRegistrationGeneratorTest.Run(files, LanguageVersion.CSharp9);
        var error = result.Diagnostics.Single();
        Assert.That(error.Id, Is.EqualTo("MORPH0056"));
        Assert.That(error.Location.SourceTree!.FilePath, Is.EqualTo("B_Other.cs"));
        Assert.That(error.AdditionalLocations.Single().SourceTree!.FilePath, Is.EqualTo("A_Derived.cs"));
        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
    }
}