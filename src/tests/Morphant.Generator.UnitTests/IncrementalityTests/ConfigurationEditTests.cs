using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class ConfigurationEditTests
{
    private const string ConstructionA =
        "Morphant.Generated.Construction.DestinationA__3733382c8803f06b6b6c3f9cba169e0f.g.cs";

    private const string ConstructionB =
        "Morphant.Generated.Construction.DestinationB__1f97cb9531b66ac2ca2a5923c972297b.g.cs";

    private const string MappingExtensionA =
        "Morphant.Generated.MappingExtension.MapperA.SourceAToDestinationA__db82dfe1a33c9cc6d08771569ec8eb46.g.cs";

    private const string MappingExtensionB =
        "Morphant.Generated.MappingExtension.MapperB.SourceBToDestinationB__ed707c749ae9ab2370bbe3005f6c1ef7.g.cs";

    private const string MemberA =
        "Morphant.Generated.Member.DestinationA__3733382c8803f06b6b6c3f9cba169e0f.g.cs";

    private const string MemberB =
        "Morphant.Generated.Member.DestinationB__1f97cb9531b66ac2ca2a5923c972297b.g.cs";

    private const string MemberExtensionA =
        "Morphant.Generated.MemberExtension.MapperA.SourceAToDestinationA__db82dfe1a33c9cc6d08771569ec8eb46.g.cs";

    private const string MemberExtensionB =
        "Morphant.Generated.MemberExtension.MapperB.SourceBToDestinationB__ed707c749ae9ab2370bbe3005f6c1ef7.g.cs";

    private const string MapperA =
        "Morphant.Generated.TypeMapper.MapperA__5e218501b1a45e633f93649296e15c01.g.cs";

    private const string MapperB =
        "Morphant.Generated.TypeMapper.MapperB__dca3e300d0a63d8240eb166c3130d2df.g.cs";

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
