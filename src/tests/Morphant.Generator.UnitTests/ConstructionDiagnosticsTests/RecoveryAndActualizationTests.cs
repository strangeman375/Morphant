using Microsoft.CodeAnalysis;

namespace Morphant.Generator.UnitTests.ConstructionDiagnosticsTests;

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
    public sealed class Source { }

    public interface AMissing { }

    public sealed class BConvention { }

    public sealed class CRule
    {
        public CRule(int missing) { }
    }

    public sealed class DPrevious { }

    public sealed class ENull { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, AMissing>();
            builder.Map<Source, BConvention>()
                .ConstructorSelection(ConstructorSelection.Explicit);
            builder.Map<Source, CRule>()
                .Construct(source => new(Auto()));
            builder.Map<Source, DPrevious>()
                .Resolve((source, previous) => previous);
            builder.Map<Source, ENull>()
                .Construct(source => default!);
        }
    }
}
""";

    [Test]
    public void Suppression_and_severity_do_not_change_recovery_artifacts()
    {
        var visible = ConstructionDiagnosticsGeneratorTest.Run(InvalidSource);
        var suppressed = ConstructionDiagnosticsGeneratorTest.Run(
            InvalidSource,
            diagnosticOptions: Options(ReportDiagnostic.Suppress));
        var warning = ConstructionDiagnosticsGeneratorTest.Run(
            InvalidSource,
            diagnosticOptions: Options(ReportDiagnostic.Warn));
        var visibleSources = Sources(visible);
        var suppressedSources = Sources(suppressed);
        var warningSources = Sources(warning);

        Assert.Multiple(() =>
        {
            Assert.That(
                visible.ConstructionDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0035",
                    "MORPH0036",
                    "MORPH0037",
                    "MORPH0038",
                    "MORPH0039"
                }));
            Assert.That(suppressed.EffectiveDiagnostics, Is.Empty);
            Assert.That(
                warning.ConstructionDiagnostics.Select(static diagnostic =>
                    diagnostic.Severity),
                Is.All.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(suppressedSources, Is.EqualTo(visibleSources));
            Assert.That(warningSources, Is.EqualTo(visibleSources));
            Assert.That(
                Count(
                    visible.TypeMapperSource,
                    "throw new global::Morphant.Exceptions." +
                    "MappingConfigurationException("),
                Is.EqualTo(5));
            Assert.That(
                visible.TypeMapperSource,
                Does.Contain(
                    "No destination construction is configured."));
            Assert.That(
                visible.TypeMapperSource,
                Does.Contain(
                    "Morphant cannot select a constructor for this " +
                    "destination."));
            Assert.That(
                visible.TypeMapperSource,
                Does.Contain(
                    "'previous' is not available in this case."));
            Assert.That(visible.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(suppressed.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(warning.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Actualizes_terminal_diagnostic_and_throwing_leaf_on_one_driver()
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
    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new());
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
    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => default!);
    }
}
""";

        var invalid = ConstructionDiagnosticsGeneratorTest.Run(invalidSource);
        var valid = ConstructionDiagnosticsGeneratorTest.Run(
            validSource,
            driver: invalid.Driver);
        var invalidAgain = ConstructionDiagnosticsGeneratorTest.Run(
            invalidSource,
            driver: valid.Driver);

        Assert.Multiple(() =>
        {
            Assert.That(
                invalid.ConstructionDiagnostics.Single().Id,
                Is.EqualTo("MORPH0039"));
            Assert.That(valid.ConstructionDiagnostics, Is.Empty);
            Assert.That(
                invalidAgain.ConstructionDiagnostics.Single().Id,
                Is.EqualTo("MORPH0039"));
            Assert.That(
                invalid.TypeMapperSource,
                Does.Contain(
                    "throw new global::Morphant.Exceptions." +
                    "MappingConfigurationException("));
            Assert.That(
                valid.TypeMapperSource,
                Does.Contain("return new global::TestCase.Destination();"));
            Assert.That(
                valid.TypeMapperSource,
                Does.Not.Contain("MappingConfigurationException"));
            Assert.That(
                invalidAgain.TypeMapperSource,
                Is.EqualTo(invalid.TypeMapperSource));
            Assert.That(invalid.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(valid.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(invalidAgain.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Earlier_callback_failure_suppresses_construction_analysis()
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
    public sealed class Destination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(Build);

        private static global::Morphant.Generated.Types.N_TestCase.Plans.DestinationConstruction
            Build(Source source) => null!;
    }
}
""";

        var result = ConstructionDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0029" }));
            Assert.That(result.ConstructionDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Deduplicates_generic_origins_across_derived_consumers()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source<T> { }

    public interface IMissing<T> { }

    public sealed class Convention<T>
    {
        public Convention(bool missing) { }
    }

    public sealed class Previous<T> { }

    [MorphantMapper]
    public abstract partial class GenericMapper<T> : TypeMapper<GenericMapper<T>>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source<T>, IMissing<T>>();
            builder.Map<Source<T>, Convention<T>>();
            builder.Map<Source<T>, Previous<T>>()
                .Resolve((source, previous) => previous);
        }
    }

    [MorphantMapper]
    public partial class IntMapper : GenericMapper<int>
    {
        protected override void Configure(MapperBuilder builder) =>
            base.Configure(builder);
    }

    [MorphantMapper]
    public partial class StringMapper : GenericMapper<string>
    {
        protected override void Configure(MapperBuilder builder) =>
            base.Configure(builder);
    }
}
""";

        var result = ConstructionDiagnosticsGeneratorTest.Run(source);
        var diagnostics = result.ConstructionDiagnostics;

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0035",
                    "MORPH0036",
                    "MORPH0038"
                }));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Has.All.Contains("TestCase.Source<T>"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    private static IReadOnlyDictionary<string, ReportDiagnostic> Options(
        ReportDiagnostic value)
    {
        return Enumerable.Range(35, 5)
            .ToDictionary(
                static id => $"MORPH{id:0000}",
                _ => value,
                StringComparer.Ordinal);
    }

    private static (string HintName, string Source)[] Sources(
        ConstructionDiagnosticsGeneratorResult result)
    {
        return result.GeneratedSources
            .Select(static source => (
                source.HintName,
                source.SourceText.ToString()))
            .OrderBy(static source => source.HintName, StringComparer.Ordinal)
            .ToArray();
    }

    private static int Count(string value, string fragment)
    {
        return value.Split(
                [fragment],
                StringSplitOptions.None)
            .Length - 1;
    }
}
