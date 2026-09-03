using Microsoft.CodeAnalysis;

namespace Morphant.Generator.UnitTests.CallbackDiagnosticsTests;

[TestFixture]
internal sealed class PrecedenceAndActualizationTests
{
    private const string InvalidSource =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public sealed class Destination
    {
        public Destination(int value) => Value = value;

        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            var offset = Environment.TickCount;

            builder.Map<Source, Destination>()
                .ConstructUsing(source => new(offset));
        }
    }
}
""";

    [Test]
    public void Earlier_composition_failure_owns_the_pair()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            var runtime = Environment.TickCount;

            builder.Map<Source, Destination>()
                .Members(source => new() { Value = runtime })
                .Members(source =>
                {
                    for (var index = 0; index < 1; index++) { }
                    return new();
                });
        }
    }
}
""";

        var result = CallbackDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0019" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Compiler_binding_error_is_not_duplicated_by_morphant()
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

    public sealed class Destination
    {
        public Destination(int value) { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(MissingValue));
    }
}
""";

        var result = CallbackDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics.Where(static diagnostic =>
                    diagnostic.Id is
                        "MORPH0029" or
                        "MORPH0030" or
                        "MORPH0031" or
                        "MORPH0032" or
                        "MORPH0033"),
                Is.Empty);
            Assert.That(
                result.CompilerWarningsAndErrors.Select(static diagnostic =>
                    diagnostic.Id),
                Does.Contain("CS0103"));
        });
    }

    [Test]
    public void Suppression_and_severity_do_not_change_recovery_artifacts()
    {
        var visible = CallbackDiagnosticsGeneratorTest.Run(InvalidSource);
        var suppressed = CallbackDiagnosticsGeneratorTest.Run(
            InvalidSource,
            diagnosticOptions: new Dictionary<string, ReportDiagnostic>
            {
                ["MORPH0030"] = ReportDiagnostic.Suppress
            });
        var warning = CallbackDiagnosticsGeneratorTest.Run(
            InvalidSource,
            diagnosticOptions: new Dictionary<string, ReportDiagnostic>
            {
                ["MORPH0030"] = ReportDiagnostic.Warn
            });
        var visibleSources = Sources(visible);
        var suppressedSources = Sources(suppressed);
        var warningSources = Sources(warning);

        Assert.Multiple(() =>
        {
            Assert.That(
                visible.EffectiveDiagnostics.Single().Id,
                Is.EqualTo("MORPH0030"));
            Assert.That(suppressed.EffectiveDiagnostics, Is.Empty);
            Assert.That(
                warning.EffectiveDiagnostics.Single().Severity,
                Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(suppressedSources, Is.EqualTo(visibleSources));
            Assert.That(warningSources, Is.EqualTo(visibleSources));
            Assert.That(
                visibleSources.Select(static source => source.HintName),
                Is.EqualTo(new[]
                {
                    "Morphant.Generated.Construction." +
                    "TestCase_Destination.g.cs",
                    "Morphant.Generated.MappingExtension." +
                    "TestCase_Source__TestCase_Destination.g.cs",
                    "Morphant.Generated.TypeMapper." +
                    "TestCase_TestMapper.g.cs"
                }));
            Assert.That(
                visibleSources.Single(static source =>
                        source.HintName.Contains(
                            ".TypeMapper.",
                            StringComparison.Ordinal))
                    .Source,
                Does.Contain(
                    "throw new global::Morphant.Exceptions." +
                    "MappingConfigurationException("));
            Assert.That(visible.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(suppressed.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(warning.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Actualizes_capture_and_recovery_on_one_driver()
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

    public sealed class Destination
    {
        public Destination(int value) => Value = value;

        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        private int Offset => 17;

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ConstructUsing(source => new(Offset));
    }
}
""";

        var invalid = CallbackDiagnosticsGeneratorTest.Run(InvalidSource);
        var valid = CallbackDiagnosticsGeneratorTest.Run(
            validSource,
            driver: invalid.Driver);
        var invalidAgain = CallbackDiagnosticsGeneratorTest.Run(
            InvalidSource,
            driver: valid.Driver);

        Assert.Multiple(() =>
        {
            Assert.That(
                invalid.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0030" }));
            Assert.That(valid.EffectiveDiagnostics, Is.Empty);
            Assert.That(
                invalidAgain.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0030" }));
            Assert.That(
                TypeMapperSource(valid),
                Does.Contain("__ConstructUsing(global::TestCase.Source source) " +
                    "=> new(this.Offset)"));
            Assert.That(
                TypeMapperSource(invalid),
                Does.Not.Contain("Environment.TickCount"));
            Assert.That(
                TypeMapperSource(invalidAgain),
                Is.EqualTo(TypeMapperSource(invalid)));
            Assert.That(valid.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(invalidAgain.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    private static (string HintName, string Source)[] Sources(
        CallbackDiagnosticsGeneratorResult result) =>
        result.GeneratedSources
            .Select(static source =>
                (source.HintName, source.SourceText.ToString()))
            .ToArray();

    private static string TypeMapperSource(
        CallbackDiagnosticsGeneratorResult result) =>
        result.GeneratedSources.Single(static source =>
                source.HintName.Contains(
                    ".TypeMapper.",
                    StringComparison.Ordinal))
            .SourceText.ToString();
}
