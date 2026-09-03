using Microsoft.CodeAnalysis;

namespace Morphant.Generator.UnitTests.MappingCompletenessDiagnosticsTests;

[TestFixture]
internal sealed class SettingAndBoundaryTests
{
    [TestCase("None", 0, 0)]
    [TestCase("Source", 1, 0)]
    [TestCase("Destination", 0, 1)]
    [TestCase("Strict", 1, 1)]
    public void Applies_each_pair_level_validation_value(
        string value,
        int expectedSourceWarnings,
        int expectedDestinationWarnings)
    {
        var source = SourceForPairSetting(value);
        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.CompletenessDiagnostics.Count(static diagnostic =>
                    diagnostic.Id == "MORPH0047"),
                Is.EqualTo(expectedSourceWarnings));
            Assert.That(
                result.CompletenessDiagnostics.Count(static diagnostic =>
                    diagnostic.Id == "MORPH0048"),
                Is.EqualTo(expectedDestinationWarnings));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Uses_None_as_the_library_default()
    {
        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(
            SourceForPairSetting(value: null));

        Assert.Multiple(() =>
        {
            Assert.That(result.CompletenessDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reads_the_MSBuild_validation_value()
    {
        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(
            SourceForPairSetting(value: null),
            globalOptions: new Dictionary<string, string>
            {
                ["build_property.MorphantUnmappedMemberValidation"] =
                    "Strict"
            });

        Assert.That(
            result.CompletenessDiagnostics.Select(static diagnostic =>
                diagnostic.Id),
            Is.EqualTo(new[] { "MORPH0047", "MORPH0048" }));
    }

    [Test]
    public void Resolves_root_and_pair_override_precedence()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class SourceA
    {
        public int Unused { get; set; }
    }

    public sealed class DestinationA
    {
        public int Unmapped { get; set; }
    }

    public sealed class SourceB
    {
        public int Unused { get; set; }
    }

    public sealed class DestinationB
    {
        public int Unmapped { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.UnmappedMemberValidation(
                UnmappedMemberValidation.Strict);
            builder.Map<SourceA, DestinationA>();
            builder.Map<SourceB, DestinationB>()
                .UnmappedMemberValidation(UnmappedMemberValidation.None);
        }
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.CompletenessDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0047", "MORPH0048" }));
            Assert.That(
                result.CompletenessDiagnostics.All(diagnostic =>
                    diagnostic.GetMessage().Contains(
                        "SourceA",
                        StringComparison.Ordinal) ||
                    diagnostic.GetMessage().Contains(
                        "DestinationA",
                        StringComparison.Ordinal)),
                Is.True);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Resolves_inherited_root_validation_for_the_current_mapper()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public int Unused { get; set; }
    }

    public sealed class Destination
    {
        public int Unmapped { get; set; }
    }

    public abstract class BaseMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : BaseMapper<TMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.UnmappedMemberValidation(
                UnmappedMemberValidation.Strict);
    }

    [MorphantMapper]
    public partial class TestMapper : BaseMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, Destination>();
        }
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.CompletenessDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0047", "MORPH0048" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Last_pair_setting_call_wins()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public int Unused { get; set; }
    }

    public sealed class Destination
    {
        public int Unmapped { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .UnmappedMemberValidation(UnmappedMemberValidation.Strict)
                .UnmappedMemberValidation(UnmappedMemberValidation.Source);
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.CompletenessDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0047" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Does_not_analyze_a_manual_Convert_pair()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public int Unused { get; set; }
    }

    public sealed class Destination
    {
        public int Unmapped { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.UnmappedMemberValidation(
                UnmappedMemberValidation.Strict);
            builder.Map<Source, Destination>()
                .Convert(source => new Destination());
        }
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.CompletenessDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Explicit_manual_setting_remains_owned_by_settings_diagnostics()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public int Unused { get; set; }
    }

    public sealed class Destination
    {
        public int Unmapped { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .UnmappedMemberValidation(UnmappedMemberValidation.Strict)
                .Convert(source => new Destination());
    }
}
""";

        var result = MappingCompletenessDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.CompletenessDiagnostics, Is.Empty);
            Assert.That(
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0023" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Suppression_and_error_override_change_only_presentation()
    {
        var baseline = MappingCompletenessDiagnosticsGeneratorTest.Run(
            SourceForPairSetting("Strict"));
        var configured = MappingCompletenessDiagnosticsGeneratorTest.Run(
            SourceForPairSetting("Strict"),
            diagnosticOptions: new Dictionary<string, ReportDiagnostic>
            {
                ["MORPH0047"] = ReportDiagnostic.Suppress,
                ["MORPH0048"] = ReportDiagnostic.Error
            });

        Assert.Multiple(() =>
        {
            Assert.That(
                configured.CompletenessDiagnostics.Select(static diagnostic =>
                    (diagnostic.Id, diagnostic.Severity)),
                Is.EqualTo(new[]
                {
                    ("MORPH0048", DiagnosticSeverity.Error)
                }));
            Assert.That(
                configured.TypeMapperSource,
                Is.EqualTo(baseline.TypeMapperSource));
            Assert.That(
                configured.TypeMapperSource,
                Does.Not.Contain("MappingConfigurationException"));
        });
    }

    private static string SourceForPairSetting(string? value)
    {
        var setting = value is null
            ? string.Empty
            : Environment.NewLine +
              "                .UnmappedMemberValidation(" +
              "UnmappedMemberValidation." + value + ")";

        // lang=c#
        return
$$"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public int Unused { get; set; }
    }

    public sealed class Destination
    {
        public int Unmapped { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>(){{setting}};
    }
}
""";
    }
}
