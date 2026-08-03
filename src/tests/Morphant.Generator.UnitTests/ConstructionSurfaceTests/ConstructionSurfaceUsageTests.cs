using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ConstructionSurfaceTests;

[TestFixture]
internal sealed class ConstructionSurfaceUsageTests
{
    [Test]
    public async Task Resolves_every_structured_construction_form_and_manual_mapping()
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
    }

    public sealed class Destination
    {
        public Destination(int id) { }

        public Destination(
            Guid id,
            bool enabled = true,
            params string[] tags) { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source?, Destination?>()
                .Construct(source => new(source.Id))
                .Construct((source, previous) =>
                {
                    if (previous.HasValue)
                        return previous;

                    return new(
                            ByConvention(),
                            new()
                            {
                                idInt = source.Id,
                                enabled = Auto(),
                                tags = new[] { "generated" }
                            });
                })
                .Construct(source => new(
                    (ConstructorParameter<Guid>)Guid.NewGuid(),
                    tags: Array.Empty<string>()))
                .Construct(source => new(
                    ByFactory(() => new Destination(source.Id))))
                .Convert((source, previous, context) =>
                    previous.HasValue
                        ? previous.Value
                        : new Destination(source!.Id));
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
public DestinationConstruction(global::Morphant.Members.ConstructorParameter<int> id)
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

        var destinationType = "global::TestCase.Destination";
        var plan = ConstructionSurfaceExpectedSource.Plan(
            "TestCase.Morphant.Generated",
            ConstructionSurfaceExpectedSource.ConstructionType(
                ConstructionSurfaceExpectedSource
                    .FallbackPlanDocumentation(destinationType),
                "internal sealed class DestinationConstruction",
                "DestinationConstruction",
                "DestinationConstruction",
                destinationType,
                destinationConstructors,
                "DestinationConstructionConstructorParameters"),
            ConstructionSurfaceExpectedSource.ConstructorParametersType(
                "internal sealed class DestinationConstructionConstructorParameters",
                destinationType,
                ConstructionSurfaceExpectedSource.ConstructorParameterField(
                    "id",
                    "public global::Morphant.Members.ConstructorParameter<int> idInt = null!;"),
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
            "global::Morphant.MapperBuilder<global::TestCase.Source?, " +
            "global::TestCase.Destination?>";
        var extension = ConstructionSurfaceExpectedSource.MappingExtension(
            builderType,
            "global::TestCase.Source",
            "global::TestCase.Source?",
            destinationType + "?",
            destinationType,
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
    public async Task Resolves_direct_method_groups_and_previous_aware_replacement()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<string?, Guid>()
                .Construct(Guid.Parse)
                .Construct((source, previous) =>
                    previous.HasValue
                        ? previous.Value
                        : Guid.Parse(source))
                .Convert((source, previous, _) =>
                    source is null
                        ? previous.HasValue
                            ? previous.Value
                            : Guid.Empty
                        : Guid.Parse(source));
        }
    }
}
""";

        var builderType =
            "global::Morphant.MapperBuilder<string?, global::System.Guid>";
        var extension = ConstructionSurfaceExpectedSource.MappingExtension(
            builderType,
            "string",
            "string?",
            "global::System.Guid",
            "global::System.Guid",
            "global::System.Guid");

        await ConstructionSurfaceGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.MappingExtension.System_String__System_Guid.g.cs",
                extension
            ));
    }
}
