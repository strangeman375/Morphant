using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class CompilerInputTests
{
    [Test]
    public void Actualizes_a_contract_when_a_preprocessor_symbol_changes()
    {
        var source = SourceFile("TestCase.cs", PreprocessorSource);
        var generated = NonGenericHints();

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            StepWithCompilerInputs(
                "narrow contract",
                [source],
                generated,
                NullableContextOptions.Enable,
                []),
            StepWithCompilerInputs(
                "wide contract",
                [source],
                generated,
                NullableContextOptions.Enable,
                ["WIDE"],
                ChangedMemberStages(
                    "Morphant.Generated.Member." +
                    "TestCase_Destination.g.cs")),
            StepWithCompilerInputs(
                "narrow contract restored",
                [source],
                generated,
                NullableContextOptions.Enable,
                [],
                ChangedMemberStages(
                    "Morphant.Generated.Member." +
                    "TestCase_Destination.g.cs")));
    }

    [Test]
    public void Actualizes_generic_surfaces_when_nullable_context_changes()
    {
        var source = SourceFile("TestCase.cs", NullableContextSource);
        var generated = GenericHints();
        const string construction =
            "Morphant.Generated.Construction." +
            "TestCase_Destination_1.g.cs";
        const string member =
            "Morphant.Generated.Member.TestCase_Destination_1.g.cs";

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            StepWithCompilerInputs(
                "nullable annotations disabled",
                [source],
                generated,
                NullableContextOptions.Disable,
                []),
            StepWithCompilerInputs(
                "nullable annotations enabled",
                [source],
                generated,
                NullableContextOptions.Enable,
                [],
                ChangedPlanStages(construction, member)),
            StepWithCompilerInputs(
                "nullable annotations disabled again",
                [source],
                generated,
                NullableContextOptions.Disable,
                [],
                ChangedPlanStages(construction, member)));
    }

    private static string[] NonGenericHints()
    {
        return
        [
            "Morphant.Generated.Construction.TestCase_Destination.g.cs",
            "Morphant.Generated.MappingExtension." +
            "TestCase_Source__TestCase_Destination.g.cs",
            "Morphant.Generated.Member.TestCase_Destination.g.cs",
            "Morphant.Generated.MemberExtension." +
            "TestCase_Source__TestCase_Destination.g.cs",
            "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs"
        ];
    }

    private static string[] GenericHints()
    {
        return
        [
            "Morphant.Generated.Construction." +
            "TestCase_Destination_1.g.cs",
            "Morphant.Generated.MappingExtension." +
            "TestCase_Source_T___TestCase_Destination_T_.g.cs",
            "Morphant.Generated.Member.TestCase_Destination_1.g.cs",
            "Morphant.Generated.MemberExtension." +
            "TestCase_Source_T___TestCase_Destination_T_.g.cs",
            "Morphant.Generated.TypeMapper.TestCase_TestMapper_1.g.cs"
        ];
    }

    private static ExpectedIncrementalStage[] ChangedMemberStages(
        string member)
    {
        return
        [
            .. EarlyPipeline(
                Reason(IncrementalStepRunReason.Modified, 1)),
            Stage(
                "BuildMemberPlanModels",
                Expected(member, IncrementalStepRunReason.Modified)),
            Stage(
                "BuildMemberPlanRequests",
                Expected(member, IncrementalStepRunReason.Modified))
        ];
    }

    private static ExpectedIncrementalStage[] ChangedPlanStages(
        string construction,
        string member)
    {
        return
        [
            .. EarlyPipeline(
                Reason(IncrementalStepRunReason.Modified, 1)),
            Stage(
                "BuildConstructionPlanModels",
                Expected(
                    construction,
                    IncrementalStepRunReason.Modified)),
            Stage(
                "BuildConstructionPlanRequests",
                Expected(
                    construction,
                    IncrementalStepRunReason.Modified)),
            Stage(
                "BuildMemberPlanModels",
                Expected(member, IncrementalStepRunReason.Modified)),
            Stage(
                "BuildMemberPlanRequests",
                Expected(member, IncrementalStepRunReason.Modified))
        ];
    }

    // lang=c#
    private const string PreprocessorSource =
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

    public sealed class Destination
    {
#if WIDE
        public long Value { get; set; }
#else
        public int Value { get; set; }
#endif
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

    // lang=c#
    private const string NullableContextSource =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source<T>
        where T : class, System.IComparable<T>
    {
        public T Value { get; init; } = default!;
    }

    public sealed class Destination<T>
        where T : class, System.IComparable<T>
    {
        public T Value { get; set; } = default!;
    }

    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper
        where T : class, System.IComparable<T>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<T>, Destination<T>>();
    }
}
""";
}
