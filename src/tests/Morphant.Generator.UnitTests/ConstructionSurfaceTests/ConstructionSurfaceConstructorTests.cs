using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ConstructionSurfaceTests;

[TestFixture]
internal sealed class ConstructionSurfaceConstructorTests
{
    [Test]
    public void Mirrors_supported_constructors_as_compiler_overload_probes()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Members;

namespace TestCase
{
    public sealed class Source
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public Destination(int id, string name) { }

        public Destination(
            Guid id,
            bool enabled = true,
            params string[] tags) { }

        public Destination(ref long unsupported) { }
        public Destination(Span<int> unsupported) { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>()
                .Construct(source => new(source.Id, source.Name))
                .Construct(source => new(
                    name: source.Name,
                    id: source.Id))
                .Construct(source => new(
                    (ConstructorParameter<Guid>)Guid.NewGuid(),
                    tags: new[] { source.Name }));
        }
    }
}
""";

        var generated =
            ConstructionSurfaceCompilationTest.RunAndGetGeneratedSources(
                LanguageVersion.CSharp9,
                source);
        var plan = generated[
            "Morphant.Generated.Construction.TestCase_Destination.g.cs"];

        var intConstructor = plan.IndexOf(
            "ConstructorParameter<int> id",
            StringComparison.Ordinal);
        var guidConstructor = plan.LastIndexOf(
            "ConstructorParameter<global::System.Guid> id",
            StringComparison.Ordinal);

        Assert.That(intConstructor, Is.GreaterThanOrEqualTo(0));
        Assert.That(guidConstructor, Is.GreaterThan(intConstructor));
        Assert.That(
            plan,
            Does.Contain(
                "ConstructorParameter<bool> enabled = null!"));
        Assert.That(
            plan,
            Does.Contain(
                "ConstructorParameter<string[]> tags = null!"));
        Assert.That(
            plan,
            Does.Contain("ConstructorParameter<int> idInt"));
        Assert.That(
            plan,
            Does.Contain(
                "ConstructorParameter<global::System.Guid> idGuid"));
        Assert.That(
            plan,
            Does.Not.Contain("unsupported"));
    }

    [Test]
    public void Generates_ByConvention_overlay_and_ByFactory_for_parameterless_types()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }
    public sealed class Destination
    {
        public Destination() { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>()
                .Construct(_ => new(ByConvention()))
                .Construct(_ => new(
                    ByFactory(() => new Destination())));
        }
    }
}
""";

        var generated =
            ConstructionSurfaceCompilationTest.RunAndGetGeneratedSources(
                LanguageVersion.CSharp9,
                source);
        var plan = generated[
            "Morphant.Generated.Construction.TestCase_Destination.g.cs"];

        Assert.That(
            plan,
            Does.Contain("ByConventionMarker marker"));
        Assert.That(
            plan,
            Does.Contain(
                "IByFactoryMarker<global::TestCase.Destination> marker"));
        Assert.That(
            plan,
            Does.Not.Contain("DestinationConstructorParameters"));
    }
}
