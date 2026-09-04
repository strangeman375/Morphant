using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.UnitTests.MemberDiagnosticsTests;

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
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class AInvalidRule
    {
        public int Missing { get; set; }
    }

    public sealed class BRequired
    {
        public required int Value { get; init; }
    }

    public sealed class CLifecycle
    {
        public int Value { get; init; }
    }

    public sealed class DNullPlan
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, AInvalidRule>()
                .Members(source => new() { Missing = Auto() });
            builder.Map<Source, BRequired>()
                .MemberSelection(MemberSelection.Explicit);
            builder.Map<Source, CLifecycle>()
                .ConstructUsing(source => new CLifecycle())
                .Members(source => new() { Value = source.Value });
            builder.Map<Source, DNullPlan>()
                .Members(source => default!);
        }
    }
}
""";

    [Test]
    public void Suppression_and_severity_do_not_change_recovery_artifacts()
    {
        var visible = MemberDiagnosticsGeneratorTest.Run(
            InvalidSource,
            LanguageVersion.CSharp11);
        var suppressed = MemberDiagnosticsGeneratorTest.Run(
            InvalidSource,
            LanguageVersion.CSharp11,
            Options(ReportDiagnostic.Suppress));
        var warning = MemberDiagnosticsGeneratorTest.Run(
            InvalidSource,
            LanguageVersion.CSharp11,
            Options(ReportDiagnostic.Warn));

        Assert.Multiple(() =>
        {
            Assert.That(
                visible.MemberDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0040",
                    "MORPH0041",
                    "MORPH0042",
                    "MORPH0043"
                }));
            Assert.That(suppressed.EffectiveDiagnostics, Is.Empty);
            Assert.That(
                warning.MemberDiagnostics.Select(static diagnostic =>
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
    public void Actualizes_a_null_terminal_and_its_recovery_on_one_driver()
    {
        // lang=c#
        const string validSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }
    public sealed class Destination { public int Value { get; set; } }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new());
    }
}
""";
        // lang=c#
        const string invalidSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }
    public sealed class Destination { public int Value { get; set; } }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => default!);
    }
}
""";

        var invalid = MemberDiagnosticsGeneratorTest.Run(invalidSource);
        var valid = MemberDiagnosticsGeneratorTest.Run(
            validSource,
            driver: invalid.Driver);
        var invalidAgain = MemberDiagnosticsGeneratorTest.Run(
            invalidSource,
            driver: valid.Driver);

        Assert.Multiple(() =>
        {
            Assert.That(
                invalid.MemberDiagnostics.Single().Id,
                Is.EqualTo("MORPH0043"));
            Assert.That(valid.MemberDiagnostics, Is.Empty);
            Assert.That(
                invalidAgain.MemberDiagnostics.Single().Id,
                Is.EqualTo("MORPH0043"));
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
    public void Earlier_callback_failure_suppresses_member_analysis()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }
    public sealed class Destination { public int Value { get; set; } }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(Build);

        private static global::Morphant.Generated.Types.N_TestCase.Plans.DestinationMembers
            Build(Source source) => null!;
    }
}
""";

        var result = MemberDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0029" }));
            Assert.That(result.MemberDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Independent_construction_failure_suppresses_member_analysis()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }
    public interface Destination { int Value { get; set; } }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>(MappingMode.Create)
                .Members(source => default!);
    }
}
""";

        var result = MemberDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0035" }));
            Assert.That(result.MemberDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    private static IReadOnlyDictionary<string, ReportDiagnostic> Options(
        ReportDiagnostic value)
    {
        return Enumerable.Range(40, 4)
            .ToDictionary(
                static id => $"MORPH{id:0000}",
                _ => value,
                StringComparer.Ordinal);
    }

    private static (string HintName, string Source)[] Sources(
        MemberDiagnosticsGeneratorResult result)
    {
        return result.GeneratedSources
            .Select(static source => (
                source.HintName,
                source.SourceText.ToString()))
            .OrderBy(static source => source.HintName, StringComparer.Ordinal)
            .ToArray();
    }
}
