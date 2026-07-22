using Microsoft.CodeAnalysis;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.TemplateExtensionIncrementalityTest;

namespace Morphant.Generator.UnitTests.TemplateExtensionTests.Incrementality;

[TestFixture]
internal sealed class TemplateExtensionGlobalCoordinationTests
{
    private const string NullableLessPreferredHintName =
        "Morphant.Generated.TemplateExtension." +
        "TestCase_NullableDestination_1_string__.g.cs";

    private const string NullablePreferredHintName =
        "Morphant.Generated.TemplateExtension." +
        "TestCase_NullableDestination_1_string_.g.cs";

    private const string DynamicLessPreferredHintName =
        "Morphant.Generated.TemplateExtension." +
        "TestCase_DynamicDestination_1_dynamic_.g.cs";

    private const string DynamicPreferredHintName =
        "Morphant.Generated.TemplateExtension." +
        "TestCase_DynamicDestination_1_object_.g.cs";

    private const string TupleLessPreferredHintName =
        "Morphant.Generated.TemplateExtension." +
        "TestCase_TupleDestination_1__int_Id__" +
        "string_Name__.g.cs";

    private const string TuplePreferredHintName =
        "Morphant.Generated.TemplateExtension." +
        "TestCase_TupleDestination_1__int__string__.g.cs";

    private const string StableHintName =
        "Morphant.Generated.TemplateExtension.TestCase_ZStableDestination.g.cs";

    private const string UpperHintName =
        "Morphant.Generated.TemplateExtension.TestCase_Destination.g.cs";

    private const string LowerHintName =
        "Morphant.Generated.TemplateExtension.TestCase_destination.g.cs";

    private const string CollidingLowerHintName =
        "Morphant.Generated.TemplateExtension." +
        "TestCase_destination__c52cc9889f9bc467.g.cs";

    [Test]
    public void Invalidates_canonical_requests_only_when_representative_changes()
    {
        RunAndAssert(
            Step(
                "less preferred usages",
                BuildCanonicalSourceFiles(LessPreferredStatements),
                LessPreferredExpected(IncrementalStepRunReason.New)),
            Step(
                "preferred equivalents added",
                BuildCanonicalSourceFiles(AllStatements),
                PreferredExpected(
                    IncrementalStepRunReason.Modified,
                    IncrementalStepRunReason.Unchanged)),
            Step(
                "equivalent usages reordered",
                BuildCanonicalSourceFiles(ReorderedAllStatements),
                PreferredExpected(
                    IncrementalStepRunReason.Cached,
                    IncrementalStepRunReason.Cached)),
            Step(
                "non-canonical usages removed",
                BuildCanonicalSourceFiles(PreferredStatements),
                PreferredExpected(
                    IncrementalStepRunReason.Unchanged,
                    IncrementalStepRunReason.Unchanged)),
            Step(
                "non-canonical usages restored",
                BuildCanonicalSourceFiles(AllStatements),
                PreferredExpected(
                    IncrementalStepRunReason.Unchanged,
                    IncrementalStepRunReason.Unchanged)),
            Step(
                "less preferred representatives restored",
                BuildCanonicalSourceFiles(LessPreferredStatements),
                LessPreferredExpected(
                    IncrementalStepRunReason.Modified)));
    }

    [Test]
    public void Reassigns_requests_when_case_insensitive_collision_changes()
    {
        RunAndAssert(
            Step(
                "lower-case destination",
                BuildCollisionSourceFiles(
                    "builder.Map<Source, destination>();"),
                Expected(
                    LowerHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "upper-case collision added",
                BuildCollisionSourceFiles(
                    "builder.Map<Source, destination>();",
                    "builder.Map<Source, Destination>();"),
                Expected(
                    UpperHintName,
                    IncrementalStepRunReason.Modified),
                Expected(
                    CollidingLowerHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "colliding usages reordered",
                BuildCollisionSourceFiles(
                    "builder.Map<Source, Destination>();",
                    "builder.Map<Source, destination>();"),
                Expected(
                    UpperHintName,
                    IncrementalStepRunReason.Cached),
                Expected(
                    CollidingLowerHintName,
                    IncrementalStepRunReason.Cached)),
            Step(
                "lower-case destination removed",
                BuildCollisionSourceFiles(
                    "builder.Map<Source, Destination>();"),
                Expected(
                    UpperHintName,
                    IncrementalStepRunReason.Unchanged),
                Expected(
                    CollidingLowerHintName,
                    IncrementalStepRunReason.Removed)),
            Step(
                "lower-case collision restored",
                BuildCollisionSourceFiles(
                    "builder.Map<Source, Destination>();",
                    "builder.Map<Source, destination>();"),
                Expected(
                    UpperHintName,
                    IncrementalStepRunReason.Unchanged),
                Expected(
                    CollidingLowerHintName,
                    IncrementalStepRunReason.New)),
            Step(
                "upper-case destination removed",
                BuildCollisionSourceFiles(
                    "builder.Map<Source, destination>();"),
                Expected(
                    LowerHintName,
                    IncrementalStepRunReason.Modified),
                Expected(
                    CollidingLowerHintName,
                    IncrementalStepRunReason.Removed)));
    }

    private static TemplateExtensionIncrementalityExpectedOutput[]
        LessPreferredExpected(
            IncrementalStepRunReason changingReason)
    {
        return new[]
        {
            Expected(
                DynamicLessPreferredHintName,
                changingReason),
            Expected(
                NullableLessPreferredHintName,
                changingReason),
            Expected(
                TupleLessPreferredHintName,
                changingReason),
            Expected(
                StableHintName,
                changingReason == IncrementalStepRunReason.New
                    ? IncrementalStepRunReason.New
                    : IncrementalStepRunReason.Unchanged)
        };
    }

    private static TemplateExtensionIncrementalityExpectedOutput[]
        PreferredExpected(
            IncrementalStepRunReason canonicalReason,
            IncrementalStepRunReason stableReason)
    {
        return new[]
        {
            Expected(
                DynamicPreferredHintName,
                canonicalReason),
            Expected(
                NullablePreferredHintName,
                canonicalReason),
            Expected(
                TuplePreferredHintName,
                canonicalReason),
            Expected(
                StableHintName,
                stableReason)
        };
    }

    private static TemplateExtensionIncrementalitySourceFile[]
        BuildCanonicalSourceFiles(string mapStatements)
    {
        return new[]
        {
            SourceFile("Models.cs", CanonicalModelsSource),
            SourceFile(
                "Mapper.cs",
                CanonicalMapperSourceTemplate.Replace(
                    "__MAP_STATEMENTS__",
                    mapStatements))
        };
    }

    private static TemplateExtensionIncrementalitySourceFile[]
        BuildCollisionSourceFiles(params string[] mapStatements)
    {
        return new[]
        {
            SourceFile("Models.cs", CollisionModelsSource),
            SourceFile(
                "Mapper.cs",
                CollisionMapperSourceTemplate.Replace(
                    "__MAP_STATEMENTS__",
                    string.Join(
                        "\n",
                        mapStatements.Select(static statement =>
                            "            " + statement))))
        };
    }

    // lang=c#
    private const string LessPreferredStatements =
"""
            builder.Map<Source, NullableDestination<string?>>();
            builder.Map<Source, DynamicDestination<dynamic>>();
            builder.Map<Source, TupleDestination<(int Id, string Name)>>();
            builder.Map<Source, ZStableDestination>();
""";

    // lang=c#
    private const string PreferredStatements =
"""
            builder.Map<Source, DynamicDestination<object>>();
            builder.Map<Source, NullableDestination<string>>();
            builder.Map<Source, TupleDestination<(int, string)>>();
            builder.Map<Source, ZStableDestination>();
""";

    // lang=c#
    private const string AllStatements =
"""
            builder.Map<Source, TupleDestination<(int Id, string Name)>>();
            builder.Map<Source, NullableDestination<string>>();
            builder.Map<Source, DynamicDestination<dynamic>>();
            builder.Map<Source, TupleDestination<(int, string)>>();
            builder.Map<Source, NullableDestination<string?>>();
            builder.Map<Source, DynamicDestination<object>>();
            builder.Map<Source, ZStableDestination>();
""";

    // lang=c#
    private const string ReorderedAllStatements =
"""
            builder.Map<Source, ZStableDestination>();
            builder.Map<Source, DynamicDestination<object>>();
            builder.Map<Source, NullableDestination<string?>>();
            builder.Map<Source, TupleDestination<(int, string)>>();
            builder.Map<Source, DynamicDestination<dynamic>>();
            builder.Map<Source, NullableDestination<string>>();
            builder.Map<Source, TupleDestination<(int Id, string Name)>>();
""";

    // lang=c#
    private const string CanonicalModelsSource =
"""
#pragma warning disable CS1591
#nullable enable

namespace TestCase
{
    public sealed class DynamicDestination<T>
    {
    }

    public sealed class NullableDestination<T>
    {
    }

    public sealed class TupleDestination<T>
    {
    }

    public sealed class ZStableDestination
    {
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record DynamicDestinationMorphantTemplate<T>;

    internal sealed record NullableDestinationMorphantTemplate<T>;

    internal sealed record TupleDestinationMorphantTemplate<T>;

    internal sealed record ZStableDestinationMorphantTemplate;
}
""";

    // lang=c#
    private const string CanonicalMapperSourceTemplate =
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
__MAP_STATEMENTS__
        }
    }
}
""";

    // lang=c#
    private const string CollisionModelsSource =
"""
#pragma warning disable CS1591

namespace TestCase
{
    public sealed class Destination
    {
    }

    public sealed class destination
    {
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate;

    internal sealed record destinationMorphantTemplate;
}
""";

    // lang=c#
    private const string CollisionMapperSourceTemplate =
"""
#pragma warning disable CS1591

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
__MAP_STATEMENTS__
        }
    }
}
""";
}
