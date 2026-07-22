using Microsoft.CodeAnalysis;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.TemplateExtensionIncrementalityTest;

namespace Morphant.Generator.UnitTests.TemplateExtensionTests.Incrementality;

[TestFixture]
internal sealed class TemplateExtensionSurfaceInvalidationTests
{
    [Test]
    public void Rebuilds_generated_reference_request_when_nullability_changes()
    {
        const string hintName =
            "Morphant.Generated.TemplateExtension.TestCase_Destination.g.cs";

        RunAndAssert(
            Step(
                "nullable generated reference",
                SingleSource(
                    BuildGeneratedReferenceSource("Destination?")),
                Expected(
                    hintName,
                    IncrementalStepRunReason.New)),
            Step(
                "non-nullable generated reference",
                SingleSource(
                    BuildGeneratedReferenceSource("Destination")),
                Expected(
                    hintName,
                    IncrementalStepRunReason.Modified)),
            Step(
                "nullable generated reference restored",
                SingleSource(
                    BuildGeneratedReferenceSource("Destination?")),
                Expected(
                    hintName,
                    IncrementalStepRunReason.Modified)));
    }

    [Test]
    public void Rebuilds_direct_reference_request_when_nullability_changes()
    {
        const string hintName =
            "Morphant.Generated.TemplateExtension.System_String.g.cs";

        RunAndAssert(
            Step(
                "nullable direct reference",
                SingleSource(BuildDirectSource("string?")),
                Expected(
                    hintName,
                    IncrementalStepRunReason.New)),
            Step(
                "non-nullable direct reference",
                SingleSource(BuildDirectSource("string")),
                Expected(
                    hintName,
                    IncrementalStepRunReason.Modified)),
            Step(
                "nullable direct reference restored",
                SingleSource(BuildDirectSource("string?")),
                Expected(
                    hintName,
                    IncrementalStepRunReason.Modified)));
    }

    [Test]
    public void Rebuilds_direct_value_request_when_nullability_changes()
    {
        const string nullableHintName =
            "Morphant.Generated.TemplateExtension." +
            "System_Nullable_1_int_.g.cs";
        const string nonNullableHintName =
            "Morphant.Generated.TemplateExtension.System_Int32.g.cs";

        RunAndAssert(
            Step(
                "nullable direct value",
                SingleSource(BuildDirectSource("int?")),
                Expected(
                    nullableHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "non-nullable direct value",
                SingleSource(BuildDirectSource("int")),
                Expected(
                    nonNullableHintName,
                    IncrementalStepRunReason.Modified)),
            Step(
                "nullable direct value restored",
                SingleSource(BuildDirectSource("int?")),
                Expected(
                    nullableHintName,
                    IncrementalStepRunReason.Modified)));
    }

    [Test]
    public void Rebuilds_generated_value_request_when_nullability_changes()
    {
        const string nullableHintName =
            "Morphant.Generated.TemplateExtension." +
            "System_Nullable_1_global__TestCase_" +
            "StructDestination_.g.cs";
        const string nonNullableHintName =
            "Morphant.Generated.TemplateExtension." +
            "TestCase_StructDestination.g.cs";

        RunAndAssert(
            Step(
                "nullable generated value",
                SingleSource(
                    BuildGeneratedValueSource("StructDestination?")),
                Expected(
                    nullableHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "non-nullable generated value",
                SingleSource(
                    BuildGeneratedValueSource("StructDestination")),
                Expected(
                    nonNullableHintName,
                    IncrementalStepRunReason.Modified)),
            Step(
                "nullable generated value restored",
                SingleSource(
                    BuildGeneratedValueSource("StructDestination?")),
                Expected(
                    nullableHintName,
                    IncrementalStepRunReason.Modified)));
    }

    [Test]
    public void Rebuilds_constructed_request_when_type_argument_changes()
    {
        const string intHintName =
            "Morphant.Generated.TemplateExtension." +
            "TestCase_ChangingDestination_1_int_.g.cs";
        const string nullableStringHintName =
            "Morphant.Generated.TemplateExtension." +
            "TestCase_ChangingDestination_1_string__.g.cs";
        const string dynamicHintName =
            "Morphant.Generated.TemplateExtension." +
            "TestCase_ChangingDestination_1_dynamic_.g.cs";

        RunAndAssert(
            Step(
                "value type argument",
                SingleSource(BuildConstructedSource("int")),
                Expected(
                    intHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "nullable reference type argument",
                SingleSource(BuildConstructedSource("string?")),
                Expected(
                    nullableStringHintName,
                    IncrementalStepRunReason.Modified)),
            Step(
                "dynamic type argument",
                SingleSource(BuildConstructedSource("dynamic")),
                Expected(
                    dynamicHintName,
                    IncrementalStepRunReason.Modified)));
    }

    private static TemplateExtensionIncrementalitySourceFile[]
        SingleSource(string source)
    {
        return new[]
        {
            SourceFile("TestCase.cs", source)
        };
    }

    private static string BuildGeneratedReferenceSource(
        string destinationUsage)
    {
        return GeneratedReferenceSourceTemplate.Replace(
            "__DESTINATION_USAGE__",
            destinationUsage);
    }

    private static string BuildDirectSource(string destinationUsage)
    {
        return DirectSourceTemplate.Replace(
            "__DESTINATION_USAGE__",
            destinationUsage);
    }

    private static string BuildGeneratedValueSource(
        string destinationUsage)
    {
        return GeneratedValueSourceTemplate.Replace(
            "__DESTINATION_USAGE__",
            destinationUsage);
    }

    private static string BuildConstructedSource(string typeArgument)
    {
        return ConstructedSourceTemplate.Replace(
            "__TYPE_ARGUMENT__",
            typeArgument);
    }

    // lang=c#
    private const string GeneratedReferenceSourceTemplate =
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

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, __DESTINATION_USAGE__>();
        }
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate;
}
""";

    // lang=c#
    private const string DirectSourceTemplate =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, __DESTINATION_USAGE__>();
        }
    }
}
""";

    // lang=c#
    private const string GeneratedValueSourceTemplate =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;

namespace TestCase
{
    public sealed class Source
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
            builder.Map<Source, __DESTINATION_USAGE__>();
        }
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record StructDestinationMorphantTemplate;
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

    public sealed class ChangingDestination<T>
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ChangingDestination<__TYPE_ARGUMENT__>>();
        }
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record ChangingDestinationMorphantTemplate<T>;
}
""";
}
