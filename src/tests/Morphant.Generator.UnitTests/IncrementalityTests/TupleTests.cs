using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class TupleTests
{
    private const string OldConstruction =
        "Morphant.Generated.Construction.Tuple_" +
        "V2_a51caaf0c27a1203d7dd02a67a0a5455.g.cs";

    private const string NewConstruction =
        "Morphant.Generated.Construction.Tuple_" +
        "V2_0b27a687eedd668df8361ccda86185ba.g.cs";

    private const string OldMember =
        "Morphant.Generated.Member.Tuple_" +
        "V2_a51caaf0c27a1203d7dd02a67a0a5455.g.cs";

    private const string NewMember =
        "Morphant.Generated.Member.Tuple_" +
        "V2_0b27a687eedd668df8361ccda86185ba.g.cs";

    private const string TupleMappingExtension =
        "Morphant.Generated.MappingExtension." +
        "TestCase_TupleSource__" +
        "System_ValueTuple_System_Int32__System_String___" +
        "TestCase_TupleMapper.g.cs";

    private const string TupleMemberExtension =
        "Morphant.Generated.MemberExtension." +
        "TestCase_TupleSource__" +
        "System_ValueTuple_System_Int32__System_String___" +
        "TestCase_TupleMapper.g.cs";

    private const string TupleMapper =
        "Morphant.Generated.TypeMapper.TestCase_TupleMapper.g.cs";

    private const string StableConstruction =
        "Morphant.Generated.Construction." +
        "TestCase_StableDestination.g.cs";

    private const string StableMappingExtension =
        "Morphant.Generated.MappingExtension." +
        "TestCase_StableSource__TestCase_StableDestination.g.cs";

    private const string StableMember =
        "Morphant.Generated.Member.TestCase_StableDestination.g.cs";

    private const string StableMemberExtension =
        "Morphant.Generated.MemberExtension." +
        "TestCase_StableSource__TestCase_StableDestination.g.cs";

    private const string StableMapper =
        "Morphant.Generated.TypeMapper.TestCase_StableMapper.g.cs";

    private const string FirstScopedMappingExtension =
        "Morphant.Generated.MappingExtension." +
        "System_ValueTuple_System_Int32__System_Int32___System_Int32__" +
        "TestCase_FirstMapper.g.cs";

    private const string SecondScopedMappingExtension =
        "Morphant.Generated.MappingExtension." +
        "System_ValueTuple_System_Int32__System_Int32___System_Int32__" +
        "TestCase_SecondMapper.g.cs";

    private const string FirstScopedMapper =
        "Morphant.Generated.TypeMapper.TestCase_FirstMapper.g.cs";

    private const string SecondScopedMapper =
        "Morphant.Generated.TypeMapper.TestCase_SecondMapper.g.cs";

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
            TupleMappingExtension,
            NewMember,
            TupleMemberExtension,
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
                    TupleMappingExtension,
                    OldMember,
                    TupleMemberExtension,
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
                        TupleMappingExtension,
                        IncrementalStepRunReason.Modified),
                    Expected(
                        StableMappingExtension,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMemberExtensionRequests",
                    Expected(
                        TupleMemberExtension,
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
                            TupleMappingExtension,
                            IncrementalStepRunReason.Cached),
                        Expected(
                            StableMappingExtension,
                            IncrementalStepRunReason.Cached)),
                    Stage(
                        "BuildMemberExtensionRequests",
                        Expected(
                            TupleMemberExtension,
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
