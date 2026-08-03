using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ConstructionSurfaceTests;

[TestFixture]
internal sealed class ConstructionSurfaceConstructorTests
{
    [Test]
    public async Task Mirrors_supported_constructors_as_compiler_overload_probes()
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

        // lang=c#
        const string destinationConstructors =
"""
/// <summary>
/// Creates a destination instance using a corresponding constructor.
/// </summary>
/// <param name="id">Configures the <c>id</c> constructor argument.</param>
/// <param name="name">Configures the <c>name</c> constructor argument.</param>
public DestinationConstruction(
    global::Morphant.Members.ConstructorParameter<int> id,
    global::Morphant.Members.ConstructorParameter<string> name)
{
}

/// <summary>
/// Creates a destination instance using a corresponding constructor.
/// </summary>
/// <param name="id">Configures the <c>id</c> constructor argument.</param>
/// <param name="enabled">Configures the <c>enabled</c> constructor argument. If omitted, the destination constructor default value <c>true</c> is used.</param>
/// <param name="tags">Configures the <c>tags</c> constructor argument.</param>
public DestinationConstruction(
    global::Morphant.Members.ConstructorParameter<global::System.Guid> id,
    global::Morphant.Members.ConstructorParameter<bool> enabled = null!,
    global::Morphant.Members.ConstructorParameter<string[]> tags = null!)
{
}
""";

        var destinationCref = "global::TestCase.Destination";
        var plan = ConstructionSurfaceExpectedSource.Plan(
            "TestCase.Morphant.Generated",
            ConstructionSurfaceExpectedSource.ConstructionType(
                ConstructionSurfaceExpectedSource
                    .FallbackPlanDocumentation(destinationCref),
                "internal sealed class DestinationConstruction",
                "DestinationConstruction",
                "DestinationConstruction",
                destinationCref,
                destinationConstructors,
                "DestinationConstructionConstructorParameters"),
            ConstructionSurfaceExpectedSource.ConstructorParametersType(
                "internal sealed class DestinationConstructionConstructorParameters",
                destinationCref,
                ConstructionSurfaceExpectedSource.ConstructorParameterField(
                    "id",
                    "public global::Morphant.Members.ConstructorParameter<int> idInt = null!;"),
                ConstructionSurfaceExpectedSource.ConstructorParameterField(
                    "name",
                    "public global::Morphant.Members.ConstructorParameter<string> name = null!;"),
                ConstructionSurfaceExpectedSource.ConstructorParameterField(
                    "id",
                    "public global::Morphant.Members.ConstructorParameter<global::System.Guid> idGuid = null!;"),
                ConstructionSurfaceExpectedSource.ConstructorParameterField(
                    "enabled",
                    "public global::Morphant.Members.ConstructorParameter<bool> enabled = null!;"),
                ConstructionSurfaceExpectedSource.ConstructorParameterField(
                    "tags",
                    "public global::Morphant.Members.ConstructorParameter<string[]> tags = null!;")));
        var builderType =
            "global::Morphant.MapperBuilder<global::TestCase.Source, " +
            "global::TestCase.Destination>";
        var extension = ConstructionSurfaceExpectedSource.MappingExtension(
            builderType,
            "global::TestCase.Source",
            "global::TestCase.Source?",
            destinationCref,
            destinationCref,
            "global::TestCase.Morphant.Generated.DestinationConstruction");

        await ConstructionSurfaceGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.Construction.TestCase_Destination.g.cs",
                plan
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_Destination.g.cs",
                extension
            ));
    }

    [Test]
    public async Task Generates_ByConvention_overlay_and_ByFactory_for_parameterless_types()
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

        // lang=c#
        const string destinationConstructor =
"""
/// <summary>
/// Creates a destination instance using a corresponding constructor.
/// </summary>
public DestinationConstruction()
{
}
""";

        var destinationCref = "global::TestCase.Destination";
        var plan = ConstructionSurfaceExpectedSource.Plan(
            "TestCase.Morphant.Generated",
            ConstructionSurfaceExpectedSource.ConstructionType(
                ConstructionSurfaceExpectedSource
                    .FallbackPlanDocumentation(destinationCref),
                "internal sealed class DestinationConstruction",
                "DestinationConstruction",
                "DestinationConstruction",
                destinationCref,
                destinationConstructor));
        var builderType =
            "global::Morphant.MapperBuilder<global::TestCase.Source, " +
            "global::TestCase.Destination>";
        var extension = ConstructionSurfaceExpectedSource.MappingExtension(
            builderType,
            "global::TestCase.Source",
            "global::TestCase.Source?",
            destinationCref,
            destinationCref,
            "global::TestCase.Morphant.Generated.DestinationConstruction");

        await ConstructionSurfaceGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.Construction.TestCase_Destination.g.cs",
                plan
            ),
            (
                "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_Destination.g.cs",
                extension
            ));
    }
}
