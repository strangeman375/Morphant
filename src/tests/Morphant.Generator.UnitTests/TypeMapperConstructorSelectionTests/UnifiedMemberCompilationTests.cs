using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperConstructorSelectionTests;

[TestFixture]
internal sealed class UnifiedMemberCompilationTests
{
    [TestCase("Automatic", false)]
    [TestCase("Automatic", true)]
    [TestCase("ByConvention", false)]
    [TestCase("ByConvention", true)]
    [TestCase("Auto", false)]
    [TestCase("Auto", true)]
    [TestCase("Value", false)]
    [TestCase("Value", true)]
    [TestCase("ByConventionAuto", false)]
    [TestCase("ByConventionAuto", true)]
    [TestCase("ByConventionValue", false)]
    [TestCase("ByConventionValue", true)]
    [TestCase("Resolve", false)]
    [TestCase("Resolve", true)]
    [TestCase("Omitted", true)]
    public void Uses_members_to_supply_constructor_arguments_without_matching_source(
        string route, bool optional)
    {
        string construction = route switch
        {
            "Automatic" => "",
            "ByConvention" => ".Construct(_ => new(ByConvention()))",
            "Auto" => ".Construct(_ => new(Auto()))",
            "Value" => ".Construct(source => new(source.Obsolete()))",
            "ByConventionAuto" => ".Construct(_ => new(ByConvention(), new() { value = Auto() }))",
            "ByConventionValue" => ".Construct(source => new(ByConvention(), new() { value = source.Obsolete() }))",
            "Resolve" => ".Resolve((_, previous) => { if (previous.HasValue) return previous; return new(Auto()); })",
            "Omitted" => ".Construct(_ => new())",
            _ => throw new ArgumentOutOfRangeException(nameof(route))
        };
        string defaultValue = optional ? " = -1" : "";
        // lang=c#
        string source = $$"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
namespace TestCase
{
    public sealed class Source
    {
        public int Raw { get; set; }
        public int Obsolete() => throw new System.InvalidOperationException();
    }
    public sealed class Destination
    {
        public Destination(int value{{defaultValue}}) { Value = value; }
        public int Value { get; set; }
    }
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>(){{construction}}
                .Members(source => new() { Value = source.Raw + 100 });
    }
}
""";
        var result = GeneratorTestDriver.Run("UnifiedMemberConsumer", source, LanguageVersion.CSharp9);
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
