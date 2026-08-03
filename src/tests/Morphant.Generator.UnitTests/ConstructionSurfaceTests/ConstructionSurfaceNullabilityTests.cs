using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ConstructionSurfaceTests;

[TestFixture]
internal sealed class ConstructionSurfaceNullabilityTests
{
    [Test]
    public async Task Preserves_constructor_input_nullability_and_normalizes_mapping_roots()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System.Diagnostics.CodeAnalysis;
using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public sealed class Destination
    {
        public Destination(
            string required,
            string? optional,
            [AllowNull] string allowNull,
            [DisallowNull] string? disallowNull,
            int? nullableValue) { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source?, Destination?>()
                .Construct((Source source) => new(
                    source.ToString(),
                    null,
                    null,
                    string.Empty,
                    null))
                .Convert((source, previous, _) =>
                    previous.HasValue
                        ? previous.Value
                        : null);
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
/// <param name="required">Configures the <c>required</c> constructor argument.</param>
/// <param name="optional">Configures the <c>optional</c> constructor argument.</param>
/// <param name="allowNull">Configures the <c>allowNull</c> constructor argument.</param>
/// <param name="disallowNull">Configures the <c>disallowNull</c> constructor argument.</param>
/// <param name="nullableValue">Configures the <c>nullableValue</c> constructor argument.</param>
public DestinationConstruction(
    global::Morphant.Members.ConstructorParameter<string> @required,
    global::Morphant.Members.ConstructorParameter<string?>? optional,
    global::Morphant.Members.ConstructorParameter<string?>? allowNull,
    global::Morphant.Members.ConstructorParameter<string> disallowNull,
    global::Morphant.Members.ConstructorParameter<int?>? nullableValue)
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
                destinationConstructor,
                "DestinationConstructionConstructorParameters"),
            ConstructionSurfaceExpectedSource.ConstructorParametersType(
                "internal sealed class DestinationConstructionConstructorParameters",
                destinationCref,
                ConstructionSurfaceExpectedSource.ConstructorParameterField(
                    "required",
                    "public global::Morphant.Members.ConstructorParameter<string> @required = null!;"),
                ConstructionSurfaceExpectedSource.ConstructorParameterField(
                    "optional",
                    "public global::Morphant.Members.ConstructorParameter<string?>? optional = null!;"),
                ConstructionSurfaceExpectedSource.ConstructorParameterField(
                    "allowNull",
                    "public global::Morphant.Members.ConstructorParameter<string?>? allowNull = null!;"),
                ConstructionSurfaceExpectedSource.ConstructorParameterField(
                    "disallowNull",
                    "public global::Morphant.Members.ConstructorParameter<string> disallowNull = null!;"),
                ConstructionSurfaceExpectedSource.ConstructorParameterField(
                    "nullableValue",
                    "public global::Morphant.Members.ConstructorParameter<int?>? nullableValue = null!;")));
        var builderType =
            "global::Morphant.MapperBuilder<global::TestCase.Source?, " +
            "global::TestCase.Destination?>";
        var extension = ConstructionSurfaceExpectedSource.MappingExtension(
            builderType,
            "global::TestCase.Source",
            "global::TestCase.Source?",
            "global::TestCase.Destination?",
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
    public async Task Unwraps_nullable_value_roots_without_losing_nested_annotations()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System.Collections.Generic;
using Morphant;

namespace TestCase
{
    public struct Source<T> { }

    public struct Destination<T>
    {
        public Destination(T value) { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<List<string?>>?, Destination<List<string?>>?>()
                .Construct(_ => new(new List<string?>()))
                .Convert((source, previous, _) =>
                    previous.HasValue
                        ? previous.Value
                        : default);
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
public DestinationConstruction(global::Morphant.Members.ConstructorParameter<T> value)
{
}

/// <summary>
/// Creates a destination instance using a corresponding constructor.
/// </summary>
public DestinationConstruction()
{
}
""";

        const string destinationCref =
            "global::TestCase.Destination&lt;T&gt;";
        var plan = ConstructionSurfaceExpectedSource.Plan(
            "TestCase.Morphant.Generated",
            ConstructionSurfaceExpectedSource.ConstructionType(
                ConstructionSurfaceExpectedSource
                    .FallbackPlanDocumentation(destinationCref),
                "internal sealed class DestinationConstruction<T>",
                "DestinationConstruction",
                "DestinationConstruction<T>",
                "global::TestCase.Destination<T>",
                destinationConstructors,
                "DestinationConstructionConstructorParameters<T>"),
            ConstructionSurfaceExpectedSource.ConstructorParametersType(
                "internal sealed class DestinationConstructionConstructorParameters<T>",
                destinationCref,
                ConstructionSurfaceExpectedSource.ConstructorParameterField(
                    "value",
                    "public global::Morphant.Members.ConstructorParameter<T> value = null!;")));
        const string sourceType =
            "global::TestCase.Source<global::System.Collections.Generic.List<string?>>";
        const string destinationType =
            "global::TestCase.Destination<global::System.Collections.Generic.List<string?>>";
        var builderType =
            $"global::Morphant.MapperBuilder<{sourceType}?, {destinationType}?>";
        var extension = ConstructionSurfaceExpectedSource.MappingExtension(
            builderType,
            sourceType,
            sourceType + "?",
            destinationType + "?",
            destinationType,
            "global::TestCase.Morphant.Generated." +
            "DestinationConstruction<global::System.Collections.Generic.List<string?>>");

        await ConstructionSurfaceGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.Construction.TestCase_Destination_1.g.cs",
                plan
            ),
            (
                "Morphant.Generated.MappingExtension.System_Nullable_TestCase_Source_System_Collections_Generic_List_System_String_____System_Nullable_TestCase_Destination_System_Collections_Generic_List_System_String___.g.cs",
                extension
            ));
    }
}
