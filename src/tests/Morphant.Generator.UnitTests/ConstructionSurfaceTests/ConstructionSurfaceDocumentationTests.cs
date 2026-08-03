using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ConstructionSurfaceTests;

[TestFixture]
internal sealed class ConstructionSurfaceDocumentationTests
{
    [Test]
    public async Task Inherits_destination_documentation_and_supplies_complete_fallbacks()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

namespace TestCase
{
    /// <summary>Source documentation.</summary>
    public sealed class Source { }

    /// <summary>Destination documentation.</summary>
    public sealed class Destination
    {
        /// <summary>Creates a destination from its value.</summary>
        /// <param name="value">The initial value.</param>
        public Destination(int value) { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

        // lang=c#
        const string destinationConstructor =
"""
/// <inheritdoc cref="global::TestCase.Destination.Destination(global::System.Int32)"/>
public DestinationConstruction(global::Morphant.Members.ConstructorParameter<int> value)
{
}
""";

        var destinationCref = "global::TestCase.Destination";
        var plan = ConstructionSurfaceExpectedSource.Plan(
            "TestCase.Morphant.Generated",
            ConstructionSurfaceExpectedSource.ConstructionType(
                ConstructionSurfaceExpectedSource
                    .InheritDocumentation(destinationCref),
                "internal sealed class DestinationConstruction",
                "DestinationConstruction",
                "DestinationConstruction",
                destinationCref,
                destinationConstructor,
                "DestinationConstructionConstructorParameters"),
            ConstructionSurfaceExpectedSource.ConstructorParametersType(
                "internal sealed class DestinationConstructionConstructorParameters",
                destinationCref,
                ConstructionSurfaceExpectedSource.ConstructorParameterField(
                    "value",
                    "public global::Morphant.Members.ConstructorParameter<int> value = null!;")));
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

        await ConstructionSurfaceGeneratorTest
            .RunAndAssertAllowingCompilerWarnings(
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
    public async Task Generates_a_meaningful_plan_summary_without_destination_docs()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }
    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
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
