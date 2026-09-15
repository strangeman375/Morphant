using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class ReferenceAndSettingsTests
{
    private const string ExternalConstruction =
        "Morphant.Generated.Construction.Destination__31cd2b83a124f88dc007fa312625999a.g.cs";

    private const string StableConstruction =
        "Morphant.Generated.Construction.StableDestination__eabf1949ab4bdd337885a7845c47d3bb.g.cs";

    private const string ExternalMapping =
        "Morphant.Generated.MappingExtension.ExternalMapper.ExternalSourceToDestination__cf5e23c78e13fc99b34ffbe25d9f3fa6.g.cs";

    private const string StableMapping =
        "Morphant.Generated.MappingExtension.StableMapper.StableSourceToStableDestination__bb75ddcff787fbc895e34e4dd60f1943.g.cs";

    private const string ExternalMember =
        "Morphant.Generated.Member.Destination__31cd2b83a124f88dc007fa312625999a.g.cs";

    private const string StableMember =
        "Morphant.Generated.Member.StableDestination__eabf1949ab4bdd337885a7845c47d3bb.g.cs";

    private const string ExternalMemberExtension =
        "Morphant.Generated.MemberExtension.ExternalMapper.ExternalSourceToDestination__cf5e23c78e13fc99b34ffbe25d9f3fa6.g.cs";

    private const string StableMemberExtension =
        "Morphant.Generated.MemberExtension.StableMapper.StableSourceToStableDestination__bb75ddcff787fbc895e34e4dd60f1943.g.cs";

    private const string ExternalMapper =
        "Morphant.Generated.TypeMapper.ExternalMapper__07a52aad7ee2b4bee3f3535408fa3189.g.cs";

    private const string StableMapper =
        "Morphant.Generated.TypeMapper.StableMapper__3251a87cd2d93d3948a4f1646b88e0d3.g.cs";

    private static readonly string[] GeneratedHints =
    [
        ExternalConstruction,
        StableConstruction,
        ExternalMapping,
        StableMapping,
        ExternalMember,
        StableMember,
        ExternalMemberExtension,
        StableMemberExtension,
        ExternalMapper,
        StableMapper
    ];

    [Test]
    public void Invalidates_only_consumers_of_a_changed_reference()
    {
        var referenceV1 = CreateReference(
            "ExternalModels",
            BuildExternalReference("int"));
        var referenceV2 = CreateReference(
            "ExternalModels",
            BuildExternalReference("long"));
        var unusedReference = CreateReference(
            "UnusedModels",
            UnusedReferenceSource);
        var files = new[]
        {
            SourceFile("ExternalMapper.cs", ExternalMapperSource),
            SourceFile("StableMapper.cs", StableMapperSource)
        };

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            StepWithReferences(
                "reference v1",
                files,
                [referenceV1],
                GeneratedHints,
                Stage(
                    "BuildConstructionPlanRequests",
                    Expected(
                        ExternalConstruction,
                        IncrementalStepRunReason.New),
                    Expected(
                        StableConstruction,
                        IncrementalStepRunReason.New)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(ExternalMapper, IncrementalStepRunReason.New),
                    Expected(StableMapper, IncrementalStepRunReason.New))),
            StepWithReferences(
                "unused reference added",
                files,
                [referenceV1, unusedReference],
                GeneratedHints,
                [
                    .. EarlyPipeline(
                        Reason(IncrementalStepRunReason.Cached, 2)),
                    Stage(
                        "BuildConstructionPlanRequests",
                        Expected(
                            ExternalConstruction,
                            IncrementalStepRunReason.Cached),
                        Expected(
                            StableConstruction,
                            IncrementalStepRunReason.Cached)),
                    Stage(
                        "BuildMemberPlanRequests",
                        Expected(
                            ExternalMember,
                            IncrementalStepRunReason.Cached),
                        Expected(
                            StableMember,
                            IncrementalStepRunReason.Cached)),
                    Stage(
                        "BuildTypeMapperRequests",
                        Expected(
                            ExternalMapper,
                            IncrementalStepRunReason.Cached),
                        Expected(
                            StableMapper,
                            IncrementalStepRunReason.Cached))
                ]),
            StepWithReferences(
                "referenced destination changed",
                files,
                [referenceV2, unusedReference],
                GeneratedHints,
                ChangedReferenceStages()),
            StepWithReferences(
                "equivalent reference restored",
                files,
                [referenceV1, unusedReference],
                GeneratedHints,
                ChangedReferenceStages()));
    }

    [Test]
    public void Tracks_contracts_from_source_backed_project_references()
    {
        var referenceV1 = CreateCompilationReference(
            "ExternalModels",
            BuildSourceBackedReference("Int32", 1));
        var unrelatedChange = CreateCompilationReference(
            "ExternalModels",
            BuildSourceBackedReference("Int32", 2));
        var referenceV2 = CreateCompilationReference(
            "ExternalModels",
            BuildSourceBackedReference("Int64", 2));
        var files = new[]
        {
            SourceFile("ExternalMapper.cs", ExternalMapperSource),
            SourceFile("StableMapper.cs", StableMapperSource)
        };

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            StepWithReferences(
                "source-backed reference v1",
                files,
                [referenceV1],
                GeneratedHints),
            StepWithReferences(
                "unrelated referenced source changed",
                files,
                [unrelatedChange],
                GeneratedHints,
                UnrelatedReferenceStages()),
            StepWithReferences(
                "source-backed destination changed",
                files,
                [referenceV2],
                GeneratedHints,
                ChangedReferenceStages()));
    }

    [Test]
    public void Keeps_recreated_trees_and_source_backed_references_aligned()
    {
        var referenceV1 = CreateCompilationReference(
            "ExternalModels",
            BuildSourceBackedReference("Int32", 1));
        var unrelatedChange = CreateCompilationReference(
            "ExternalModels",
            BuildSourceBackedReference("Int32", 2));
        var referenceV2 = CreateCompilationReference(
            "ExternalModels",
            BuildSourceBackedReference("Int64", 3));
        var files = new[]
        {
            SourceFile("ExternalMapper.cs", ExternalMapperSource),
            SourceFile("StableMapper.cs", StableMapperSource)
        };

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            StepWithReferences(
                "initial source-backed compilation",
                files,
                [referenceV1],
                GeneratedHints),
            StepWithRecreatedSyntaxTreesAndReferences(
                "all trees and unrelated referenced source recreated",
                files,
                [unrelatedChange],
                GeneratedHints,
                UnrelatedReferenceStages()),
            StepWithRecreatedSyntaxTreesAndReferences(
                "all trees and referenced contract recreated",
                files,
                [referenceV2],
                GeneratedHints,
                ChangedReferenceStages()));
    }

    [Test]
    public void Assembly_setting_rebuilds_only_mapper_artifacts()
    {
        var files = new[]
        {
            SourceFile("SettingsMappers.cs", SettingsMappersSource)
        };
        var generated = new[]
        {
            "Morphant.Generated.Construction.DestinationA__3733382c8803f06b6b6c3f9cba169e0f.g.cs",
            "Morphant.Generated.Construction.DestinationB__1f97cb9531b66ac2ca2a5923c972297b.g.cs",
            "Morphant.Generated.MappingExtension.MapperA.SourceAToDestinationA__db82dfe1a33c9cc6d08771569ec8eb46.g.cs",
            "Morphant.Generated.MappingExtension.MapperB.SourceBToDestinationB__ed707c749ae9ab2370bbe3005f6c1ef7.g.cs",
            "Morphant.Generated.Member.DestinationA__3733382c8803f06b6b6c3f9cba169e0f.g.cs",
            "Morphant.Generated.Member.DestinationB__1f97cb9531b66ac2ca2a5923c972297b.g.cs",
            "Morphant.Generated.MemberExtension.MapperA.SourceAToDestinationA__db82dfe1a33c9cc6d08771569ec8eb46.g.cs",
            "Morphant.Generated.MemberExtension.MapperB.SourceBToDestinationB__ed707c749ae9ab2370bbe3005f6c1ef7.g.cs",
            "Morphant.Generated.TypeMapper.MapperA__5e218501b1a45e633f93649296e15c01.g.cs",
            "Morphant.Generated.TypeMapper.MapperB__dca3e300d0a63d8240eb166c3130d2df.g.cs"
        };
        var mapperA =
            "Morphant.Generated.TypeMapper.MapperA__5e218501b1a45e633f93649296e15c01.g.cs";
        var mapperB =
            "Morphant.Generated.TypeMapper.MapperB__dca3e300d0a63d8240eb166c3130d2df.g.cs";
        var create = new Dictionary<string, string>
        {
            ["build_property.MorphantMappingMode"] = "Create"
        };
        var update = new Dictionary<string, string>
        {
            ["build_property.MorphantMappingMode"] = "Update"
        };

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            StepWithOptions(
                "create mode",
                files,
                create,
                generated,
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(mapperA, IncrementalStepRunReason.New),
                    Expected(mapperB, IncrementalStepRunReason.New))),
            StepWithOptions(
                "update mode",
                files,
                update,
                generated,
                SettingChangeStages(mapperA, mapperB)),
            StepWithOptions(
                "equivalent create mode restored",
                files,
                create,
                generated,
                SettingChangeStages(mapperA, mapperB)));
    }

    private static ExpectedIncrementalStage[] ChangedReferenceStages()
    {
        return
        [
            .. EarlyPipeline(
                Reason(IncrementalStepRunReason.Modified, 1),
                Reason(IncrementalStepRunReason.Cached, 1)),
            Stage(
                "BuildConstructionPlanModels",
                Expected(
                    ExternalConstruction,
                    IncrementalStepRunReason.Modified),
                Expected(
                    StableConstruction,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildConstructionPlanRequests",
                Expected(
                    ExternalConstruction,
                    IncrementalStepRunReason.Modified),
                Expected(
                    StableConstruction,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildMemberPlanModels",
                Expected(
                    ExternalMember,
                    IncrementalStepRunReason.Modified),
                Expected(
                    StableMember,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildMemberPlanRequests",
                Expected(
                    ExternalMember,
                    IncrementalStepRunReason.Modified),
                Expected(
                    StableMember,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildMappingExtensionRequests",
                Expected(
                    ExternalMapping,
                    IncrementalStepRunReason.Cached),
                Expected(
                    StableMapping,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildMemberExtensionRequests",
                Expected(
                    ExternalMemberExtension,
                    IncrementalStepRunReason.Cached),
                Expected(
                    StableMemberExtension,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildTypeMapperModels",
                Expected(
                    ExternalMapper,
                    IncrementalStepRunReason.Cached),
                Expected(
                    StableMapper,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildTypeMapperRequests",
                Expected(
                    ExternalMapper,
                    IncrementalStepRunReason.Cached),
                Expected(
                    StableMapper,
                    IncrementalStepRunReason.Cached))
        ];
    }

    private static ExpectedIncrementalStage[] UnrelatedReferenceStages()
    {
        return
        [
            .. EarlyPipeline(
                Reason(IncrementalStepRunReason.Cached, 2)),
            Stage(
                "BuildConstructionPlanRequests",
                Expected(
                    ExternalConstruction,
                    IncrementalStepRunReason.Cached),
                Expected(
                    StableConstruction,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildMemberPlanRequests",
                Expected(
                    ExternalMember,
                    IncrementalStepRunReason.Cached),
                Expected(
                    StableMember,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildTypeMapperRequests",
                Expected(
                    ExternalMapper,
                    IncrementalStepRunReason.Cached),
                Expected(
                    StableMapper,
                    IncrementalStepRunReason.Cached))
        ];
    }

    private static ExpectedIncrementalStage[] SettingChangeStages(
        string mapperA,
        string mapperB)
    {
        return
        [
            .. EarlyPipeline(
                Reason(IncrementalStepRunReason.Cached, 2)),
            Stage(
                "BuildConstructionPlanRequests",
                Expected(
                    "Morphant.Generated.Construction.DestinationA__3733382c8803f06b6b6c3f9cba169e0f.g.cs",
                    IncrementalStepRunReason.Cached),
                Expected(
                    "Morphant.Generated.Construction.DestinationB__1f97cb9531b66ac2ca2a5923c972297b.g.cs",
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildMemberPlanRequests",
                Expected(
                    "Morphant.Generated.Member.DestinationA__3733382c8803f06b6b6c3f9cba169e0f.g.cs",
                    IncrementalStepRunReason.Cached),
                Expected(
                    "Morphant.Generated.Member.DestinationB__1f97cb9531b66ac2ca2a5923c972297b.g.cs",
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildTypeMapperModels",
                Expected(mapperA, IncrementalStepRunReason.Modified),
                Expected(mapperB, IncrementalStepRunReason.Modified)),
            Stage(
                "BuildTypeMapperRequests",
                Expected(mapperA, IncrementalStepRunReason.Modified),
                Expected(mapperB, IncrementalStepRunReason.Modified))
        ];
    }

    private static string BuildExternalReference(string valueType)
    {
        return ExternalReferenceSource.Replace("__VALUE_TYPE__", valueType);
    }

    private static string BuildSourceBackedReference(
        string valueType,
        int unrelatedValue)
    {
        return SourceBackedReferenceSource
            .Replace("__VALUE_TYPE__", valueType)
            .Replace(
                "__UNRELATED_VALUE__",
                unrelatedValue.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
    }

    // lang=c#
    private const string ExternalReferenceSource =
"""
#nullable enable
#pragma warning disable CS1591

namespace ExternalModels
{
    public sealed class Destination
    {
        public Destination(__VALUE_TYPE__ value) => Value = value;

        public __VALUE_TYPE__ Value { get; set; }
    }
}
""";

    // lang=c#
    private const string UnusedReferenceSource =
"""
#nullable enable
#pragma warning disable CS1591

namespace UnusedModels
{
    public sealed class Value { }
}
""";

    // lang=c#
    private const string SourceBackedReferenceSource =
"""
#nullable enable
#pragma warning disable CS1591

using ContractValue = System.__VALUE_TYPE__;

namespace ExternalModels
{
    public sealed class Destination
    {
        public Destination(ContractValue value) => Value = value;

        public ContractValue Value { get; set; }
    }

    public static class Unrelated
    {
        public const int Value = __UNRELATED_VALUE__;
    }
}
""";

    // lang=c#
    private const string ExternalMapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using ExternalModels;
using Morphant;

namespace TestCase
{
    public sealed class ExternalSource
    {
        public int Value { get; init; }
    }

    [MorphantMapper]
    public partial class ExternalMapper : TypeMapper<ExternalMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<ExternalSource, Destination>();
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
    public sealed class StableSource
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed class StableDestination
    {
        public string Name { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class StableMapper : TypeMapper<StableMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<StableSource, StableDestination>();
    }
}
""";

    // lang=c#
    private const string SettingsMappersSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class SourceA
    {
        public int Value { get; init; }
    }

    public sealed class DestinationA
    {
        public int Value { get; set; }
    }

    public sealed class SourceB
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed class DestinationB
    {
        public string Name { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class MapperA : TypeMapper<MapperA>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<SourceA, DestinationA>();
    }

    [MorphantMapper]
    public partial class MapperB : TypeMapper<MapperB>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<SourceB, DestinationB>();
    }
}
""";
}
