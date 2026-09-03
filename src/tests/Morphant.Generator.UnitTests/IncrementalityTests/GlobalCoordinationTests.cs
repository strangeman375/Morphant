using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class GlobalCoordinationTests
{
    private const string UpperConstruction =
        "Morphant.Generated.Construction.TestCase_URL.g.cs";

    private const string TitleConstruction =
        "Morphant.Generated.Construction." +
        "TestCase_Url__e9fae35bfd70d886.g.cs";

    private const string ReadableTitleConstruction =
        "Morphant.Generated.Construction.TestCase_Url.g.cs";

    private const string StableConstruction =
        "Morphant.Generated.Construction.TestCase_StableDestination.g.cs";

    private const string UpperMapping =
        "Morphant.Generated.MappingExtension." +
        "TestCase_Source__TestCase_URL.g.cs";

    private const string TitleMapping =
        "Morphant.Generated.MappingExtension." +
        "TestCase_Source__TestCase_Url__df20b2fbed6d104d.g.cs";

    private const string ReadableTitleMapping =
        "Morphant.Generated.MappingExtension." +
        "TestCase_Source__TestCase_Url.g.cs";

    private const string StableMapping =
        "Morphant.Generated.MappingExtension." +
        "TestCase_StableSource__TestCase_StableDestination.g.cs";

    private const string UpperMember =
        "Morphant.Generated.Member.TestCase_URL.g.cs";

    private const string TitleMember =
        "Morphant.Generated.Member." +
        "TestCase_Url__e9fae35bfd70d886.g.cs";

    private const string ReadableTitleMember =
        "Morphant.Generated.Member.TestCase_Url.g.cs";

    private const string StableMember =
        "Morphant.Generated.Member.TestCase_StableDestination.g.cs";

    private const string UpperMemberExtension =
        "Morphant.Generated.MemberExtension." +
        "TestCase_Source__TestCase_URL.g.cs";

    private const string TitleMemberExtension =
        "Morphant.Generated.MemberExtension." +
        "TestCase_Source__TestCase_Url__df20b2fbed6d104d.g.cs";

    private const string ReadableTitleMemberExtension =
        "Morphant.Generated.MemberExtension." +
        "TestCase_Source__TestCase_Url.g.cs";

    private const string StableMemberExtension =
        "Morphant.Generated.MemberExtension." +
        "TestCase_StableSource__TestCase_StableDestination.g.cs";

    private const string CollisionMapper =
        "Morphant.Generated.TypeMapper.TestCase_CollisionMapper.g.cs";

    private const string StableMapper =
        "Morphant.Generated.TypeMapper.TestCase_StableMapper.g.cs";

    [Test]
    public void Coordinates_only_real_surface_hint_collisions()
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
                "single readable hint",
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
    public void Transfers_readable_surface_hints_to_the_remaining_owner()
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
            ReadableTitleConstruction,
            StableConstruction,
            ReadableTitleMapping,
            StableMapping,
            ReadableTitleMember,
            StableMember,
            ReadableTitleMemberExtension,
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
                "original readable owner removed",
                [models, stable, title],
                remainingHints));
    }

    [Test]
    public void Coordinates_type_mapper_hint_collisions_without_reemission()
    {
        var models = SourceFile("MapperModels.cs", MapperModelsSource);
        var stable = SourceFile(
            "StableMapper.cs",
            BuildMapper("StableMapper"));
        var upper = SourceFile("UpperMapper.cs", BuildMapper("URL"));
        var title = SourceFile("TitleMapper.cs", BuildMapper("Url"));
        var upperHint =
            "Morphant.Generated.TypeMapper.TestCase_URL.g.cs";
        var titleHint =
            "Morphant.Generated.TypeMapper." +
            "TestCase_Url__e9fae35bfd70d886.g.cs";
        var titleModelHint =
            "Morphant.Generated.TypeMapper.TestCase_Url.g.cs";
        var stableHint =
            "Morphant.Generated.TypeMapper.TestCase_StableMapper.g.cs";
        var surfaceHints = new[]
        {
            "Morphant.Generated.Construction.Models_Destination.g.cs",
            "Morphant.Generated.MappingExtension." +
            "Models_Source__Models_Destination.g.cs",
            "Morphant.Generated.Member.Models_Destination.g.cs",
            "Morphant.Generated.MemberExtension." +
            "Models_Source__Models_Destination.g.cs"
        };
        var initialHints = surfaceHints
            .Append(upperHint)
            .Append(stableHint)
            .ToArray();
        var collisionHints = initialHints.Append(titleHint).ToArray();

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
                        titleModelHint,
                        IncrementalStepRunReason.New),
                    Expected(stableHint, IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(upperHint, IncrementalStepRunReason.Unchanged),
                    Expected(titleHint, IncrementalStepRunReason.New),
                    Expected(
                        stableHint,
                        IncrementalStepRunReason.Unchanged))),
            Step(
                "mapper hint collision removed",
                [models, stable, upper],
                initialHints,
                Stage(
                    "BuildTypeMapperModels",
                    Expected(upperHint, IncrementalStepRunReason.Cached),
                    Expected(
                        titleModelHint,
                        IncrementalStepRunReason.Removed),
                    Expected(stableHint, IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(upperHint, IncrementalStepRunReason.Unchanged),
                    Expected(titleHint, IncrementalStepRunReason.Removed),
                    Expected(
                        stableHint,
                        IncrementalStepRunReason.Unchanged))));
    }

    [Test]
    public void Transfers_a_readable_mapper_hint_to_the_remaining_owner()
    {
        var models = SourceFile("MapperModels.cs", MapperModelsSource);
        var stable = SourceFile(
            "StableMapper.cs",
            BuildMapper("StableMapper"));
        var upper = SourceFile("UpperMapper.cs", BuildMapper("URL"));
        var title = SourceFile("TitleMapper.cs", BuildMapper("Url"));
        const string upperHint =
            "Morphant.Generated.TypeMapper.TestCase_URL.g.cs";
        const string hashedTitleHint =
            "Morphant.Generated.TypeMapper." +
            "TestCase_Url__e9fae35bfd70d886.g.cs";
        const string readableTitleHint =
            "Morphant.Generated.TypeMapper.TestCase_Url.g.cs";
        const string stableHint =
            "Morphant.Generated.TypeMapper.TestCase_StableMapper.g.cs";
        var surfaceHints = new[]
        {
            "Morphant.Generated.Construction.Models_Destination.g.cs",
            "Morphant.Generated.MappingExtension." +
            "Models_Source__Models_Destination.g.cs",
            "Morphant.Generated.Member.Models_Destination.g.cs",
            "Morphant.Generated.MemberExtension." +
            "Models_Source__Models_Destination.g.cs"
        };

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            Step(
                "colliding mapper hints",
                [models, stable, upper, title],
                surfaceHints
                    .Append(upperHint)
                    .Append(hashedTitleHint)
                    .Append(stableHint)
                    .ToArray()),
            Step(
                "original readable mapper removed",
                [models, stable, title],
                surfaceHints
                    .Append(readableTitleHint)
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
                Expected(UpperMapping, IncrementalStepRunReason.Unchanged),
                Expected(TitleMapping, IncrementalStepRunReason.New),
                Expected(
                    StableMapping,
                    IncrementalStepRunReason.Unchanged)),
            Stage(
                "BuildMemberExtensionRequests",
                Expected(
                    UpperMemberExtension,
                    IncrementalStepRunReason.Unchanged),
                Expected(
                    TitleMemberExtension,
                    IncrementalStepRunReason.New),
                Expected(
                    StableMemberExtension,
                    IncrementalStepRunReason.Unchanged)),
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
                Expected(UpperMapping, IncrementalStepRunReason.Unchanged),
                Expected(TitleMapping, IncrementalStepRunReason.Removed),
                Expected(
                    StableMapping,
                    IncrementalStepRunReason.Unchanged)),
            Stage(
                "BuildMemberExtensionRequests",
                Expected(
                    UpperMemberExtension,
                    IncrementalStepRunReason.Unchanged),
                Expected(
                    TitleMemberExtension,
                    IncrementalStepRunReason.Removed),
                Expected(
                    StableMemberExtension,
                    IncrementalStepRunReason.Unchanged)),
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
