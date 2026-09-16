using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class GlobalCoordinationTests
{
    private const string UpperConstruction =
        "Morphant.Generated.Construction.URL__4a82eb081f4a7a56b8896decd859114c.g.cs";

    private const string TitleConstruction =
        "Morphant.Generated.Construction.Url__d4a5140116a516605b9bfbf106e2cfd0.g.cs";

    private const string StableConstruction =
        "Morphant.Generated.Construction.StableDestination__eabf1949ab4bdd337885a7845c47d3bb.g.cs";

    private const string UpperMapping =
        "Morphant.Generated.MappingExtension.CollisionMapper.SourceToURL__80880e30e04f7546af678998151bbecb.g.cs";

    private const string TitleMapping =
        "Morphant.Generated.MappingExtension.CollisionMapper.SourceToUrl__2460e2b8dd3b03db2a1ce0f542feaf31.g.cs";

    private const string StableMapping =
        "Morphant.Generated.MappingExtension.StableMapper.StableSourceToStableDestination__bb75ddcff787fbc895e34e4dd60f1943.g.cs";

    private const string UpperMember =
        "Morphant.Generated.Member.URL__4a82eb081f4a7a56b8896decd859114c.g.cs";

    private const string TitleMember =
        "Morphant.Generated.Member.Url__d4a5140116a516605b9bfbf106e2cfd0.g.cs";

    private const string StableMember =
        "Morphant.Generated.Member.StableDestination__eabf1949ab4bdd337885a7845c47d3bb.g.cs";

    private const string UpperMemberExtension =
        "Morphant.Generated.MemberExtension.CollisionMapper.SourceToURL__80880e30e04f7546af678998151bbecb.g.cs";

    private const string TitleMemberExtension =
        "Morphant.Generated.MemberExtension.CollisionMapper.SourceToUrl__2460e2b8dd3b03db2a1ce0f542feaf31.g.cs";

    private const string StableMemberExtension =
        "Morphant.Generated.MemberExtension.StableMapper.StableSourceToStableDestination__bb75ddcff787fbc895e34e4dd60f1943.g.cs";

    private const string CollisionMapper =
        "Morphant.Generated.TypeMapper.CollisionMapper__ff86deae24b179203f68a6b360ced567.g.cs";

    private const string StableMapper =
        "Morphant.Generated.TypeMapper.StableMapper__3251a87cd2d93d3948a4f1646b88e0d3.g.cs";

    [Test]
    public void Adding_an_unrelated_mapping_keeps_destination_dependencies_cached()
    {
        var models = SourceFile("Models.cs", SurfaceModelsSource);
        var stable = SourceFile("StableMapper.cs", StableMapperSource);
        var added = SourceFile("CollisionMapper.cs", BuildCollisionMapper(
            includeUpperCase: true, includeTitleCase: false));
        var initialHints = new[]
        {
            StableConstruction, StableMapping, StableMember,
            StableMemberExtension, StableMapper
        };
        var addedHints = new[]
        {
            StableConstruction, StableMapping, StableMember,
            StableMemberExtension, StableMapper,
            UpperConstruction, UpperMapping, UpperMember,
            UpperMemberExtension, CollisionMapper
        };

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            Step("initial mapper", [models, stable], initialHints),
            Step(
                "unrelated destination added",
                [models, stable, added],
                addedHints,
                Stage("BuildConstructionPlanModelInputs",
                    Expected(StableConstruction, IncrementalStepRunReason.Cached),
                    Expected(UpperConstruction, IncrementalStepRunReason.New)),
                Stage("BuildMemberPlanModelInputs",
                    Expected(StableMember, IncrementalStepRunReason.Cached),
                    Expected(UpperMember, IncrementalStepRunReason.New))),
            Step(
                "unrelated destination removed",
                [models, stable],
                initialHints,
                Stage("BuildConstructionPlanModelInputs",
                    Expected(StableConstruction, IncrementalStepRunReason.Cached),
                    Expected(UpperConstruction, IncrementalStepRunReason.Removed)),
                Stage("BuildMemberPlanModelInputs",
                    Expected(StableMember, IncrementalStepRunReason.Cached),
                    Expected(UpperMember, IncrementalStepRunReason.Removed))));
    }

    [Test]
    public void Adding_and_removing_similar_surface_labels_preserves_existing_files()
    {
        var models = SourceFile("Models.cs", SurfaceModelsSource);
        var stable = SourceFile("StableMapper.cs", StableMapperSource);
        var upper = SourceFile(
            "CollisionMapper.cs",
            BuildCollisionMapper(
                includeUpperCase: true,
                includeTitleCase: false));
        var both = SourceFile(
            "CollisionMapper.cs",
            BuildCollisionMapper(
                includeUpperCase: true,
                includeTitleCase: true));
        var initialHints = new[]
        {
            UpperConstruction,
            StableConstruction,
            UpperMapping,
            StableMapping,
            UpperMember,
            StableMember,
            UpperMemberExtension,
            StableMemberExtension,
            CollisionMapper,
            StableMapper
        };
        var collisionHints = initialHints
            .Append(TitleConstruction)
            .Append(TitleMapping)
            .Append(TitleMember)
            .Append(TitleMemberExtension)
            .ToArray();

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            Step(
                "initial destination",
                [models, stable, upper],
                initialHints,
                Stage(
                    "BuildConstructionPlanRequests",
                    Expected(
                        UpperConstruction,
                        IncrementalStepRunReason.New),
                    Expected(
                        StableConstruction,
                        IncrementalStepRunReason.New))),
            Step(
                "case insensitive collision added",
                [models, stable, both],
                collisionHints,
                SurfaceCollisionAddedStages()),
            Step(
                "case insensitive collision removed",
                [models, stable, upper],
                initialHints,
                SurfaceCollisionRemovedStages()));
    }

    [Test]
    public void Removing_a_similar_destination_keeps_the_remaining_hint_names()
    {
        var models = SourceFile("Models.cs", SurfaceModelsSource);
        var stable = SourceFile("StableMapper.cs", StableMapperSource);
        var both = SourceFile(
            "CollisionMapper.cs",
            BuildCollisionMapper(
                includeUpperCase: true,
                includeTitleCase: true));
        var title = SourceFile(
            "CollisionMapper.cs",
            BuildCollisionMapper(
                includeUpperCase: false,
                includeTitleCase: true));
        var collisionHints = new[]
        {
            UpperConstruction,
            TitleConstruction,
            StableConstruction,
            UpperMapping,
            TitleMapping,
            StableMapping,
            UpperMember,
            TitleMember,
            StableMember,
            UpperMemberExtension,
            TitleMemberExtension,
            StableMemberExtension,
            CollisionMapper,
            StableMapper
        };
        var remainingHints = new[]
        {
            TitleConstruction,
            StableConstruction,
            TitleMapping,
            StableMapping,
            TitleMember,
            StableMember,
            TitleMemberExtension,
            StableMemberExtension,
            CollisionMapper,
            StableMapper
        };

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            Step(
                "colliding surfaces",
                [models, stable, both],
                collisionHints),
            Step(
                "similar destination removed",
                [models, stable, title],
                remainingHints));
    }

    [Test]
    public void Adding_similar_mapper_labels_keeps_existing_requests_cached()
    {
        var models = SourceFile("MapperModels.cs", MapperModelsSource);
        var stable = SourceFile(
            "StableMapper.cs",
            BuildMapper("StableMapper"));
        var upper = SourceFile("UpperMapper.cs", BuildMapper("URL"));
        var title = SourceFile("TitleMapper.cs", BuildMapper("Url"));
        var upperHint =
            "Morphant.Generated.TypeMapper.URL__4a82eb081f4a7a56b8896decd859114c.g.cs";
        var titleHint =
            "Morphant.Generated.TypeMapper.Url__d4a5140116a516605b9bfbf106e2cfd0.g.cs";
        var stableHint =
            "Morphant.Generated.TypeMapper.StableMapper__3251a87cd2d93d3948a4f1646b88e0d3.g.cs";
        var surfaceHints = new[]
        {
            "Morphant.Generated.Construction.Destination__2b5567bb552187abaf6a4fd02028bb84.g.cs",
            "Morphant.Generated.Member.Destination__2b5567bb552187abaf6a4fd02028bb84.g.cs",
            "Morphant.Generated.MappingExtension.StableMapper.SourceToDestination__4dc5cccf2398fab78f138fd682715480.g.cs",
            "Morphant.Generated.MemberExtension.StableMapper.SourceToDestination__4dc5cccf2398fab78f138fd682715480.g.cs"
        };
        var initialHints = surfaceHints
            .Append("Morphant.Generated.MappingExtension.URL.SourceToDestination__83f0e7d5b97b92c44f55ac44c5432be1.g.cs")
            .Append("Morphant.Generated.MemberExtension.URL.SourceToDestination__83f0e7d5b97b92c44f55ac44c5432be1.g.cs")
            .Append(upperHint)
            .Append(stableHint)
            .ToArray();
        var collisionHints = initialHints
            .Append("Morphant.Generated.MappingExtension.Url.SourceToDestination__b134aa8327824ac21f45b9223ea895df.g.cs")
            .Append("Morphant.Generated.MemberExtension.Url.SourceToDestination__b134aa8327824ac21f45b9223ea895df.g.cs")
            .Append(titleHint)
            .ToArray();

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            Step(
                "single mapper hint",
                [models, stable, upper],
                initialHints,
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(upperHint, IncrementalStepRunReason.New),
                    Expected(stableHint, IncrementalStepRunReason.New))),
            Step(
                "mapper hint collision added",
                [models, stable, upper, title],
                collisionHints,
                Stage(
                    "BuildTypeMapperModels",
                    Expected(upperHint, IncrementalStepRunReason.Cached),
                    Expected(
                        titleHint,
                        IncrementalStepRunReason.New),
                    Expected(stableHint, IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(upperHint, IncrementalStepRunReason.Cached),
                    Expected(titleHint, IncrementalStepRunReason.New),
                    Expected(
                        stableHint,
                        IncrementalStepRunReason.Cached))),
            Step(
                "mapper hint collision removed",
                [models, stable, upper],
                initialHints,
                Stage(
                    "BuildTypeMapperModels",
                    Expected(upperHint, IncrementalStepRunReason.Cached),
                    Expected(
                        titleHint,
                        IncrementalStepRunReason.Removed),
                    Expected(stableHint, IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(upperHint, IncrementalStepRunReason.Cached),
                    Expected(titleHint, IncrementalStepRunReason.Removed),
                    Expected(
                        stableHint,
                        IncrementalStepRunReason.Cached))));
    }

    [Test]
    public void Removing_a_similar_mapper_keeps_the_remaining_hint_name()
    {
        var models = SourceFile("MapperModels.cs", MapperModelsSource);
        var stable = SourceFile(
            "StableMapper.cs",
            BuildMapper("StableMapper"));
        var upper = SourceFile("UpperMapper.cs", BuildMapper("URL"));
        var title = SourceFile("TitleMapper.cs", BuildMapper("Url"));
        const string upperHint =
            "Morphant.Generated.TypeMapper.URL__4a82eb081f4a7a56b8896decd859114c.g.cs";
        const string titleHint =
            "Morphant.Generated.TypeMapper.Url__d4a5140116a516605b9bfbf106e2cfd0.g.cs";
        const string stableHint =
            "Morphant.Generated.TypeMapper.StableMapper__3251a87cd2d93d3948a4f1646b88e0d3.g.cs";
        var surfaceHints = new[]
        {
            "Morphant.Generated.Construction.Destination__2b5567bb552187abaf6a4fd02028bb84.g.cs",
            "Morphant.Generated.Member.Destination__2b5567bb552187abaf6a4fd02028bb84.g.cs",
            "Morphant.Generated.MappingExtension.StableMapper.SourceToDestination__4dc5cccf2398fab78f138fd682715480.g.cs",
            "Morphant.Generated.MemberExtension.StableMapper.SourceToDestination__4dc5cccf2398fab78f138fd682715480.g.cs"
        };

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            Step(
                "colliding mapper hints",
                [models, stable, upper, title],
                surfaceHints
                    .Append("Morphant.Generated.MappingExtension.URL.SourceToDestination__83f0e7d5b97b92c44f55ac44c5432be1.g.cs")
                    .Append("Morphant.Generated.MemberExtension.URL.SourceToDestination__83f0e7d5b97b92c44f55ac44c5432be1.g.cs")
                    .Append("Morphant.Generated.MappingExtension.Url.SourceToDestination__b134aa8327824ac21f45b9223ea895df.g.cs")
                    .Append("Morphant.Generated.MemberExtension.Url.SourceToDestination__b134aa8327824ac21f45b9223ea895df.g.cs")
                    .Append(upperHint)
                    .Append(titleHint)
                    .Append(stableHint)
                    .ToArray()),
            Step(
                "similar mapper removed",
                [models, stable, title],
                surfaceHints
                    .Append("Morphant.Generated.MappingExtension.Url.SourceToDestination__b134aa8327824ac21f45b9223ea895df.g.cs")
                    .Append("Morphant.Generated.MemberExtension.Url.SourceToDestination__b134aa8327824ac21f45b9223ea895df.g.cs")
                    .Append(titleHint)
                    .Append(stableHint)
                    .ToArray()));
    }

    private static ExpectedIncrementalStage[]
        SurfaceCollisionAddedStages()
    {
        return
        [
            Stage(
                "BuildConstructionPlanRequests",
                Expected(
                    UpperConstruction,
                    IncrementalStepRunReason.Cached),
                Expected(
                    TitleConstruction,
                    IncrementalStepRunReason.New),
                Expected(
                    StableConstruction,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildMemberPlanRequests",
                Expected(UpperMember, IncrementalStepRunReason.Cached),
                Expected(TitleMember, IncrementalStepRunReason.New),
                Expected(StableMember, IncrementalStepRunReason.Cached)),
            Stage(
                "BuildMappingExtensionRequests",
                Expected(UpperMapping, IncrementalStepRunReason.Cached),
                Expected(TitleMapping, IncrementalStepRunReason.New),
                Expected(
                    StableMapping,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildMemberExtensionRequests",
                Expected(
                    UpperMemberExtension,
                    IncrementalStepRunReason.Cached),
                Expected(
                    TitleMemberExtension,
                    IncrementalStepRunReason.New),
                Expected(
                    StableMemberExtension,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildTypeMapperRequests",
                Expected(
                    CollisionMapper,
                    IncrementalStepRunReason.Modified),
                Expected(StableMapper, IncrementalStepRunReason.Cached))
        ];
    }

    private static ExpectedIncrementalStage[]
        SurfaceCollisionRemovedStages()
    {
        return
        [
            Stage(
                "BuildConstructionPlanRequests",
                Expected(
                    UpperConstruction,
                    IncrementalStepRunReason.Cached),
                Expected(
                    TitleConstruction,
                    IncrementalStepRunReason.Removed),
                Expected(
                    StableConstruction,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildMemberPlanRequests",
                Expected(UpperMember, IncrementalStepRunReason.Cached),
                Expected(TitleMember, IncrementalStepRunReason.Removed),
                Expected(StableMember, IncrementalStepRunReason.Cached)),
            Stage(
                "BuildMappingExtensionRequests",
                Expected(UpperMapping, IncrementalStepRunReason.Cached),
                Expected(TitleMapping, IncrementalStepRunReason.Removed),
                Expected(
                    StableMapping,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildMemberExtensionRequests",
                Expected(
                    UpperMemberExtension,
                    IncrementalStepRunReason.Cached),
                Expected(
                    TitleMemberExtension,
                    IncrementalStepRunReason.Removed),
                Expected(
                    StableMemberExtension,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildTypeMapperRequests",
                Expected(
                    CollisionMapper,
                    IncrementalStepRunReason.Modified),
                Expected(StableMapper, IncrementalStepRunReason.Cached))
        ];
    }

    private static string BuildCollisionMapper(
        bool includeUpperCase,
        bool includeTitleCase)
    {
        return CollisionMapperSource
            .Replace(
                "__UPPER_MAPPING__",
                includeUpperCase
                    ? "            builder.Map<Source, URL>();"
                    : string.Empty)
            .Replace(
                "__TITLE_MAPPING__",
                includeTitleCase
                    ? "            builder.Map<Source, Url>();"
                    : string.Empty);
    }

    private static string BuildMapper(string mapperName)
    {
        return MapperSource.Replace("__MAPPER_NAME__", mapperName);
    }

    // lang=c#
    private const string SurfaceModelsSource =
"""
#nullable enable
#pragma warning disable CS1591

namespace TestCase
{
    public sealed class Source { }
    public sealed class URL
    {
        public int Value { get; set; }
    }

    public sealed class Url
    {
        public int Value { get; set; }
    }

    public sealed class StableSource { }
    public sealed class StableDestination
    {
        public int Value { get; set; }
    }
}
""";

    // lang=c#
    private const string CollisionMapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class CollisionMapper : TypeMapper<CollisionMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
__UPPER_MAPPING__
__TITLE_MAPPING__
        }
    }
}
""";

    // lang=c#
    private const string StableMapperSource =
"""
#nullable enable
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
    private const string MapperModelsSource =
"""
#nullable enable
#pragma warning disable CS1591

namespace Models
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }
}
""";

    // lang=c#
    private const string MapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class __MAPPER_NAME__ : TypeMapper<__MAPPER_NAME__>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Models.Source, Models.Destination>();
    }
}
""";
}
