using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ConstructionSurfaceTests;

[TestFixture]
internal sealed class ConstructionSurfaceAttributeTests
{
    [Test]
    public async Task Copies_applicable_Obsolete_attributes_to_the_generated_plan()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591, CS0612, CS0618

using System;
using Morphant;

namespace TestCase
{
    public sealed class Source { }

    [Obsolete("Use CurrentDestination instead.")]
    public sealed class Destination
    {
        [Obsolete("Use the string constructor.", true)]
        public Destination(int value) { }

        public Destination(string value) { }
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
        const string destinationConstructors =
"""
/// <summary>
/// Creates a destination instance using a corresponding constructor.
/// </summary>
/// <param name="value">Configures the <c>value</c> constructor argument.</param>
[global::System.ObsoleteAttribute("Use the string constructor.", true)]
public DestinationConstruction(global::Morphant.Members.ConstructorParameter<int> value)
{
}

/// <summary>
/// Creates a destination instance using a corresponding constructor.
/// </summary>
/// <param name="value">Configures the <c>value</c> constructor argument.</param>
#nullable disable annotations
public DestinationConstruction(global::Morphant.Members.ConstructorParameter<string> value)
{
}
#nullable enable annotations
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
                "DestinationConstructionConstructorParameters",
                "[global::System.ObsoleteAttribute(\"Use CurrentDestination instead.\")]"),
            ConstructionSurfaceExpectedSource.ConstructorParametersType(
                "internal sealed class DestinationConstructionConstructorParameters",
                destinationCref,
                ConstructionSurfaceExpectedSource.ConstructorParameterField(
                    "value",
                    "public global::Morphant.Members.ConstructorParameter<int> valueInt = null!;"),
                """
#nullable disable annotations
/// <summary>
/// Configures the <c>value</c> constructor argument.
/// </summary>
public global::Morphant.Members.ConstructorParameter<string> valueString = null!;
#nullable enable annotations
"""));
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
}
