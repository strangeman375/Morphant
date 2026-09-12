using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ConstructionDiagnosticsTests;

[TestFixture]
internal sealed class StructuredAvailabilityTests
{
    [TestCase("var available = previous.HasValue; if (available) return previous.Value; return new(source.Name);")]
    [TestCase("var available = previous.TryGetValue(out var existing); if (available) return existing!; return new(source.Name);")]
    [TestCase("var available = previous.HasValue; var reuse = available && source.Reuse; if (reuse) return previous.Value; return new(source.Name);")]
    [TestCase("var unavailable = !previous.HasValue; if (unavailable) return new(source.Name); return previous.Value;")]
    [TestCase("return previous.HasValue switch { true => previous.Value, false => new Construction(source.Name) };")]
    [TestCase("Construction selected = previous.HasValue switch { true => previous.Value, false => new Construction(source.Name) }; return selected;")]
    [TestCase("Construction selected = previous.HasValue ? previous.Value : new Construction(source.Name); return selected;")]
    [TestCase("Construction selected = previous.HasValue ? new Construction(previous.Value.Name) : new Construction(source.Name); return selected;")]
    public void Accepts_available_values_through_stored_guards_and_selections(string body)
    {
        var result = Run(body);
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [TestCase("Construction selected = previous.Value; if (previous.HasValue) return selected; return new(source.Name);")]
    [TestCase("Construction selected = new Construction(previous.Value.Name); if (previous.HasValue) return selected; return new(source.Name);")]
    [TestCase("var name = previous.Value.Name; return new(name);")]
    [TestCase("return new(previous.Value.Name);")]
    [TestCase("if (previous.Value.Name == source.Name) return new(source.Name); return new(source.Name);")]
    [TestCase("var available = source.Reuse; if (available) return previous.Value; return new(source.Name);")]
    public void Rejects_reads_before_an_availability_guard(string body)
    {
        var result = Run(body);
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics.Select(d => d.Id),
                Is.EqualTo(new[] { "MORPH0038" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    private static GeneratorTestDriverResult Run(string body) =>
        GeneratorTestDriver.Run("ConstructionAndMembersReview",
            Source.Replace("__BODY__", body), LanguageVersion.CSharp9);

    private const string Source =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
using Construction = Morphant.Generated.N_78409e4ae9aaeef534a067d95eb70f27.DestinationConstruction;
namespace TestCase
{
    public sealed class Source
    {
        public bool Reuse { get; set; }
        public string Name { get; set; } = "name";
    }
    public sealed class Destination
    {
        public Destination(string name) => Name = name;
        public string Name { get; set; }
    }
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>().Resolve((source, previous) =>
            {
                __BODY__
            });
    }
}
""";
}
