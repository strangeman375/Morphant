using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class CachingTests
{
    private const string MapperHint =
        "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs";

    private const string ConstructionHint =
        "Morphant.Generated.Construction.TestCase_Destination.g.cs";

    private const string MappingExtensionHint =
        "Morphant.Generated.MappingExtension.TestCase_Source__TestCase_Destination__TestCase_TestMapper.g.cs";

    private const string MemberHint =
        "Morphant.Generated.Member.TestCase_Destination.g.cs";

    private const string MemberExtensionHint =
        "Morphant.Generated.MemberExtension.TestCase_Source__TestCase_Destination__TestCase_TestMapper.g.cs";

    [Test]
    public void Reports_unrelated_edit_reason()
    {
        var initialFiles = new[]
        {
            SourceFile("Mapping.cs", MappingSource),
            SourceFile("Unrelated.cs", BuildUnrelatedSource(1))
        };
        var updatedFiles = new[]
        {
            initialFiles[0],
            SourceFile("Unrelated.cs", BuildUnrelatedSource(2))
        };
        var generated = new[]
        {
            ConstructionHint,
            MappingExtensionHint,
            MemberHint,
            MemberExtensionHint,
            MapperHint
        };

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            Step(
                "initial",
                initialFiles,
                generated,
                Stage(
                    "BuildConstructionPlanModels",
                    Expected(
                        ConstructionHint,
                        IncrementalStepRunReason.New)),
                Stage(
                    "BuildConstructionPlanRequests",
                    Expected(
                        ConstructionHint,
                        IncrementalStepRunReason.New)),
                Stage(
                    "BuildMappingExtensionModels",
                    Expected(
                        MappingExtensionHint,
                        IncrementalStepRunReason.New)),
                Stage(
                    "BuildMappingExtensionRequests",
                    Expected(
                        MappingExtensionHint,
                        IncrementalStepRunReason.New)),
                Stage(
                    "BuildMemberPlanModels",
                    Expected(
                        MemberHint,
                        IncrementalStepRunReason.New)),
                Stage(
                    "BuildMemberPlanRequests",
                    Expected(
                        MemberHint,
                        IncrementalStepRunReason.New)),
                Stage(
                    "BuildMemberExtensionModels",
                    Expected(
                        MemberExtensionHint,
                        IncrementalStepRunReason.New)),
                Stage(
                    "BuildMemberExtensionRequests",
                    Expected(
                        MemberExtensionHint,
                        IncrementalStepRunReason.New)),
                Stage(
                    "BuildTypeMapperModels",
                    Expected(MapperHint, IncrementalStepRunReason.New)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(MapperHint, IncrementalStepRunReason.New))),
            Step(
                "unrelated edit",
                updatedFiles,
                generated,
                [
                    .. EarlyPipeline(
                        Reason(IncrementalStepRunReason.Cached, 1)),
                    Stage(
                        "BuildConstructionPlanModels",
                        Expected(
                            ConstructionHint,
                            IncrementalStepRunReason.Cached)),
                    Stage(
                        "BuildConstructionPlanRequests",
                        Expected(
                            ConstructionHint,
                            IncrementalStepRunReason.Cached)),
                    Stage(
                        "BuildMappingExtensionModels",
                        Expected(
                            MappingExtensionHint,
                            IncrementalStepRunReason.Cached)),
                    Stage(
                        "BuildMappingExtensionRequests",
                        Expected(
                            MappingExtensionHint,
                            IncrementalStepRunReason.Cached)),
                    Stage(
                        "BuildMemberPlanModels",
                        Expected(
                            MemberHint,
                            IncrementalStepRunReason.Cached)),
                    Stage(
                        "BuildMemberPlanRequests",
                        Expected(
                            MemberHint,
                            IncrementalStepRunReason.Cached)),
                    Stage(
                        "BuildMemberExtensionModels",
                        Expected(
                            MemberExtensionHint,
                            IncrementalStepRunReason.Cached)),
                    Stage(
                        "BuildMemberExtensionRequests",
                        Expected(
                            MemberExtensionHint,
                            IncrementalStepRunReason.Cached)),
                    Stage(
                        "BuildTypeMapperModels",
                        Expected(
                            MapperHint,
                            IncrementalStepRunReason.Cached)),
                    Stage(
                        "BuildTypeMapperRequests",
                        Expected(
                            MapperHint,
                            IncrementalStepRunReason.Cached))
                ]),
            Step(
                "identical rerun",
                updatedFiles,
                generated,
                [
                    .. EarlyPipeline(
                        Reason(IncrementalStepRunReason.Cached, 1)),
                    .. CachedOutputStages()
                ]));
    }

    private static ExpectedIncrementalStage[] CachedOutputStages()
    {
        return
        [
            Stage(
                "BuildConstructionPlanModels",
                Expected(
                    ConstructionHint,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildConstructionPlanRequests",
                Expected(
                    ConstructionHint,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildMappingExtensionModels",
                Expected(
                    MappingExtensionHint,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildMappingExtensionRequests",
                Expected(
                    MappingExtensionHint,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildMemberPlanModels",
                Expected(MemberHint, IncrementalStepRunReason.Cached)),
            Stage(
                "BuildMemberPlanRequests",
                Expected(MemberHint, IncrementalStepRunReason.Cached)),
            Stage(
                "BuildMemberExtensionModels",
                Expected(
                    MemberExtensionHint,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildMemberExtensionRequests",
                Expected(
                    MemberExtensionHint,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildTypeMapperModels",
                Expected(MapperHint, IncrementalStepRunReason.Cached)),
            Stage(
                "BuildTypeMapperRequests",
                Expected(MapperHint, IncrementalStepRunReason.Cached))
        ];
    }

    private static string BuildUnrelatedSource(int value)
    {
        return UnrelatedSource.Replace("__VALUE__", value.ToString());
    }

    // lang=c#
    private const string MappingSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

    // lang=c#
    private const string UnrelatedSource =
"""
#pragma warning disable CS1591

namespace Unrelated
{
    internal static class Version
    {
        public const int Value = __VALUE__;
    }
}
""";
}
