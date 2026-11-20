using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class TupleTests
{
    private const string OldConstruction =
        "Morphant.Generated.Construction.ValueTuple2__3b1e68a198da76249c517e6be4de50ac.g.cs";

    private const string NewConstruction =
        "Morphant.Generated.Construction.ValueTuple2__5aa24c2801582c743af2ed2fb0d3ac56.g.cs";

    private const string OldMember =
        "Morphant.Generated.Member.ValueTuple2__3b1e68a198da76249c517e6be4de50ac.g.cs";

    private const string NewMember =
        "Morphant.Generated.Member.ValueTuple2__5aa24c2801582c743af2ed2fb0d3ac56.g.cs";

    private const string OldTupleMappingExtension =
        "Morphant.Generated.MappingExtension.TupleSourceToValueTuple2__aa2853039d60ebad1dfb93f13938bcc1.g.cs";

    private const string NewTupleMappingExtension =
        "Morphant.Generated.MappingExtension.TupleSourceToValueTuple2__5fcfd052a2158cf6cb3e5545f9f26536.g.cs";

    private const string OldTupleMemberExtension =
        "Morphant.Generated.MemberExtension.TupleSourceToValueTuple2__aa2853039d60ebad1dfb93f13938bcc1.g.cs";

    private const string NewTupleMemberExtension =
        "Morphant.Generated.MemberExtension.TupleSourceToValueTuple2__5fcfd052a2158cf6cb3e5545f9f26536.g.cs";

    private const string TupleMapper =
        "Morphant.Generated.TypeMapper.TupleMapper__d3f34444739964258f945f6b73ecca58.g.cs";

    private const string StableConstruction =
        "Morphant.Generated.Construction.StableDestination__eabf1949ab4bdd337885a7845c47d3bb.g.cs";

    private const string StableMappingExtension =
        "Morphant.Generated.MappingExtension.StableSourceToStableDestination__183aa6bcace2f8e773b98c81e36c9920.g.cs";

    private const string StableMember =
        "Morphant.Generated.Member.StableDestination__eabf1949ab4bdd337885a7845c47d3bb.g.cs";

    private const string StableMemberExtension =
        "Morphant.Generated.MemberExtension.StableSourceToStableDestination__183aa6bcace2f8e773b98c81e36c9920.g.cs";

    private const string StableMapper =
        "Morphant.Generated.TypeMapper.StableMapper__3251a87cd2d93d3948a4f1646b88e0d3.g.cs";

    private const string SharedTupleExtension =
        "Morphant.Generated.MappingExtension.ValueTuple2ToInt32__20b3de42e67b0b11483f1bb4dd3387dd.g.cs";

    private const string FirstTupleMapper =
        "Morphant.Generated.TypeMapper.FirstMapper__4d569664dcc0275de3ba45ccfd799fe1.g.cs";

    private const string SecondTupleMapper =
        "Morphant.Generated.TypeMapper.SecondMapper__1bdb3890185e1d22ec23681d67d25e87.g.cs";

    [Test]
    public void Renaming_a_tuple_element_invalidates_only_affected_outputs()
    {
        var models = SourceFile("Models.cs", ModelsSource);
        var stable = SourceFile("StableMapper.cs", StableMapperSource);
        var oldTuple = SourceFile(
            "TupleMapper.cs",
            BuildTupleMapper("Id"));
        var newTuple = SourceFile(
            "TupleMapper.cs",
            BuildTupleMapper("Code"));
        var stableHints = new[]
        {
            StableConstruction,
            StableMappingExtension,
            StableMember,
            StableMemberExtension,
            StableMapper
        };
        var newHints = stableHints.Concat(new[]
        {
            NewConstruction,
            NewTupleMappingExtension,
            NewMember,
            NewTupleMemberExtension,
            TupleMapper
        }).ToArray();

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            Step(
                "initial tuple presentation",
                [models, stable, oldTuple],
                stableHints.Concat(new[]
                {
                    OldConstruction,
                    OldTupleMappingExtension,
                    OldMember,
                    OldTupleMemberExtension,
                    TupleMapper
                }).ToArray()),
            Step(
                "tuple element renamed",
                [models, stable, newTuple],
                newHints,
                Stage(
                    "BuildConstructionPlanRequests",
                    Expected(
                        NewConstruction,
                        IncrementalStepRunReason.Modified),
                    Expected(
                        StableConstruction,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMemberPlanRequests",
                    Expected(
                        NewMember,
                        IncrementalStepRunReason.Modified),
                    Expected(
                        StableMember,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMappingExtensionRequests",
                    Expected(
                        NewTupleMappingExtension,
                        IncrementalStepRunReason.Modified),
                    Expected(
                        StableMappingExtension,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMemberExtensionRequests",
                    Expected(
                        NewTupleMemberExtension,
                        IncrementalStepRunReason.Modified),
                    Expected(
                        StableMemberExtension,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(
                        TupleMapper,
                        IncrementalStepRunReason.Modified),
                    Expected(
                        StableMapper,
                        IncrementalStepRunReason.Cached))),
            StepWithRecreatedSyntaxTrees(
                "same tuple compilation recreated",
                [models, stable, newTuple],
                newHints,
                [
                    .. EarlyPipeline(
                        Reason(IncrementalStepRunReason.Cached, 2)),
                    Stage(
                        "BuildConstructionPlanRequests",
                        Expected(
                            NewConstruction,
                            IncrementalStepRunReason.Cached),
                        Expected(
                            StableConstruction,
                            IncrementalStepRunReason.Cached)),
                    Stage(
                        "BuildMemberPlanRequests",
                        Expected(
                            NewMember,
                            IncrementalStepRunReason.Cached),
                        Expected(
                            StableMember,
                            IncrementalStepRunReason.Cached)),
                    Stage(
                        "BuildMappingExtensionRequests",
                        Expected(
                            NewTupleMappingExtension,
                            IncrementalStepRunReason.Cached),
                        Expected(
                            StableMappingExtension,
                            IncrementalStepRunReason.Cached)),
                    Stage(
                        "BuildMemberExtensionRequests",
                        Expected(
                            NewTupleMemberExtension,
                            IncrementalStepRunReason.Cached),
                        Expected(
                            StableMemberExtension,
                            IncrementalStepRunReason.Cached)),
                    Stage(
                        "BuildTypeMapperRequests",
                        Expected(
                            TupleMapper,
                            IncrementalStepRunReason.Cached),
                        Expected(
                            StableMapper,
                            IncrementalStepRunReason.Cached))
                ]));
    }

    [Test]
    public void An_independent_mapper_reuses_the_cached_tuple_extension()
    {
        var first = SourceFile("FirstMapper.cs", FirstTupleMapperSource);
        var second = SourceFile("SecondMapper.cs", SecondTupleMapperSource);
        var firstHints = new[]
        {
            SharedTupleExtension,
            FirstTupleMapper
        };
        var bothHints = firstHints.Concat(new[]
        {
            SecondTupleMapper
        }).ToArray();

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            Step(
                "first tuple mapper",
                [first],
                firstHints),
            Step(
                "independent tuple mapper added",
                [first, second],
                bothHints,
                Stage(
                    "BuildMappingExtensionRequests",
                    Expected(
                        SharedTupleExtension,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(
                        FirstTupleMapper,
                        IncrementalStepRunReason.Cached),
                    Expected(
                        SecondTupleMapper,
                        IncrementalStepRunReason.New))),
            Step(
                "independent tuple mapper removed",
                [first],
                firstHints,
                Stage(
                    "BuildMappingExtensionRequests",
                    Expected(
                        SharedTupleExtension,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(
                        FirstTupleMapper,
                        IncrementalStepRunReason.Cached),
                    Expected(
                        SecondTupleMapper,
                        IncrementalStepRunReason.Removed))));
    }

    private static string BuildTupleMapper(string elementName) =>
        TupleMapperSource.Replace("__ELEMENT__", elementName);

    // lang=c#
    private const string ModelsSource =
"""
#nullable enable
#pragma warning disable CS1591

namespace TestCase
{
    public sealed class TupleSource
    {
        public int Id { get; init; }

        public int Code { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    public sealed class StableSource
    {
        public int Value { get; init; }
    }

    public sealed class StableDestination
    {
        public int Value { get; set; }
    }
}
""";

    // lang=c#
    private const string TupleMapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class TupleMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<TupleSource, (int __ELEMENT__, string Name)>();
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
    public partial class StableMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<StableSource, StableDestination>();
    }
}
""";

    // lang=c#
    private const string FirstTupleMapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class FirstMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<(int X, int Y), int>()
                .Convert(source => source.X + source.Y);
    }
}
""";

    // lang=c#
    private const string SecondTupleMapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class SecondMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<(int X, int Y), int>()
                .Convert(source => source.X * source.Y);
    }
}
""";
}
