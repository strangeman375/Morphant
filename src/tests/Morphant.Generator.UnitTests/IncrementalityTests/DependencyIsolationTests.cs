using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class DependencyIsolationTests
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
    public void Isolates_unrelated_declarations_bodies_and_type_contracts()
    {
        var mapperFiles = new[]
        {
            SourceFile("MapperA.cs", MapperASource),
            SourceFile("MapperB.cs", MapperBSource)
        };
        var initialFiles = mapperFiles
            .Append(
                SourceFile(
                    "Destinations.cs",
                    BuildDestinationsSource("int", 1, 1)))
            .ToArray();
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
                "initial contracts",
                initialFiles,
                generated,
                Stage(
                    "BuildConstructionPlanModels",
                    Expected(ConstructionA, IncrementalStepRunReason.New),
                    Expected(ConstructionB, IncrementalStepRunReason.New)),
                Stage(
                    "BuildMemberPlanModels",
                    Expected(MemberA, IncrementalStepRunReason.New),
                    Expected(MemberB, IncrementalStepRunReason.New)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(MapperA, IncrementalStepRunReason.New),
                    Expected(MapperB, IncrementalStepRunReason.New))),
            Step(
                "unrelated declaration changed in shared file",
                mapperFiles
                    .Append(
                        SourceFile(
                            "Destinations.cs",
                            BuildDestinationsSource("int", 1, 2)))
                    .ToArray(),
                generated,
                Stage(
                    "BuildConstructionPlanModels",
                    Expected(ConstructionA, IncrementalStepRunReason.Cached),
                    Expected(ConstructionB, IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildConstructionPlanRequests",
                    Expected(ConstructionA, IncrementalStepRunReason.Cached),
                    Expected(ConstructionB, IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMemberPlanModels",
                    Expected(MemberA, IncrementalStepRunReason.Cached),
                    Expected(MemberB, IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMemberPlanRequests",
                    Expected(MemberA, IncrementalStepRunReason.Cached),
                    Expected(MemberB, IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(MapperA, IncrementalStepRunReason.Cached),
                    Expected(MapperB, IncrementalStepRunReason.Cached))),
            Step(
                "unmapped method body changed",
                mapperFiles
                    .Append(
                        SourceFile(
                            "Destinations.cs",
                            BuildDestinationsSource("int", 2, 2)))
                    .ToArray(),
                generated,
                Stage(
                    "BuildConstructionPlanModels",
                    Expected(
                        ConstructionA,
                        IncrementalStepRunReason.Unchanged),
                    Expected(ConstructionB, IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildConstructionPlanRequests",
                    Expected(ConstructionA, IncrementalStepRunReason.Cached),
                    Expected(ConstructionB, IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMemberPlanModels",
                    Expected(MemberA, IncrementalStepRunReason.Unchanged),
                    Expected(MemberB, IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMemberPlanRequests",
                    Expected(MemberA, IncrementalStepRunReason.Cached),
                    Expected(MemberB, IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(MapperA, IncrementalStepRunReason.Cached),
                    Expected(MapperB, IncrementalStepRunReason.Cached))),
            Step(
                "one destination contract changed",
                mapperFiles
                    .Append(
                        SourceFile(
                            "Destinations.cs",
                            BuildDestinationsSource("long", 2, 2)))
                    .ToArray(),
                generated,
                Stage(
                    "BuildConstructionPlanModels",
                    Expected(
                        ConstructionA,
                        IncrementalStepRunReason.Modified),
                    Expected(ConstructionB, IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildConstructionPlanRequests",
                    Expected(
                        ConstructionA,
                        IncrementalStepRunReason.Modified),
                    Expected(ConstructionB, IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMemberPlanModels",
                    Expected(MemberA, IncrementalStepRunReason.Modified),
                    Expected(MemberB, IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMemberPlanRequests",
                    Expected(MemberA, IncrementalStepRunReason.Modified),
                    Expected(MemberB, IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMappingExtensionRequests",
                    Expected(
                        MappingExtensionA,
                        IncrementalStepRunReason.Cached),
                    Expected(
                        MappingExtensionB,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildMemberExtensionRequests",
                    Expected(
                        MemberExtensionA,
                        IncrementalStepRunReason.Cached),
                    Expected(
                        MemberExtensionB,
                        IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildTypeMapperModels",
                    Expected(MapperA, IncrementalStepRunReason.Cached),
                    Expected(MapperB, IncrementalStepRunReason.Cached)),
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(MapperA, IncrementalStepRunReason.Cached),
                    Expected(MapperB, IncrementalStepRunReason.Cached))));
    }

    private static string BuildDestinationsSource(
        string valueType,
        int methodVersion,
        int unrelatedVersion)
    {
        return DestinationsSource
            .Replace("__VALUE_TYPE__", valueType)
            .Replace("__METHOD_VERSION__", methodVersion.ToString())
            .Replace("__UNRELATED_VERSION__", unrelatedVersion.ToString());
    }

    // lang=c#
    private const string MapperASource =
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

    [MorphantMapper]
    public partial class MapperA : TypeMapper<MapperA>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<SourceA, DestinationA>();
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
    public sealed class SourceB
    {
        public string Name { get; init; } = string.Empty;
    }

    [MorphantMapper]
    public partial class MapperB : TypeMapper<MapperB>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<SourceB, DestinationB>();
    }
}
""";

    // lang=c#
    private const string DestinationsSource =
"""
#nullable enable
#pragma warning disable CS1591

namespace TestCase
{
    public sealed class DestinationA
    {
        public DestinationA(__VALUE_TYPE__ value) => Value = value;

        public __VALUE_TYPE__ Value { get; }

        public __VALUE_TYPE__ MutableValue { get; set; }

        public int UnmappedMethod() => __METHOD_VERSION__;
    }

    public sealed class DestinationB
    {
        public DestinationB(string name) => Name = name;

        public string Name { get; }

        public string MutableName { get; set; } = string.Empty;
    }

    internal static class Unrelated
    {
        public const int Version = __UNRELATED_VERSION__;
    }
}
""";
}
