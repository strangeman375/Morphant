using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class IncrementalLifecycleTests
{
    private const string SharedConstruction =
        "Morphant.Generated.Construction.SharedDestination__e9ed0e69d5eda29f3d88335d09cc30a0.g.cs";

    private const string StableConstruction =
        "Morphant.Generated.Construction.StableDestination__eabf1949ab4bdd337885a7845c47d3bb.g.cs";

    private const string SharedMember =
        "Morphant.Generated.Member.SharedDestination__e9ed0e69d5eda29f3d88335d09cc30a0.g.cs";

    private const string StableMember =
        "Morphant.Generated.Member.StableDestination__eabf1949ab4bdd337885a7845c47d3bb.g.cs";

    private const string MappingOne =
        "Morphant.Generated.MappingExtension.MapperOne.SourceOneToSharedDestination__1ccb2c9bede74b4f3b51ec3ba5898c76.g.cs";

    private const string MappingTwo =
        "Morphant.Generated.MappingExtension.MapperTwo.SourceTwoToSharedDestination__b8e49fee3a4d12d7ace074f802206f74.g.cs";

    private const string StableMapping =
        "Morphant.Generated.MappingExtension.StableMapper.StableSourceToStableDestination__bb75ddcff787fbc895e34e4dd60f1943.g.cs";

    private const string MemberOne =
        "Morphant.Generated.MemberExtension.MapperOne.SourceOneToSharedDestination__1ccb2c9bede74b4f3b51ec3ba5898c76.g.cs";

    private const string MemberTwo =
        "Morphant.Generated.MemberExtension.MapperTwo.SourceTwoToSharedDestination__b8e49fee3a4d12d7ace074f802206f74.g.cs";

    private const string StableMemberExtension =
        "Morphant.Generated.MemberExtension.StableMapper.StableSourceToStableDestination__bb75ddcff787fbc895e34e4dd60f1943.g.cs";

    private const string MapperOne =
        "Morphant.Generated.TypeMapper.MapperOne__60d70ed955273c1b91485515f14a3ca1.g.cs";

    private const string MapperTwo =
        "Morphant.Generated.TypeMapper.MapperTwo__e9b3eb56747d4f195aba3cac0d9cf6bf.g.cs";

    private const string StableMapper =
        "Morphant.Generated.TypeMapper.StableMapper__3251a87cd2d93d3948a4f1646b88e0d3.g.cs";

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
    public void Preserves_destination_plans_when_one_mapper_is_removed()
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
                "two mappers sharing destination plans",
                [models, stable, first, second],
                sharedHints),
            Step(
                "first mapper removed",
                [models, stable, second],
                remainingHints));
    }

    [Test]
    public void Keeps_existing_extensions_cached_when_nullable_mapper_is_added_and_removed()
    {
        const string construction =
            "Morphant.Generated.Construction.SurfaceDestination__08333ce1cf99bfc02b89cde9e06a0b49.g.cs";
        const string member =
            "Morphant.Generated.Member.SurfaceDestination__08333ce1cf99bfc02b89cde9e06a0b49.g.cs";
        const string nonNullableMapping =
            "Morphant.Generated.MappingExtension.NonNullableMapper.SurfaceSourceToSurfaceDestination__bc92abf3096a7bacd2adb4a04954e462.g.cs";
        const string nullableMapping =
            "Morphant.Generated.MappingExtension.NullablePresentationMapper.SurfaceSourceToSurfaceDestination__6967be8c085cdd530e9cf2d4333e8151.g.cs";
        const string nonNullableMember =
            "Morphant.Generated.MemberExtension.NonNullableMapper.SurfaceSourceToSurfaceDestination__bc92abf3096a7bacd2adb4a04954e462.g.cs";
        const string nullableMember =
            "Morphant.Generated.MemberExtension.NullablePresentationMapper.SurfaceSourceToSurfaceDestination__6967be8c085cdd530e9cf2d4333e8151.g.cs";
        const string nonNullableMapper =
            "Morphant.Generated.TypeMapper.NonNullableMapper__4d62945fd92434a26b8c7253a0724aff.g.cs";
        const string nullableMapper =
            "Morphant.Generated.TypeMapper.NullablePresentationMapper__e05424e51eebc47045ed3f7ab42fc5e5.g.cs";
        var models = SourceFile("SurfaceModels.cs", SurfaceModelsSource);
        var nonNullable = SourceFile(
            "NonNullableMapper.cs",
            NonNullableMapperSource);
        var nullable = SourceFile(
            "NullablePresentationMapper.cs",
            NullablePresentationMapperSource);
        var sharedHints = new[]
        {
            construction,
            nonNullableMapping,
            member,
            nonNullableMember,
            nonNullableMapper
        };
        var bothHints = new[]
        {
            construction,
            nonNullableMapping,
            nullableMapping,
            member,
            nonNullableMember,
            nullableMember,
            nonNullableMapper,
            nullableMapper
        };

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            Step(
                "non-nullable mapper",
                [models, nonNullable],
                sharedHints),
            Step(
                "nullable mapper scope added",
                [models, nonNullable, nullable],
                bothHints,
                Stage(
                    "BuildMappingExtensionRequests",
                    Expected(
                        nonNullableMapping,
                        IncrementalStepRunReason.Cached),
                    Expected(
                        nullableMapping,
                        IncrementalStepRunReason.New)),
                Stage(
                    "BuildMemberExtensionRequests",
                    Expected(
                        nonNullableMember,
                        IncrementalStepRunReason.Cached),
                    Expected(
                        nullableMember,
                        IncrementalStepRunReason.New))),
            Step(
                "nullable mapper scope removed",
                [models, nonNullable],
                sharedHints,
                Stage(
                    "BuildMappingExtensionRequests",
                    Expected(
                        nonNullableMapping,
                        IncrementalStepRunReason.Cached),
                    Expected(
                        nullableMapping,
                        IncrementalStepRunReason.Removed)),
                Stage(
                    "BuildMemberExtensionRequests",
                    Expected(
                        nonNullableMember,
                        IncrementalStepRunReason.Cached),
                    Expected(
                        nullableMember,
                        IncrementalStepRunReason.Removed))));
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
    public partial class MapperOne : TypeMapper<MapperOne>
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
    public partial class MapperTwo : TypeMapper<MapperTwo>
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
    public partial class StableMapper : TypeMapper<StableMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<StableSource, StableDestination>();
    }
}
""";

    // lang=c#
    private const string SurfaceModelsSource =
"""
#nullable enable
#pragma warning disable CS1591

namespace TestCase
{
    public sealed class SurfaceSource
    {
        public int Value { get; init; }
    }

    public sealed class SurfaceDestination
    {
        public int Value { get; set; }
    }
}
""";

    // lang=c#
    private const string NonNullableMapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class NonNullableMapper :
        TypeMapper<NonNullableMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<SurfaceSource, SurfaceDestination>();
    }
}
""";

    // lang=c#
    private const string NullablePresentationMapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class NullablePresentationMapper :
        TypeMapper<NullablePresentationMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<SurfaceSource?, SurfaceDestination?>();
    }
}
""";
}
