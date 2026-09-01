using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorIncrementalityTest;

namespace Morphant.Generator.UnitTests.IncrementalityTests;

[TestFixture]
internal sealed class CompilationBoundaryTests
{
    private static readonly string[] ValidGeneratedHints =
    [
        "Morphant.Generated.Construction.TestCase_Destination.g.cs",
        "Morphant.Generated.MappingExtension." +
        "TestCase_Source__TestCase_Destination.g.cs",
        "Morphant.Generated.Member.TestCase_Destination.g.cs",
        "Morphant.Generated.MemberExtension." +
        "TestCase_Source__TestCase_Destination.g.cs",
        "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs"
    ];

    private static readonly string[] SettingGeneratedHints =
    [
        "Morphant.Generated.Construction.First_Destination.g.cs",
        "Morphant.Generated.Construction.Second_Destination.g.cs",
        "Morphant.Generated.MappingExtension." +
        "First_Source__First_Destination.g.cs",
        "Morphant.Generated.MappingExtension." +
        "Second_Source__Second_Destination.g.cs",
        "Morphant.Generated.TypeMapper.First_FirstMapper.g.cs",
        "Morphant.Generated.TypeMapper.Second_SecondMapper.g.cs"
    ];

    [Test]
    public void Recreated_syntax_trees_keep_generated_artifacts_alive()
    {
        var files = new[]
        {
            SourceFile("ValidMapper.cs", ValidMapperSource)
        };

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            Step(
                "initial compilation",
                files,
                ValidGeneratedHints),
            StepWithRecreatedSyntaxTrees(
                "every syntax tree recreated",
                files,
                ValidGeneratedHints,
                [
                    .. EarlyPipeline(
                        Reason(IncrementalStepRunReason.Cached, 1)),
                    Stage(
                        "BuildConstructionPlanRequests",
                        Expected(
                            ValidGeneratedHints[0],
                            IncrementalStepRunReason.Cached)),
                    Stage(
                        "BuildMemberPlanRequests",
                        Expected(
                            ValidGeneratedHints[2],
                            IncrementalStepRunReason.Cached)),
                    Stage(
                        "BuildTypeMapperRequests",
                        Expected(
                            ValidGeneratedHints[4],
                            IncrementalStepRunReason.Cached))
                ]));
    }

    [Test]
    public void Recreated_syntax_trees_preserve_mapper_diagnostic_order()
    {
        var files = new[]
        {
            SourceFile("FirstMapper.cs", FirstInvalidMapperSource),
            SourceFile("SecondMapper.cs", SecondInvalidMapperSource)
        };
        var diagnostics = new[]
        {
            CompilerDiagnostic(
                "MORPH0006",
                DiagnosticSeverity.Error,
                "FirstMapper.cs",
                FirstInvalidMapperSource.IndexOf(
                    "FirstMapper :",
                    StringComparison.Ordinal),
                "FirstMapper".Length),
            CompilerDiagnostic(
                "MORPH0006",
                DiagnosticSeverity.Error,
                "SecondMapper.cs",
                SecondInvalidMapperSource.IndexOf(
                    "SecondMapper :",
                    StringComparison.Ordinal),
                "SecondMapper".Length)
        };

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            StepWithDiagnostics(
                "initial mapper diagnostics",
                files,
                [],
                diagnostics),
            StepWithRecreatedSyntaxTreesAndDiagnostics(
                "mapper diagnostic trees recreated",
                files,
                [],
                diagnostics));
    }

    [Test]
    public void Recreated_syntax_trees_preserve_setting_diagnostic_order()
    {
        var files = new[]
        {
            SourceFile(
                "FirstSettingsMapper.cs",
                FirstInvalidSettingSource),
            SourceFile(
                "SecondSettingsMapper.cs",
                SecondInvalidSettingSource)
        };
        var diagnostics = new[]
        {
            CompilerDiagnostic(
                "MORPH0021",
                DiagnosticSeverity.Error,
                "FirstSettingsMapper.cs",
                FirstInvalidSettingSource.LastIndexOf(
                    "invalid",
                    StringComparison.Ordinal),
                "invalid".Length),
            CompilerDiagnostic(
                "MORPH0021",
                DiagnosticSeverity.Error,
                "SecondSettingsMapper.cs",
                SecondInvalidSettingSource.LastIndexOf(
                    "invalid",
                    StringComparison.Ordinal),
                "invalid".Length)
        };

        RunAndAssert(
            LanguageVersion.CSharp9,
            static () => new MorphantGenerator(),
            StepWithDiagnostics(
                "initial setting diagnostics",
                files,
                SettingGeneratedHints,
                diagnostics),
            StepWithRecreatedSyntaxTreesAndDiagnostics(
                "setting diagnostic trees recreated",
                files,
                SettingGeneratedHints,
                diagnostics));
    }

    // lang=c#
    private const string ValidMapperSource =
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
        public int Value { get; set; }
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
    private const string FirstInvalidMapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace First
{
    // Keep this declaration later in its file than the declaration in the
    // second file. Diagnostic order must follow source-tree order, not span.
    public sealed class PaddingOne { }
    public sealed class PaddingTwo { }

    [MorphantMapper]
    public class FirstMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) { }
    }
}
""";

    // lang=c#
    private const string SecondInvalidMapperSource =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
namespace Second
{
    [MorphantMapper]
    public class SecondMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) { }
    }
}
""";

    // lang=c#
    private const string FirstInvalidSettingSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace First
{
    // Keep the invalid expression later in this file than the expression in
    // the second file. Diagnostic order follows source-tree order, not span.
    public sealed class PaddingOne { }
    public sealed class PaddingTwo { }

    public sealed class Source { }
    public sealed class Destination { }

    [MorphantMapper]
    public partial class FirstMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            var invalid = MemberSelection.Auto;
            builder.Map<Source, Destination>()
                .MemberSelection(invalid);
        }
    }
}
""";

    // lang=c#
    private const string SecondInvalidSettingSource =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;
namespace Second
{
    public sealed class Source { }
    public sealed class Destination { }
    [MorphantMapper]
    public partial class SecondMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            var invalid = MemberSelection.Auto;
            builder.Map<Source, Destination>().MemberSelection(invalid);
        }
    }
}
""";
}
