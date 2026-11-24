using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncludeMembersTests;

[TestFixture]
internal sealed class ActualizationTests
{
    private const string Mapper =
        "Morphant.Generated.TypeMapper.TestMapper__c4f37cd504cad969975e05a81c79a459.g.cs";

    private static readonly string[] GeneratedFiles =
    [
        "Morphant.Generated.Construction.Destination__adf0e0d9f318ac2567116b6b00dfb794.g.cs",
        "Morphant.Generated.MappingExtension.SourceToDestination__fd3791156a0b8842275fd74856c7bb41.g.cs",
        "Morphant.Generated.Member.Destination__adf0e0d9f318ac2567116b6b00dfb794.g.cs",
        "Morphant.Generated.MemberExtension.SourceToDestination__fd3791156a0b8842275fd74856c7bb41.g.cs",
        Mapper
    ];

    [Test]
    public void A_deep_selector_after_members_tracks_the_selected_flattening_contract()
    {
        var mapper = SourceFile("Mapper.cs", """
#nullable enable
using Morphant;
namespace TestCase
{
    [MorphantMapper] internal partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder b) => b.Map<Source?,Destination>()
            .Flattening(Flattening.Auto).Members(s => new() { Marker = s.Marker })
            .IncludeMembers(s => s.Envelope.Included);
    }
    internal class Source { public int Marker { get; } public Envelope Envelope { get; } = new(); }
    internal class Envelope { public Included Included { get; } = new(); }
    internal class Included { public Payload Payload { get; } = new(); }
    internal class Destination { public int Marker { get; set; } public int PayloadCode { get; set; } }
}
""");
        GeneratorIncrementalityStep Edit(string member, params ExpectedIncrementalStage[] stages) => Step(member,
            [mapper, SourceFile("Payload.cs", $"namespace TestCase {{ internal class Payload {{ public int {member} {{ get; }} }} }}")],
            GeneratedFiles, stages);
        var modified = Stage("BuildTypeMapperRequests", Expected(Mapper, IncrementalStepRunReason.Modified));
        RunAndAssert(LanguageVersion.CSharp9, static () => new MorphantGenerator(),
            Edit("Code"), Edit("Other", modified), Edit("Code", modified));
    }

    [Test]
    public void Actualizes_added_changed_and_removed_scopes()
    {
        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            ScenarioStep("no scope", string.Empty, -1, initial: true),
            ScenarioStep("left scope", "source.Left", 11),
            ScenarioStep("right scope", "source.Right", 13),
            ScenarioStep("scope removed", string.Empty, -1));
    }

    [Test]
    public void Actualizes_when_the_selected_type_contract_changes()
    {
        var stable = SourceFile(
            "MapperAndScenario.cs",
            ContractMapperAndScenarioSource);

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            ExecutableStep(
                "left member",
                [
                    stable,
                    SourceFile(
                        "Models.cs",
                        BuildContractModels("Left", 11))
                ],
                GeneratedFiles,
                "TestCase.ContractScenario"),
            ExecutableStep(
                "right member",
                [
                    stable,
                    SourceFile(
                        "Models.cs",
                        BuildContractModels("Right", 13))
                ],
                GeneratedFiles,
                "TestCase.ContractScenario",
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(
                        Mapper,
                        IncrementalStepRunReason.Modified))),
            ExecutableStep(
                "left member restored",
                [
                    stable,
                    SourceFile(
                        "Models.cs",
                        BuildContractModels("Left", 11))
                ],
                GeneratedFiles,
                "TestCase.ContractScenario",
                Stage(
                    "BuildTypeMapperRequests",
                    Expected(
                        Mapper,
                        IncrementalStepRunReason.Modified))));
    }

    private static string BuildContractModels(string memberName, int value) =>
        ContractModelsTemplate
            .Replace("__MEMBER__", memberName)
            .Replace("__VALUE__", value.ToString());

    private static GeneratorIncrementalityStep ScenarioStep(
        string name,
        string selector,
        int expected,
        bool initial = false)
    {
        var configuration = selector.Length == 0
            ? string.Empty
            : ".IncludeMembers(source => " + selector + ")";
        var source = SourceTemplate
            .Replace("__CONFIGURATION__", configuration)
            .Replace("__EXPECTED__", expected.ToString());

        return ExecutableStep(
            name,
            [SourceFile("TestCase.cs", source)],
            GeneratedFiles,
            "TestCase.Scenario",
            initial
                ? []
                :
                [
                    Stage(
                        "BuildTypeMapperRequests",
                        Expected(
                            Mapper,
                            IncrementalStepRunReason.Modified))
                ]);
    }

    // lang=c#
    private const string SourceTemplate =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
{
    public sealed class Source
    {
        public Details Left { get; init; } = new Details { Value = 11 };

        public Details Right { get; init; } = new Details { Value = 13 };
    }

    public sealed class Details
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; } = -1;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                __CONFIGURATION__;
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var result = mapper.Create(new Source());

            if (result.Value != __EXPECTED__)
            {
                throw new InvalidOperationException(
                    "IncludeMembers configuration was not actualized.");
            }
        }
    }
}
""";

    // lang=c#
    private const string ContractModelsTemplate =
"""
#nullable enable
#pragma warning disable CS1591

namespace TestCase
{
    public sealed class Source
    {
        public Details Details { get; init; } = new Details();
    }

    public sealed class Details
    {
        public int __MEMBER__ { get; init; } = __VALUE__;
    }

    public sealed class Destination
    {
        public int Left { get; set; } = -1;

        public int Right { get; set; } = -1;
    }
}
""";

    // lang=c#
    private const string ContractMapperAndScenarioSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .IncludeMembers(source => source.Details);
    }

    public static class ContractScenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var result = mapper.Create(new Source());
            var mapsLeft = result.Left == 11 && result.Right == -1;
            var mapsRight = result.Left == -1 && result.Right == 13;

            if (!mapsLeft && !mapsRight)
            {
                throw new InvalidOperationException(
                    "The selected nested contract was not actualized.");
            }
        }
    }
}
""";
}
