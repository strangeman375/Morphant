using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class ConfigurationEditTests
{
    private const string ConstructionA =
        "Morphant.Generated.Construction.TestCase_DestinationA.g.cs";

    private const string ConstructionB =
        "Morphant.Generated.Construction.TestCase_DestinationB.g.cs";

    private const string MappingExtensionA =
        "Morphant.Generated.MappingExtension." +
        "TestCase_SourceA__TestCase_DestinationA.g.cs";

    private const string MappingExtensionB =
        "Morphant.Generated.MappingExtension." +
        "TestCase_SourceB__TestCase_DestinationB.g.cs";

    private const string MemberA =
        "Morphant.Generated.Member.TestCase_DestinationA.g.cs";

    private const string MemberB =
        "Morphant.Generated.Member.TestCase_DestinationB.g.cs";

    private const string MemberExtensionA =
        "Morphant.Generated.MemberExtension." +
        "TestCase_SourceA__TestCase_DestinationA.g.cs";

    private const string MemberExtensionB =
        "Morphant.Generated.MemberExtension." +
        "TestCase_SourceB__TestCase_DestinationB.g.cs";

    private const string MapperA =
        "Morphant.Generated.TypeMapper.TestCase_MapperA.g.cs";

    private const string MapperB =
        "Morphant.Generated.TypeMapper.TestCase_MapperB.g.cs";

    [Test]
    public void Rebuilds_only_the_mapper_whose_callback_changed()
    {
        var stableFiles = new[]
        {
            SourceFile("Models.cs", ModelsSource),
            SourceFile("MapperB.cs", MapperBSource)
        };
        var generated = new[]
        {
            ConstructionA,
            ConstructionB,
            MappingExtensionA,
            MappingExtensionB,
            MemberA,
            MemberB,
            MemberExtensionA,
            MemberExtensionB,
            MapperA,
            MapperB
        };

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            Step(
                "initial callbacks",
                stableFiles
                    .Append(SourceFile("MapperA.cs", BuildMapperA(1)))
                    .ToArray(),
                generated,
                Stage(
                    "BuildConstructionPlanRequests",
                    Expected(ConstructionA, IncrementalStepRunReason.New),
                    Expected(ConstructionB, IncrementalStepRunReason.New)),
                Stage(
                    "BuildMemberPlanRequests",
                    Expected(MemberA, IncrementalStepRunReason.New),
                    Expected(MemberB, IncrementalStepRunReason.New)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(MapperA, IncrementalStepRunReason.New),
                    Expected(MapperB, IncrementalStepRunReason.New))),
            Step(
                "one callback changed",
                stableFiles
                    .Append(SourceFile("MapperA.cs", BuildMapperA(2)))
                    .ToArray(),
                generated,
                [
                    .. EarlyPipeline(
                        Reason(IncrementalStepRunReason.Modified, 1),
                        Reason(IncrementalStepRunReason.Cached, 1)),
                    Stage(
                        "BuildConstructionPlanRequests",
                        Expected(
                            ConstructionA,
                            IncrementalStepRunReason.Cached),
                        Expected(
                            ConstructionB,
                            IncrementalStepRunReason.Cached)),
                    Stage(
                        "BuildMappingExtensionRequests",
                        Expected(
                            MappingExtensionA,
                            IncrementalStepRunReason.Cached),
                        Expected(
                            MappingExtensionB,
                            IncrementalStepRunReason.Cached)),
                    Stage(
                        "BuildMemberPlanRequests",
                        Expected(MemberA, IncrementalStepRunReason.Cached),
                        Expected(MemberB, IncrementalStepRunReason.Cached)),
                    Stage(
                        "BuildMemberExtensionRequests",
                        Expected(
                            MemberExtensionA,
                            IncrementalStepRunReason.Cached),
                        Expected(
                            MemberExtensionB,
                            IncrementalStepRunReason.Cached)),
                    Stage(
                        "BuildTypeMapperRequests",
                        Expected(MapperA, IncrementalStepRunReason.Modified),
                        Expected(MapperB, IncrementalStepRunReason.Cached))
                ]));
    }

    private static string BuildMapperA(int delta) =>
        MapperASource.Replace("__DELTA__", delta.ToString());

    // lang=c#
    private const string ModelsSource =
"""
#nullable enable
#pragma warning disable CS1591

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
        public int Value { get; init; }
    }

    public sealed class DestinationB
    {
        public int Value { get; set; }
    }
}
""";

    // lang=c#
    private const string MapperASource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class MapperA : TypeMapper<MapperA>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<SourceA, DestinationA>()
                .Members((source, _) => new()
                {
                    Value = source.Value + __DELTA__
                });
    }
}
""";

    // lang=c#
    private const string MapperBSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class MapperB : TypeMapper<MapperB>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<SourceB, DestinationB>();
    }
}
""";
}
