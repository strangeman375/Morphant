using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Morphant.Generator.UnitTests.NestedMappingDiagnosticsTests;

[TestFixture]
internal sealed class RecoveryAndActualizationTests
{
    private const string InvalidSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class ChildSource { }
    public sealed class ChildDestination { }
    public sealed class Source
    {
        public ChildSource Child { get; } = new();
        public int Value { get; set; }
    }

    public sealed class AUnknown
    {
        public ChildDestination? Child { get; set; }
    }

    public sealed class BResult
    {
        public string Text { get; set; } = string.Empty;
    }

    public sealed class CUpdate
    {
        public ChildDestination? Child { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, AUnknown>()
                .Members(source => new() { Child = Map(null) });
            builder.Map<Source, BResult>()
                .Members(source => new() { Text = Map<int>(source.Value) });
            builder.Map<Source, CUpdate>()
                .Members(source => new()
                {
                    Child = Update<ChildDestination>(
                        source.Child,
                        new object())
                });
        }
    }
}
""";

    [Test]
    public void Suppression_and_severity_do_not_change_recovery_artifacts()
    {
        var visible = NestedMappingDiagnosticsGeneratorTest.Run(InvalidSource);
        var suppressed = NestedMappingDiagnosticsGeneratorTest.Run(
            InvalidSource,
            diagnosticOptions: Options(ReportDiagnostic.Suppress));
        var warning = NestedMappingDiagnosticsGeneratorTest.Run(
            InvalidSource,
            diagnosticOptions: Options(ReportDiagnostic.Warn));

        Assert.Multiple(() =>
        {
            Assert.That(
                visible.NestedMappingDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0044",
                    "MORPH0045",
                    "MORPH0046"
                }));
            Assert.That(suppressed.EffectiveDiagnostics, Is.Empty);
            Assert.That(
                warning.NestedMappingDiagnostics.Select(static diagnostic =>
                    diagnostic.Severity),
                Is.All.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(Sources(suppressed), Is.EqualTo(Sources(visible)));
            Assert.That(Sources(warning), Is.EqualTo(Sources(visible)));
            Assert.That(visible.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(suppressed.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(warning.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Actualizes_pair_inference_and_recovery_on_one_driver()
    {
        var invalid = NestedMappingDiagnosticsGeneratorTest.Run(
            InvalidSource);
        var valid = NestedMappingDiagnosticsGeneratorTest.Run(
            InvalidSource.Replace(
                "Map(null)",
                "Map<ChildDestination>(source.Child)",
                StringComparison.Ordinal),
            driver: invalid.Driver);
        var invalidAgain = NestedMappingDiagnosticsGeneratorTest.Run(
            InvalidSource,
            driver: valid.Driver);

        Assert.Multiple(() =>
        {
            Assert.That(
                invalid.NestedMappingDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Does.Contain("MORPH0044"));
            Assert.That(
                valid.NestedMappingDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Does.Not.Contain("MORPH0044"));
            Assert.That(
                invalidAgain.TypeMapperSource,
                Is.EqualTo(invalid.TypeMapperSource));
            Assert.That(
                valid.TypeMapperSource,
                Is.Not.EqualTo(invalid.TypeMapperSource));
            Assert.That(invalid.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(valid.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(invalidAgain.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Actualizes_target_and_current_slot_types_on_one_driver()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class ChildSource { }
    public sealed class ChildDestination { }

    public sealed class Source
    {
        public ChildSource Child { get; } = new();
        public int Number { get; set; }
    }

    public sealed class ResultDestination
    {
        public object Child { get; set; } = default!;
    }

    public sealed class CurrentDestination
    {
        public object Number { get; set; } = default!;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ResultDestination>(MappingMode.Create)
                .Members(value => new()
                {
                    Child = Map<ChildDestination>(value.Child)
                });
            builder.Map<Source, CurrentDestination>()
                .Members(value => new()
                {
                    Number = Map<int>(value.Number)
                });
        }
    }
}
""";

        var valid = NestedMappingDiagnosticsGeneratorTest.Run(source);
        var incompatibleResult = NestedMappingDiagnosticsGeneratorTest.Run(
            source.Replace(
                "public object Child",
                "public string Child",
                StringComparison.Ordinal),
            driver: valid.Driver);
        var impossibleCurrent = NestedMappingDiagnosticsGeneratorTest.Run(
            source.Replace(
                "public object Number",
                "public long Number",
                StringComparison.Ordinal),
            driver: incompatibleResult.Driver);
        var validAgain = NestedMappingDiagnosticsGeneratorTest.Run(
            source,
            driver: impossibleCurrent.Driver);

        Assert.Multiple(() =>
        {
            Assert.That(valid.NestedMappingDiagnostics, Is.Empty);
            Assert.That(
                incompatibleResult.NestedMappingDiagnostics.Select(
                    static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0045" }));
            Assert.That(
                impossibleCurrent.NestedMappingDiagnostics.Select(
                    static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0046" }));
            Assert.That(
                Sources(validAgain),
                Is.EqualTo(Sources(valid)));
            Assert.That(
                Sources(incompatibleResult),
                Is.Not.EqualTo(Sources(valid)));
            Assert.That(
                Sources(impossibleCurrent),
                Is.Not.EqualTo(Sources(valid)));
            Assert.That(valid.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(
                incompatibleResult.CompilerWarningsAndErrors,
                Is.Empty);
            Assert.That(
                impossibleCurrent.CompilerWarningsAndErrors,
                Is.Empty);
            Assert.That(validAgain.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    private static IReadOnlyDictionary<string, ReportDiagnostic> Options(
        ReportDiagnostic value)
    {
        return new Dictionary<string, ReportDiagnostic>(StringComparer.Ordinal)
        {
            ["MORPH0044"] = value,
            ["MORPH0045"] = value,
            ["MORPH0046"] = value
        };
    }

    private static string[] Sources(
        NestedMappingDiagnosticsGeneratorResult result)
    {
        return result.GeneratedSources
            .OrderBy(static source => source.HintName, StringComparer.Ordinal)
            .Select(static source =>
                source.HintName + "\n" + source.SourceText)
            .ToArray();
    }
}
