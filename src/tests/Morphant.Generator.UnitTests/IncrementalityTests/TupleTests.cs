using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class TupleTests
{
    private const string OldConstruction =
        "Morphant.Generated.Construction.Tuple_" +
        "ValueTuple2_6a518f7e7fb5607c.g.cs";

    private const string NewConstruction =
        "Morphant.Generated.Construction.Tuple_" +
        "ValueTuple2_77f96889121eca12.g.cs";

    private const string OldMember =
        "Morphant.Generated.Member.Tuple_" +
        "ValueTuple2_6a518f7e7fb5607c.g.cs";

    private const string NewMember =
        "Morphant.Generated.Member.Tuple_" +
        "ValueTuple2_77f96889121eca12.g.cs";

    private const string TupleMappingExtension =
        "Morphant.Generated.MappingExtension." +
        "TestCase_TupleSource__" +
        "System_ValueTuple_System_Int32__System_String_.g.cs";

    private const string TupleMemberExtension =
        "Morphant.Generated.MemberExtension." +
        "TestCase_TupleSource__" +
        "System_ValueTuple_System_Int32__System_String_.g.cs";

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
                stableHints.Concat(new[]
                {
                    NewConstruction,
                    TupleMappingExtension,
                    NewMember,
                    TupleMemberExtension,
                    TupleMapper
                }).ToArray(),
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
                        IncrementalStepRunReason.Cached))));
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
}
