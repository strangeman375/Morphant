using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.TemplateExtensionActualizationTest;

namespace Morphant.Generator.UnitTests.TemplateExtensionTests.Actualization;

[TestFixture]
internal sealed class TemplateExtensionContentActualizationTests
{
    [Test]
    public void Updates_generated_and_direct_extensions_when_top_level_nullability_changes()
    {
        const string destination =
            "global::TestCase.Destination";
        const string template =
            "global::TestCase.Morphant.Generated." +
            "DestinationMorphantTemplate";

        var nullableGenerated =
            ExpectedGeneratedReferenceExtension(
                "TestCase_Destination",
                destination + "?",
                template + "?",
                destination + "?");

        var nonNullableGenerated =
            ExpectedGeneratedReferenceExtension(
                "TestCase_Destination",
                destination,
                template);

        var nullableDirect = ExpectedDirectExtension(
            "System_String",
            "string?",
            "string?");

        var nonNullableDirect = ExpectedDirectExtension(
            "System_String",
            "string",
            "string?");

        var nullableDirectValue = ExpectedDirectExtension(
            "System_Nullable_1_int_",
            "int?",
            "int?");

        var nonNullableDirectValue = ExpectedDirectExtension(
            "System_Int32",
            "int",
            "int");

        var nullableGeneratedValue =
            ExpectedGeneratedValueExtension(
                "System_Nullable_1_global__TestCase_" +
                "StructDestination_",
                "global::TestCase.StructDestination?",
                "global::TestCase.Morphant.Generated." +
                "StructDestinationMorphantTemplate?");

        var nonNullableGeneratedValue =
            ExpectedGeneratedValueExtension(
                "TestCase_StructDestination",
                "global::TestCase.StructDestination",
                "global::TestCase.Morphant.Generated." +
                "StructDestinationMorphantTemplate");

        RunAndAssert(
            Step(
                "all destinations nullable",
                BuildNullabilitySource(
                    "Destination?",
                    "string?",
                    "int?",
                    "StructDestination?"),
                nullableGenerated,
                nullableDirect,
                nullableDirectValue,
                nullableGeneratedValue),
            Step(
                "generated reference destination becomes non-nullable",
                BuildNullabilitySource(
                    "Destination",
                    "string?",
                    "int?",
                    "StructDestination?"),
                nonNullableGenerated,
                nullableDirect,
                nullableDirectValue,
                nullableGeneratedValue),
            Step(
                "direct reference destination becomes non-nullable",
                BuildNullabilitySource(
                    "Destination",
                    "string",
                    "int?",
                    "StructDestination?"),
                nonNullableGenerated,
                nonNullableDirect,
                nullableDirectValue,
                nullableGeneratedValue),
            Step(
                "direct value destination becomes non-nullable",
                BuildNullabilitySource(
                    "Destination",
                    "string",
                    "int",
                    "StructDestination?"),
                nonNullableGenerated,
                nonNullableDirect,
                nonNullableDirectValue,
                nullableGeneratedValue),
            Step(
                "generated value destination becomes non-nullable",
                BuildNullabilitySource(
                    "Destination",
                    "string",
                    "int",
                    "StructDestination"),
                nonNullableGenerated,
                nonNullableDirect,
                nonNullableDirectValue,
                nonNullableGeneratedValue),
            Step(
                "all nullable surfaces restored",
                BuildNullabilitySource(
                    "Destination?",
                    "string?",
                    "int?",
                    "StructDestination?"),
                nullableGenerated,
                nullableDirect,
                nullableDirectValue,
                nullableGeneratedValue));
    }

    [Test]
    public void Updates_generated_extension_when_destination_changes_between_reference_and_value_type()
    {
        const string destination =
            "global::TestCase.Destination";
        const string template =
            "global::TestCase.Morphant.Generated." +
            "DestinationMorphantTemplate";

        var referenceExpected = ExpectedGeneratedReferenceExtension(
            "TestCase_Destination",
            destination,
            template);

        var valueExpected = ExpectedGeneratedValueExtension(
            "TestCase_Destination",
            destination,
            template);

        RunAndAssert(
            Step(
                "reference destination",
                BuildDestinationTypeKindSource(
                    "public sealed class Destination"),
                referenceExpected),
            Step(
                "destination becomes a value type",
                BuildDestinationTypeKindSource(
                    "public struct Destination"),
                valueExpected),
            Step(
                "reference destination restored",
                BuildDestinationTypeKindSource(
                    "public sealed class Destination"),
                referenceExpected));
    }

    [Test]
    public void Replaces_changed_constructed_extension_and_preserves_unaffected_extension()
    {
        var stable = ExpectedGeneratedReferenceExtension(
            "TestCase_StableDestination",
            "global::TestCase.StableDestination",
            "global::TestCase.Morphant.Generated." +
            "StableDestinationMorphantTemplate");

        var intConstructed = ExpectedConstructedExtension("int");
        var nullableStringConstructed =
            ExpectedConstructedExtension("string?");
        var dynamicConstructed =
            ExpectedConstructedExtension("dynamic");

        RunAndAssert(
            Step(
                "int constructed destination",
                BuildConstructedSource("int"),
                stable,
                intConstructed),
            Step(
                "nullable reference type argument",
                BuildConstructedSource("string?"),
                stable,
                nullableStringConstructed),
            Step(
                "dynamic type argument",
                BuildConstructedSource("dynamic"),
                stable,
                dynamicConstructed),
            Step(
                "constructed usage removed",
                BuildConstructedSource(null),
                stable));
    }

    [Test]
    public void Changes_canonical_representation_as_equivalent_usages_appear_and_disappear()
    {
        var lessPreferred = new[]
        {
            ExpectedConstructedExtension(
                "NullableDestination",
                "string?"),
            ExpectedConstructedExtension(
                "DynamicDestination",
                "dynamic"),
            ExpectedConstructedExtension(
                "TupleDestination",
                "(int Id, string Name)")
        };

        var preferred = new[]
        {
            ExpectedConstructedExtension(
                "NullableDestination",
                "string"),
            ExpectedConstructedExtension(
                "DynamicDestination",
                "object"),
            ExpectedConstructedExtension(
                "TupleDestination",
                "(int, string)")
        };

        // lang=c#
        const string lessPreferredStatements =
"""
            builder.Map<Source, NullableDestination<string?>>();
            builder.Map<Source, DynamicDestination<dynamic>>();
            builder.Map<Source, TupleDestination<(int Id, string Name)>>();
""";

        // lang=c#
        const string allStatements =
"""
            builder.Map<Source, TupleDestination<(int Id, string Name)>>();
            builder.Map<Source, NullableDestination<string>>();
            builder.Map<Source, DynamicDestination<dynamic>>();
            builder.Map<Source, TupleDestination<(int, string)>>();
            builder.Map<Source, NullableDestination<string?>>();
            builder.Map<Source, DynamicDestination<object>>();
""";

        // lang=c#
        const string reorderedStatements =
"""
            builder.Map<Source, DynamicDestination<object>>();
            builder.Map<Source, NullableDestination<string?>>();
            builder.Map<Source, TupleDestination<(int, string)>>();
            builder.Map<Source, DynamicDestination<dynamic>>();
            builder.Map<Source, NullableDestination<string>>();
            builder.Map<Source, TupleDestination<(int Id, string Name)>>();
""";

        RunAndAssert(
            Step(
                "less preferred usages only",
                BuildEquivalentSource(lessPreferredStatements),
                lessPreferred),
            Step(
                "preferred equivalents added",
                BuildEquivalentSource(allStatements),
                preferred),
            Step(
                "equivalent usages reordered",
                BuildEquivalentSource(reorderedStatements),
                preferred),
            Step(
                "preferred equivalents removed",
                BuildEquivalentSource(lessPreferredStatements),
                lessPreferred));
    }

    [Test]
    public void Reassigns_hint_names_as_case_insensitive_collisions_appear_and_disappear()
    {
        var lowerWithoutCollision =
            ExpectedGeneratedReferenceExtension(
                "TestCase_destination",
                "global::TestCase.destination",
                "global::TestCase.Morphant.Generated." +
                "destinationMorphantTemplate");

        var upperWithCollision =
            ExpectedGeneratedReferenceExtension(
                "TestCase_Destination",
                "global::TestCase.Destination",
                "global::TestCase.Morphant.Generated." +
                "DestinationMorphantTemplate");

        var lowerWithCollision =
            ExpectedGeneratedReferenceExtension(
                "TestCase_destination__c52cc9889f9bc467",
                "global::TestCase.destination",
                "global::TestCase.Morphant.Generated." +
                "destinationMorphantTemplate");

        RunAndAssert(
            Step(
                "lower-case destination only",
                BuildCollisionSource(
                    "            builder.Map<Source, destination>();"),
                lowerWithoutCollision),
            Step(
                "case-insensitive collision added",
                BuildCollisionSource(
                    "            builder.Map<Source, destination>();\n" +
                    "            builder.Map<Source, Destination>();"),
                upperWithCollision,
                lowerWithCollision),
            Step(
                "colliding usages reordered",
                BuildCollisionSource(
                    "            builder.Map<Source, Destination>();\n" +
                    "            builder.Map<Source, destination>();"),
                upperWithCollision,
                lowerWithCollision),
            Step(
                "upper-case collision removed",
                BuildCollisionSource(
                    "            builder.Map<Source, destination>();"),
                lowerWithoutCollision));
    }

    [Test]
    public void Keeps_extension_unchanged_when_irrelevant_inputs_change()
    {
        var expected = ExpectedGeneratedReferenceExtension(
            "TestCase_Destination",
            "global::TestCase.Destination",
            "global::TestCase.Morphant.Generated." +
            "DestinationMorphantTemplate");

        RunAndAssert(
            Step(
                "baseline",
                BuildIrrelevantChangeSource(
                    "Source",
                    "",
                    "        public int Id { get; set; }",
                    ";"),
                expected),
            Step(
                "source and destination members changed",
                BuildIrrelevantChangeSource(
                    "AlternativeSource",
                    "MappingMode.MapNew",
                    "        public Destination()\n" +
                    "        {\n" +
                    "        }\n\n" +
                    "        public string Name { get; init; } = \"\";",
                    "\n    {\n" +
                    "        public int Version { get; init; }\n" +
                    "    }"),
                expected),
            Step(
                "mapping mode and template stub changed again",
                BuildIrrelevantChangeSource(
                    "Source",
                    "MappingMode.MapExisting",
                    "        public decimal Amount { get; set; }",
                    "\n    {\n" +
                    "        public string Marker => string.Empty;\n" +
                    "    }"),
                expected));
    }

    [Test]
    public void Keeps_extension_documentation_unchanged_when_destination_documentation_changes()
    {
        var generatedExpected = ExpectedGeneratedReferenceExtension(
            "TestCase_Destination",
            "global::TestCase.Destination",
            "global::TestCase.Morphant.Generated." +
            "DestinationMorphantTemplate");

        var directExpected = ExpectedDirectExtension(
            "TestCase_DirectDestination",
            "global::TestCase.DirectDestination",
            "global::TestCase.DirectDestination");

        // lang=c#
        const string initialDocumentation =
"""
    /// <summary>
    /// Represents the initial destination.
    /// </summary>

""";

        // lang=c#
        const string editedDocumentation =
"""
    /// <summary>
    /// Represents the edited destination.
    /// </summary>
    /// <remarks>
    /// This destination-specific text must not appear on Template methods.
    /// </remarks>

""";

        RunAndAssert(
            Step(
                "undocumented destination",
                BuildDestinationDocumentationSource(string.Empty),
                generatedExpected,
                directExpected),
            Step(
                "destination documentation added",
                BuildDestinationDocumentationSource(
                    initialDocumentation),
                generatedExpected,
                directExpected),
            Step(
                "destination documentation edited",
                BuildDestinationDocumentationSource(
                    editedDocumentation),
                generatedExpected,
                directExpected),
            Step(
                "destination documentation removed",
                BuildDestinationDocumentationSource(string.Empty),
                generatedExpected,
                directExpected));
    }

    private static (string HintName, string Source)
        ExpectedConstructedExtension(string typeArgument)
    {
        return ExpectedConstructedExtension(
            "ChangingDestination",
            typeArgument);
    }

    private static (string HintName, string Source)
        ExpectedConstructedExtension(
            string destinationName,
            string typeArgument)
    {
        var hintNamePart = (destinationName, typeArgument) switch
        {
            ("ChangingDestination", "int") =>
                "TestCase_ChangingDestination_1_int_",
            ("ChangingDestination", "string?") =>
                "TestCase_ChangingDestination_1_string__",
            ("ChangingDestination", "dynamic") =>
                "TestCase_ChangingDestination_1_dynamic_",
            ("NullableDestination", "string?") =>
                "TestCase_NullableDestination_1_string__",
            ("NullableDestination", "string") =>
                "TestCase_NullableDestination_1_string_",
            ("DynamicDestination", "dynamic") =>
                "TestCase_DynamicDestination_1_dynamic_",
            ("DynamicDestination", "object") =>
                "TestCase_DynamicDestination_1_object_",
            ("TupleDestination", "(int Id, string Name)") =>
                "TestCase_TupleDestination_1__int_Id__" +
                "string_Name__",
            ("TupleDestination", "(int, string)") =>
                "TestCase_TupleDestination_1__int__string__",
            _ => throw new ArgumentOutOfRangeException(
                nameof(typeArgument),
                typeArgument,
                $"Unexpected destination '{destinationName}'.")
        };

        var destinationType =
            $"global::TestCase.{destinationName}<{typeArgument}>";
        var templateType =
            "global::TestCase.Morphant.Generated." +
            $"{destinationName}MorphantTemplate<{typeArgument}>";

        return ExpectedGeneratedReferenceExtension(
            hintNamePart,
            destinationType,
            templateType);
    }

    private static string BuildNullabilitySource(
        string generatedDestination,
        string directReferenceDestination,
        string directValueDestination,
        string generatedValueDestination)
    {
        return NullabilitySourceTemplate
            .Replace(
                "__GENERATED_DESTINATION__",
                generatedDestination)
            .Replace(
                "__DIRECT_REFERENCE_DESTINATION__",
                directReferenceDestination)
            .Replace(
                "__DIRECT_VALUE_DESTINATION__",
                directValueDestination)
            .Replace(
                "__GENERATED_VALUE_DESTINATION__",
                generatedValueDestination);
    }

    private static string BuildDestinationTypeKindSource(
        string destinationDeclaration)
    {
        return DestinationTypeKindSourceTemplate.Replace(
            "__DESTINATION_DECLARATION__",
            destinationDeclaration);
    }

    private static string BuildConstructedSource(
        string? typeArgument)
    {
        var changingMap = typeArgument is null
            ? string.Empty
            : "            builder.Map<Source, " +
              $"ChangingDestination<{typeArgument}>>();";

        return ConstructedSourceTemplate.Replace(
            "__CHANGING_MAP__",
            changingMap);
    }

    private static string BuildEquivalentSource(string mapStatements)
    {
        return EquivalentSourceTemplate.Replace(
            "__MAP_STATEMENTS__",
            mapStatements);
    }

    private static string BuildCollisionSource(string mapStatements)
    {
        return CollisionSourceTemplate.Replace(
            "__MAP_STATEMENTS__",
            mapStatements);
    }

    private static string BuildIrrelevantChangeSource(
        string sourceType,
        string mappingModeArgument,
        string destinationBody,
        string templateBody)
    {
        return IrrelevantChangeSourceTemplate
            .Replace("__SOURCE_TYPE__", sourceType)
            .Replace(
                "__MAPPING_MODE_ARGUMENT__",
                mappingModeArgument)
            .Replace("__DESTINATION_BODY__", destinationBody)
            .Replace("__TEMPLATE_BODY__", templateBody);
    }

    private static string BuildDestinationDocumentationSource(
        string destinationDocumentation)
    {
        return DestinationDocumentationSourceTemplate.Replace(
            "__DESTINATION_DOCUMENTATION__",
            destinationDocumentation);
    }

    // lang=c#
    private const string NullabilitySourceTemplate =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    public sealed class Destination
    {
    }

    public struct StructDestination
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, __GENERATED_DESTINATION__>();
            builder.Map<Source, __DIRECT_REFERENCE_DESTINATION__>();
            builder.Map<Source, __DIRECT_VALUE_DESTINATION__>();
            builder.Map<Source, __GENERATED_VALUE_DESTINATION__>();
        }
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate;

    internal sealed record StructDestinationMorphantTemplate;
}
""";

    // lang=c#
    private const string DestinationTypeKindSourceTemplate =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    __DESTINATION_DECLARATION__
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
        }
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate;
}
""";

    // lang=c#
    private const string ConstructedSourceTemplate =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    public sealed class StableDestination
    {
    }

    public sealed class ChangingDestination<T>
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, StableDestination>();
__CHANGING_MAP__
        }
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record StableDestinationMorphantTemplate;

    internal sealed record ChangingDestinationMorphantTemplate<T>;
}
""";

    // lang=c#
    private const string EquivalentSourceTemplate =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    public sealed class NullableDestination<T>
    {
    }

    public sealed class DynamicDestination<T>
    {
    }

    public sealed class TupleDestination<T>
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
__MAP_STATEMENTS__
        }
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record NullableDestinationMorphantTemplate<T>;

    internal sealed record DynamicDestinationMorphantTemplate<T>;

    internal sealed record TupleDestinationMorphantTemplate<T>;
}
""";

    // lang=c#
    private const string CollisionSourceTemplate =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    public sealed class Destination
    {
    }

    public sealed class destination
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
__MAP_STATEMENTS__
        }
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate;

    internal sealed record destinationMorphantTemplate;
}
""";

    // lang=c#
    private const string IrrelevantChangeSourceTemplate =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    public sealed class AlternativeSource
    {
    }

    public sealed class Destination
    {
__DESTINATION_BODY__
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<__SOURCE_TYPE__, Destination>(__MAPPING_MODE_ARGUMENT__);
        }
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate__TEMPLATE_BODY__
}
""";

    // lang=c#
    private const string DestinationDocumentationSourceTemplate =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

__DESTINATION_DOCUMENTATION__    public sealed class Destination
    {
    }

__DESTINATION_DOCUMENTATION__    public enum DirectDestination
    {
        None
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
            builder.Map<Source, DirectDestination>();
        }
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate;
}
""";
}
