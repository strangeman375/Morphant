using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class StaticContainerTypeUsageTests
{
    [TestCase("static class")]
    [TestCase("ref struct")]
    public void Names_nested_ordinary_types_independently_of_container_root_eligibility(string declaration)
    {
        const string source =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
public __CONTAINER__ Models<T>
{
    public static class Inner
    {
        public sealed class Source { public T Id { get; set; } = default!; }
        public sealed class Destination { public T Id { get; set; } = default!; }
    }
}
[MorphantMapper]
public sealed partial class Mapper : TypeMapper<Mapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Models<int>.Inner.Source, Models<int>.Inner.Destination>();
}
public static class Usage
{
    public static ITypeMapper<Models<int>.Inner.Source, Models<int>.Inner.Destination> Create() => new Mapper();
}
""";
        var result = GeneratorTestDriver.Run("StaticContainerTypes",
            source.Replace("__CONTAINER__", declaration), LanguageVersion.CSharp9);
        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
    }
}
