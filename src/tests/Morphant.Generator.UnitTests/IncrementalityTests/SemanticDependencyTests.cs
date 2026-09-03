using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class SemanticDependencyTests
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

    private static readonly string[] Generated =
    [
        Construction,
        MappingExtension,
        Member,
        MemberExtension,
        Mapper
    ];

    private static readonly string[] GeneratedWithoutMembers =
    [
        Construction,
        MappingExtension,
        Mapper
    ];

    [Test]
    public void Actualizes_a_contract_when_an_alias_changes_its_type()
    {
        var mapper = SourceFile("Mapper.cs", MapperSource);

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            Step(
                "int alias",
                [mapper, SourceFile("Models.cs", BuildModels("Int32"))],
                Generated),
            Step(
                "long alias",
                [mapper, SourceFile("Models.cs", BuildModels("Int64"))],
                Generated,
                ChangedContractStages()),
            Step(
                "int alias restored",
                [mapper, SourceFile("Models.cs", BuildModels("Int32"))],
                Generated,
                ChangedContractStages()));
    }

    [Test]
    public void Actualizes_an_external_constant_used_as_a_default_value()
    {
        var models = SourceFile("Models.cs", DefaultValueModelsSource);
        var mapper = SourceFile("Mapper.cs", MapperSource);

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            Step(
                "default value one",
                [models, mapper, SourceFile("Defaults.cs", BuildDefaults(1))],
                GeneratedWithoutMembers),
            Step(
                "default value two",
                [models, mapper, SourceFile("Defaults.cs", BuildDefaults(2))],
                GeneratedWithoutMembers,
                [
                    .. EarlyPipeline(
                        Reason(IncrementalStepRunReason.Modified, 1)),
                    Stage(
                        "BuildConstructionPlanModels",
                        Expected(
                            Construction,
                            IncrementalStepRunReason.Modified)),
                    Stage(
                        "BuildConstructionPlanRequests",
                        Expected(
                            Construction,
                            IncrementalStepRunReason.Modified))
                ]),
            Step(
                "default value one restored",
                [models, mapper, SourceFile("Defaults.cs", BuildDefaults(1))],
                GeneratedWithoutMembers,
                [
                    .. EarlyPipeline(
                        Reason(IncrementalStepRunReason.Modified, 1)),
                    Stage(
                        "BuildConstructionPlanModels",
                        Expected(
                            Construction,
                            IncrementalStepRunReason.Modified)),
                    Stage(
                        "BuildConstructionPlanRequests",
                        Expected(
                            Construction,
                            IncrementalStepRunReason.Modified))
                ]));
    }

    [Test]
    public void Reanalyzes_a_callback_when_an_external_constant_changes()
    {
        var models = SourceFile("Models.cs", CallbackModelsSource);
        var mapper = SourceFile("Mapper.cs", CallbackMapperSource);

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            Step(
                "callback offset one",
                [models, mapper, SourceFile("Defaults.cs", BuildDefaults(1))],
                Generated),
            Step(
                "callback offset two",
                [models, mapper, SourceFile("Defaults.cs", BuildDefaults(2))],
                Generated,
                ChangedCallbackStages()),
            Step(
                "callback offset one restored",
                [models, mapper, SourceFile("Defaults.cs", BuildDefaults(1))],
                Generated,
                ChangedCallbackStages()));
    }

    private static ExpectedIncrementalStage[] ChangedCallbackStages()
    {
        return EarlyPipeline(
            Reason(IncrementalStepRunReason.Modified, 1));
    }

    private static ExpectedIncrementalStage[] ChangedContractStages()
    {
        return
        [
            .. EarlyPipeline(
                Reason(IncrementalStepRunReason.Modified, 1)),
            Stage(
                "BuildConstructionPlanModels",
                Expected(
                    Construction,
                    IncrementalStepRunReason.Modified)),
            Stage(
                "BuildConstructionPlanRequests",
                Expected(
                    Construction,
                    IncrementalStepRunReason.Modified)),
            Stage(
                "BuildMemberPlanModels",
                Expected(Member, IncrementalStepRunReason.Modified)),
            Stage(
                "BuildMemberPlanRequests",
                Expected(Member, IncrementalStepRunReason.Modified))
        ];
    }

    private static string BuildModels(string aliasTarget)
    {
        return AliasModelsSource.Replace("__ALIAS_TARGET__", aliasTarget);
    }

    private static string BuildDefaults(int value)
    {
        return DefaultsSource.Replace("__VALUE__", value.ToString());
    }

    // lang=c#
    private const string AliasModelsSource =
"""
#nullable enable
#pragma warning disable CS1591

using ContractValue = System.__ALIAS_TARGET__;

namespace TestCase
{
    public sealed class Source
    {
        public int Initial { get; init; }

        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public Destination(ContractValue initial) => Value = initial;

        public ContractValue Value { get; set; }
    }
}
""";

    // lang=c#
    private const string DefaultValueModelsSource =
"""
#nullable enable
#pragma warning disable CS1591

namespace TestCase
{
    public sealed class Source
    {
    }

    public sealed class Destination
    {
        public Destination(int value = Defaults.Value) => Value = value;

        public int Value { get; }
    }
}
""";

    // lang=c#
    private const string DefaultsSource =
"""
#nullable enable
#pragma warning disable CS1591

namespace TestCase
{
    internal static class Defaults
    {
        public const int Value = __VALUE__;
    }
}
""";

    // lang=c#
    private const string MapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

    // lang=c#
    private const string CallbackModelsSource =
"""
#nullable enable
#pragma warning disable CS1591

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
}
""";

    // lang=c#
    private const string CallbackMapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((source, _) => new()
                {
                    Value = source.Value + Defaults.Value
                });
    }
}
""";
}
