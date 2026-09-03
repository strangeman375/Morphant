using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class SyntaxIsolationTests
{
    private const string Construction =
        "Morphant.Generated.Construction.TestCase_Destination.g.cs";

    private const string MappingExtension =
        "Morphant.Generated.MappingExtension." +
        "TestCase_Source__TestCase_Destination.g.cs";

    private const string Member =
        "Morphant.Generated.Member.TestCase_Destination.g.cs";

    private const string MemberExtension =
        "Morphant.Generated.MemberExtension." +
        "TestCase_Source__TestCase_Destination.g.cs";

    private const string Mapper =
        "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs";

    [Test]
    public void Distinguishes_contract_content_from_unrelated_syntax()
    {
        var mapper = SourceFile("Mapper.cs", MapperSource);
        var generated = new[]
        {
            Construction,
            MappingExtension,
            Member,
            MemberExtension,
            Mapper
        };

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            Step(
                "initial syntax",
                [mapper, SourceFile("Models.cs", BuildModels())],
                generated,
                Stage(
                    "BuildConstructionPlanRequests",
                    Expected(Construction, IncrementalStepRunReason.New)),
                Stage(
                    "BuildMemberPlanRequests",
                    Expected(Member, IncrementalStepRunReason.New))),
            Step(
                "unrelated documentation attribute and body changed",
                [
                    mapper,
                    SourceFile(
                        "Models.cs",
                        BuildModels(
                            unrelatedDocumentation: "Second version.",
                            unrelatedMarker: 2,
                            unrelatedBody: 2))
                ],
                generated,
                CachedArtifactStages()),
            Step(
                "equivalent using changed",
                [
                    mapper,
                    SourceFile(
                        "Models.cs",
                        BuildModels(
                            usingTarget: "global::System.Int32",
                            unrelatedDocumentation: "Second version.",
                            unrelatedMarker: 2,
                            unrelatedBody: 2))
                ],
                generated,
                CachedArtifactStages()),
            Step(
                "irrelevant destination attribute changed",
                [
                    mapper,
                    SourceFile(
                        "Models.cs",
                        BuildModels(
                            usingTarget: "global::System.Int32",
                            destinationMarker: 2,
                            unrelatedDocumentation: "Second version.",
                            unrelatedMarker: 2,
                            unrelatedBody: 2))
                ],
                generated,
                Stage(
                    "BuildConstructionPlanModels",
                    Expected(
                        Construction,
                        IncrementalStepRunReason.Unchanged)),
                Stage(
                    "BuildConstructionPlanRequests",
                    Expected(
                        Construction,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMemberPlanModels",
                    Expected(Member, IncrementalStepRunReason.Unchanged)),
                Stage(
                    "BuildMemberPlanRequests",
                    Expected(Member, IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(Mapper, IncrementalStepRunReason.Cached))),
            Step(
                "documentation wording changed",
                [
                    mapper,
                    SourceFile(
                        "Models.cs",
                        BuildModels(
                            usingTarget: "global::System.Int32",
                            destinationDocumentation: "Second version.",
                            destinationMarker: 2,
                            unrelatedDocumentation: "Second version.",
                            unrelatedMarker: 2,
                            unrelatedBody: 2))
                ],
                generated,
                Stage(
                    "BuildConstructionPlanModels",
                    Expected(
                        Construction,
                        IncrementalStepRunReason.Unchanged)),
                Stage(
                    "BuildConstructionPlanRequests",
                    Expected(
                        Construction,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMemberPlanModels",
                    Expected(Member, IncrementalStepRunReason.Unchanged)),
                Stage(
                    "BuildMemberPlanRequests",
                    Expected(Member, IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(Mapper, IncrementalStepRunReason.Cached))),
            Step(
                "destination documentation removed",
                [
                    mapper,
                    SourceFile(
                        "Models.cs",
                        BuildModels(
                            usingTarget: "global::System.Int32",
                            destinationDocumentation: "Second version.",
                            includeDestinationDocumentation: false,
                            destinationMarker: 2,
                            unrelatedDocumentation: "Second version.",
                            unrelatedMarker: 2,
                            unrelatedBody: 2))
                ],
                generated,
                Stage(
                    "BuildConstructionPlanModels",
                    Expected(
                        Construction,
                        IncrementalStepRunReason.Unchanged)),
                Stage(
                    "BuildConstructionPlanRequests",
                    Expected(
                        Construction,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMemberPlanModels",
                    Expected(Member, IncrementalStepRunReason.Unchanged)),
                Stage(
                    "BuildMemberPlanRequests",
                    Expected(Member, IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMappingExtensionRequests",
                    Expected(
                        MappingExtension,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMemberExtensionRequests",
                    Expected(
                        MemberExtension,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(Mapper, IncrementalStepRunReason.Cached))));
    }

    private static ExpectedIncrementalStage[] CachedArtifactStages()
    {
        return
        [
            Stage(
                "BuildConstructionPlanModels",
                Expected(Construction, IncrementalStepRunReason.Cached)),
            Stage(
                "BuildConstructionPlanRequests",
                Expected(Construction, IncrementalStepRunReason.Cached)),
            Stage(
                "BuildMemberPlanModels",
                Expected(Member, IncrementalStepRunReason.Cached)),
            Stage(
                "BuildMemberPlanRequests",
                Expected(Member, IncrementalStepRunReason.Cached)),
            Stage(
                "BuildMappingExtensionRequests",
                Expected(
                    MappingExtension,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildMemberExtensionRequests",
                Expected(
                    MemberExtension,
                    IncrementalStepRunReason.Cached)),
            Stage(
                "BuildTypeMapperRequests",
                Expected(Mapper, IncrementalStepRunReason.Cached))
        ];
    }

    private static string BuildModels(
        string usingTarget = "System.Int32",
        string destinationDocumentation = "First version.",
        bool includeDestinationDocumentation = true,
        int destinationMarker = 1,
        string unrelatedDocumentation = "First version.",
        int unrelatedMarker = 1,
        int unrelatedBody = 1)
    {
        return ModelsSource
            .Replace("__USING_TARGET__", usingTarget)
            .Replace(
                "__DESTINATION_DOCUMENTATION__",
                includeDestinationDocumentation
                    ? "    /// <summary>" +
                      destinationDocumentation +
                      "</summary>" + Environment.NewLine
                    : string.Empty)
            .Replace(
                "__DESTINATION_MARKER__",
                destinationMarker.ToString())
            .Replace(
                "__UNRELATED_DOCUMENTATION__",
                unrelatedDocumentation)
            .Replace(
                "__UNRELATED_MARKER__",
                unrelatedMarker.ToString())
            .Replace("__UNRELATED_BODY__", unrelatedBody.ToString());
    }

    // lang=c#
    private const string MapperSource =
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

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

    // lang=c#
    private const string ModelsSource =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Number = __USING_TARGET__;

namespace TestCase
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class MarkerAttribute : Attribute
    {
        public MarkerAttribute(int version) { }
    }

__DESTINATION_DOCUMENTATION__
    [Marker(__DESTINATION_MARKER__)]
    public sealed class Destination
    {
        public Destination(Number value) => Value = value;

        public Number Value { get; set; }
    }

    /// <summary>__UNRELATED_DOCUMENTATION__</summary>
    [Marker(__UNRELATED_MARKER__)]
    internal sealed class Unrelated
    {
        public int GetVersion() => __UNRELATED_BODY__;
    }
}
""";
}
