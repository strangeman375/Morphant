using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperConstructorSelectionTests;

[TestFixture]
internal sealed class ExplicitMemberCompilationTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void Compiles_explicit_members_with_nullable_constructor_contracts(
        bool byConvention, bool initOnly)
    {
        string construction = byConvention
            ? ".Construct(_ => new(ByConvention()))"
            : string.Empty;
        string accessor = initOnly ? "init" : "set";
        // lang=c#
        string source = $$"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
namespace TestCase
{
    public sealed class Source
    {
        public string? Value { get; set; }
    }
    public sealed class Destination
    {
        public Destination(string? value) { Value = value; }
        public string? Value { get; {{accessor}}; }
    }
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                {{construction}}
                .Members(source => new() { Value = source.Value ?? "explicit" });
    }
    public static class Usage
    {
        public static void Map()
        {
            ITypeMapper<Source, Destination> mapper = new Mapper();
            Destination created = mapper.Create(new Source());
            Destination updated = mapper.Update(new Source(), created);
            Destination replacement = mapper.Update(new Source(), null);
        }
    }
}
""";
        var result = GeneratorTestDriver.Run(
            "ExplicitMemberConsumer", source, LanguageVersion.CSharp9);
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [TestCase(false, false, false)]
    [TestCase(false, false, true)]
    [TestCase(false, true, false)]
    [TestCase(false, true, true)]
    [TestCase(true, false, false)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    [TestCase(true, true, true)]
    public void Satisfies_required_members_with_or_without_constructor_attribute(
        bool byConvention, bool setsRequiredMembers, bool initOnly)
    {
        string construction = byConvention
            ? ".Construct(_ => new(ByConvention()))"
            : string.Empty;
        string attribute = setsRequiredMembers ? "[SetsRequiredMembers]" : string.Empty;
        string accessor = initOnly ? "init" : "set";
        // lang=c#
        string source = $$"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
using System.Diagnostics.CodeAnalysis;
namespace TestCase
{
    public sealed class Source { public string Value { get; set; } = "source"; }
    public sealed class Destination
    {
        {{attribute}}
        public Destination(string value) { Value = value; }
        public required string Value { get; {{accessor}}; }
    }
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                {{construction}}
                .Members(source => new() { Value = source.Value + "-explicit" });
    }
    public static class Usage
    {
        public static void Map()
        {
            ITypeMapper<Source, Destination> mapper = new Mapper();
            Destination created = mapper.Create(new Source());
            Destination updated = mapper.Update(new Source(), created);
            Destination replacement = mapper.Update(new Source(), null);
        }
    }
}
""";
        var result = GeneratorTestDriver.Run(
            "ExplicitRequiredMemberConsumer", source, LanguageVersion.CSharp11);
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
