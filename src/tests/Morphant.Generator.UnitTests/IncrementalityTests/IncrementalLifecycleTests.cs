using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class IncrementalLifecycleTests
{
    private const string SharedConstruction =
        "Morphant.Generated.Construction.TestCase_SharedDestination.g.cs";

    private const string StableConstruction =
        "Morphant.Generated.Construction.TestCase_StableDestination.g.cs";

    private const string SharedMember =
        "Morphant.Generated.Member.TestCase_SharedDestination.g.cs";

    private const string StableMember =
        "Morphant.Generated.Member.TestCase_StableDestination.g.cs";

    private const string MappingOne =
        "Morphant.Generated.MappingExtension." +
        "TestCase_SourceOne__TestCase_SharedDestination.g.cs";

    private const string MappingTwo =
        "Morphant.Generated.MappingExtension." +
        "TestCase_SourceTwo__TestCase_SharedDestination.g.cs";

    private const string StableMapping =
        "Morphant.Generated.MappingExtension." +
        "TestCase_StableSource__TestCase_StableDestination.g.cs";

    private const string MemberOne =
        "Morphant.Generated.MemberExtension." +
        "TestCase_SourceOne__TestCase_SharedDestination.g.cs";

    private const string MemberTwo =
        "Morphant.Generated.MemberExtension." +
        "TestCase_SourceTwo__TestCase_SharedDestination.g.cs";

    private const string StableMemberExtension =
        "Morphant.Generated.MemberExtension." +
        "TestCase_StableSource__TestCase_StableDestination.g.cs";

    private const string MapperOne =
        "Morphant.Generated.TypeMapper.TestCase_MapperOne.g.cs";

    private const string MapperTwo =
        "Morphant.Generated.TypeMapper.TestCase_MapperTwo.g.cs";

    private const string StableMapper =
        "Morphant.Generated.TypeMapper.TestCase_StableMapper.g.cs";

    [Test]
    public void Adds_and_removes_only_artifacts_whose_last_reason_changes()
    {
        var models = SourceFile("Models.cs", ModelsSource);
        var stable = SourceFile("StableMapper.cs", StableMapperSource);
        var first = SourceFile("MapperOne.cs", MapperOneSource);
        var second = SourceFile("MapperTwo.cs", MapperTwoSource);
        var initialHints = new[]
        {
            SharedConstruction,
            StableConstruction,
            SharedMember,
            StableMember,
            MappingOne,
            StableMapping,
            MemberOne,
            StableMemberExtension,
            MapperOne,
            StableMapper
        };
        var bothHints = initialHints
            .Append(MappingTwo)
            .Append(MemberTwo)
            .Append(MapperTwo)
            .ToArray();
        var stableHints = new[]
        {
            StableConstruction,
            StableMember,
            StableMapping,
            StableMemberExtension,
            StableMapper
        };

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            Step(
                "first shared usage",
                [models, stable, first],
                initialHints,
                Stage(
                    "BuildConstructionPlanRequests",
                    Expected(
                        SharedConstruction,
                        IncrementalStepRunReason.New),
                    Expected(
                        StableConstruction,
                        IncrementalStepRunReason.New)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(MapperOne, IncrementalStepRunReason.New),
                    Expected(StableMapper, IncrementalStepRunReason.New))),
            Step(
                "second shared usage added",
                [models, stable, first, second],
                bothHints,
                Stage(
                    "BuildConstructionPlanRequests",
                    Expected(
                        SharedConstruction,
                        IncrementalStepRunReason.Cached),
                    Expected(
                        StableConstruction,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMemberPlanRequests",
                    Expected(
                        SharedMember,
                        IncrementalStepRunReason.Cached),
                    Expected(
                        StableMember,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMappingExtensionRequests",
                    Expected(MappingOne, IncrementalStepRunReason.Cached),
                    Expected(MappingTwo, IncrementalStepRunReason.New),
                    Expected(
                        StableMapping,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMemberExtensionRequests",
                    Expected(MemberOne, IncrementalStepRunReason.Cached),
                    Expected(MemberTwo, IncrementalStepRunReason.New),
                    Expected(
                        StableMemberExtension,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(MapperOne, IncrementalStepRunReason.Cached),
                    Expected(MapperTwo, IncrementalStepRunReason.New),
                    Expected(StableMapper, IncrementalStepRunReason.Cached))),
            Step(
                "second shared usage removed",
                [models, stable, first],
                initialHints,
                Stage(
                    "BuildConstructionPlanRequests",
                    Expected(
                        SharedConstruction,
                        IncrementalStepRunReason.Cached),
                    Expected(
                        StableConstruction,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMemberPlanRequests",
                    Expected(
                        SharedMember,
                        IncrementalStepRunReason.Cached),
                    Expected(
                        StableMember,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMappingExtensionRequests",
                    Expected(MappingOne, IncrementalStepRunReason.Cached),
                    Expected(MappingTwo, IncrementalStepRunReason.Removed),
                    Expected(
                        StableMapping,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMemberExtensionRequests",
                    Expected(MemberOne, IncrementalStepRunReason.Cached),
                    Expected(MemberTwo, IncrementalStepRunReason.Removed),
                    Expected(
                        StableMemberExtension,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(MapperOne, IncrementalStepRunReason.Cached),
                    Expected(MapperTwo, IncrementalStepRunReason.Removed),
                    Expected(StableMapper, IncrementalStepRunReason.Cached))),
            Step(
                "last shared usage removed",
                [models, stable],
                stableHints,
                Stage(
                    "BuildConstructionPlanRequests",
                    Expected(
                        SharedConstruction,
                        IncrementalStepRunReason.Removed),
                    Expected(
                        StableConstruction,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMemberPlanRequests",
                    Expected(
                        SharedMember,
                        IncrementalStepRunReason.Removed),
                    Expected(
                        StableMember,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMappingExtensionRequests",
                    Expected(MappingOne, IncrementalStepRunReason.Removed),
                    Expected(
                        StableMapping,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMemberExtensionRequests",
                    Expected(MemberOne, IncrementalStepRunReason.Removed),
                    Expected(
                        StableMemberExtension,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(MapperOne, IncrementalStepRunReason.Removed),
                    Expected(StableMapper, IncrementalStepRunReason.Cached))));
    }

    [Test]
    public void Preserves_shared_surfaces_when_the_canonical_owner_is_removed()
    {
        var models = SourceFile("Models.cs", ModelsSource);
        var stable = SourceFile("StableMapper.cs", StableMapperSource);
        var first = SourceFile("MapperOne.cs", MapperOneSource);
        var second = SourceFile("MapperTwo.cs", MapperTwoSource);
        var sharedHints = new[]
        {
            SharedConstruction,
            StableConstruction,
            SharedMember,
            StableMember,
            MappingOne,
            MappingTwo,
            StableMapping,
            MemberOne,
            MemberTwo,
            StableMemberExtension,
            MapperOne,
            MapperTwo,
            StableMapper
        };
        var remainingHints = new[]
        {
            SharedConstruction,
            StableConstruction,
            SharedMember,
            StableMember,
            MappingTwo,
            StableMapping,
            MemberTwo,
            StableMemberExtension,
            MapperTwo,
            StableMapper
        };

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            Step(
                "two shared surface owners",
                [models, stable, first, second],
                sharedHints),
            Step(
                "canonical surface owner removed",
                [models, stable, second],
                remainingHints));
    }

    // lang=c#
    private const string ModelsSource =
"""
#nullable enable
#pragma warning disable CS1591

namespace TestCase
{
    public sealed class SourceOne
    {
        public int Value { get; init; }
    }

    public sealed class SourceTwo
    {
        public int Value { get; init; }
    }

    public sealed class SharedDestination
    {
        public int Value { get; set; }
    }

    public sealed class StableSource
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed class StableDestination
    {
        public string Name { get; set; } = string.Empty;
    }
}
""";

    // lang=c#
    private const string MapperOneSource =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class MapperOne : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<SourceOne, SharedDestination>();
    }
}
""";

    // lang=c#
    private const string MapperTwoSource =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class MapperTwo : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<SourceTwo, SharedDestination>();
    }
}
""";

    // lang=c#
    private const string StableMapperSource =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class StableMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<StableSource, StableDestination>();
    }
}
""";
}
