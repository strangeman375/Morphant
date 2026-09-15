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
        "Morphant.Generated.Construction.Destination__adf0e0d9f318ac2567116b6b00dfb794.g.cs",
        "Morphant.Generated.MappingExtension.TestMapper.SourceToDestination__2087052f5a3645b79062a3a095263f92.g.cs",
        "Morphant.Generated.Member.Destination__adf0e0d9f318ac2567116b6b00dfb794.g.cs",
        "Morphant.Generated.MemberExtension.TestMapper.SourceToDestination__2087052f5a3645b79062a3a095263f92.g.cs",
        "Morphant.Generated.TypeMapper.TestMapper__c4f37cd504cad969975e05a81c79a459.g.cs"
    ];

    private static readonly string[] SettingGeneratedHints =
    [
        "Morphant.Generated.Construction.Destination__5922229893d00b089d666c239ec2b1b0.g.cs",
        "Morphant.Generated.Construction.Destination__8ee05995dd9094f3e70bf0f7c0a6baa1.g.cs",
        "Morphant.Generated.MappingExtension.FirstMapper.SourceToDestination__ac0501f16e94748795bd8e9af42a414f.g.cs",
        "Morphant.Generated.MappingExtension.SecondMapper.SourceToDestination__02742474f7a51f524265dc5ede7f7758.g.cs",
        "Morphant.Generated.TypeMapper.FirstMapper__29796a8377d78c922212687ea45e9969.g.cs",
        "Morphant.Generated.TypeMapper.SecondMapper__955140c4d572867922f378f0e01f2a82.g.cs"
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
    public partial class TestMapper : TypeMapper<TestMapper>
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
    public class FirstMapper : TypeMapper<FirstMapper>
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
    public class SecondMapper : TypeMapper<SecondMapper>
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
    public partial class FirstMapper : TypeMapper<FirstMapper>
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
    public partial class SecondMapper : TypeMapper<SecondMapper>
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
