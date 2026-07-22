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
            "Morphant.TemplateExtensions.TestCase_Destination.g.cs";

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
            "Morphant.TemplateExtensions.System_String.g.cs";

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
            "Morphant.TemplateExtensions." +
            "System_Nullable_1_int___7d45e0b10f64f4d1.g.cs";
        const string nonNullableHintName =
            "Morphant.TemplateExtensions.System_Int32.g.cs";

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
            "Morphant.TemplateExtensions." +
            "System_Nullable_1_global__TestCase_" +
            "StructDestination___a1aeebe8bb0e4854.g.cs";
        const string nonNullableHintName =
            "Morphant.TemplateExtensions." +
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
            "Morphant.TemplateExtensions." +
            "TestCase_ChangingDestination_1_int___" +
            "e0dc5b0509e8cbce.g.cs";
        const string nullableStringHintName =
            "Morphant.TemplateExtensions." +
            "TestCase_ChangingDestination_1_string____" +
            "2040dc9137256187.g.cs";
        const string dynamicHintName =
            "Morphant.TemplateExtensions." +
            "TestCase_ChangingDestination_1_dynamic___" +
            "0a0fbd5397ddb59a.g.cs";

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
