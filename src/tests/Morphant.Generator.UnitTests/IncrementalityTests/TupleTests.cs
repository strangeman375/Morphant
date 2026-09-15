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
        "Morphant.Generated.MappingExtension.TupleMapper.TupleSourceToValueTuple2__a18b216bf0de1e8dfc4c42bb7afc07a3.g.cs";

    private const string NewTupleMappingExtension =
        "Morphant.Generated.MappingExtension.TupleMapper.TupleSourceToValueTuple2__0709fac8e537bea4f858c2174a5b0226.g.cs";

    private const string OldTupleMemberExtension =
        "Morphant.Generated.MemberExtension.TupleMapper.TupleSourceToValueTuple2__a18b216bf0de1e8dfc4c42bb7afc07a3.g.cs";

    private const string NewTupleMemberExtension =
        "Morphant.Generated.MemberExtension.TupleMapper.TupleSourceToValueTuple2__0709fac8e537bea4f858c2174a5b0226.g.cs";

    private const string TupleMapper =
        "Morphant.Generated.TypeMapper.TupleMapper__d3f34444739964258f945f6b73ecca58.g.cs";

    private const string StableConstruction =
        "Morphant.Generated.Construction.StableDestination__eabf1949ab4bdd337885a7845c47d3bb.g.cs";

    private const string StableMappingExtension =
        "Morphant.Generated.MappingExtension.StableMapper.StableSourceToStableDestination__bb75ddcff787fbc895e34e4dd60f1943.g.cs";

    private const string StableMember =
        "Morphant.Generated.Member.StableDestination__eabf1949ab4bdd337885a7845c47d3bb.g.cs";

    private const string StableMemberExtension =
        "Morphant.Generated.MemberExtension.StableMapper.StableSourceToStableDestination__bb75ddcff787fbc895e34e4dd60f1943.g.cs";

    private const string StableMapper =
        "Morphant.Generated.TypeMapper.StableMapper__3251a87cd2d93d3948a4f1646b88e0d3.g.cs";

    private const string FirstScopedMappingExtension =
        "Morphant.Generated.MappingExtension.FirstMapper.ValueTuple2ToInt32__81d4a11e0526aadb9f10096399e1c1e3.g.cs";

    private const string SecondScopedMappingExtension =
        "Morphant.Generated.MappingExtension.SecondMapper.ValueTuple2ToInt32__c894737b18825758ea16d72c3ab6e065.g.cs";

    private const string FirstScopedMapper =
        "Morphant.Generated.TypeMapper.FirstMapper__4d569664dcc0275de3ba45ccfd799fe1.g.cs";

    private const string SecondScopedMapper =
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
    public void Adding_an_unrelated_tuple_presentation_keeps_existing_scope_cached()
    {
        var first = SourceFile("FirstMapper.cs", FirstScopedMapperSource);
        var second = SourceFile("SecondMapper.cs", SecondScopedMapperSource);
        var firstHints = new[]
        {
            FirstScopedMappingExtension,
            FirstScopedMapper
        };
        var bothHints = firstHints.Concat(new[]
        {
            SecondScopedMappingExtension,
            SecondScopedMapper
        }).ToArray();

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            Step(
                "first tuple scope",
                [first],
                firstHints),
            Step(
                "unrelated tuple presentation added",
                [first, second],
                bothHints,
                Stage(
                    "BuildMappingExtensionRequests",
                    Expected(
                        FirstScopedMappingExtension,
                        IncrementalStepRunReason.Cached),
                    Expected(
                        SecondScopedMappingExtension,
                        IncrementalStepRunReason.New)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(
                        FirstScopedMapper,
                        IncrementalStepRunReason.Cached),
                    Expected(
                        SecondScopedMapper,
                        IncrementalStepRunReason.New))),
            Step(
                "unrelated tuple presentation removed",
                [first],
                firstHints,
                Stage(
                    "BuildMappingExtensionRequests",
                    Expected(
                        FirstScopedMappingExtension,
                        IncrementalStepRunReason.Cached),
                    Expected(
                        SecondScopedMappingExtension,
                        IncrementalStepRunReason.Removed)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(
                        FirstScopedMapper,
                        IncrementalStepRunReason.Cached),
                    Expected(
                        SecondScopedMapper,
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
    public partial class TupleMapper : TypeMapper<TupleMapper>
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
    public partial class StableMapper : TypeMapper<StableMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<StableSource, StableDestination>();
    }
}
""";

    // lang=c#
    private const string FirstScopedMapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class FirstMapper : TypeMapper<FirstMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<(int X, int Y), int>()
                .Convert(source => source.X + source.Y);
    }
}
""";

    // lang=c#
    private const string SecondScopedMapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class SecondMapper : TypeMapper<SecondMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<(int A, int B), int>()
                .Convert(source => source.A * source.B);
    }
}
""";
}
