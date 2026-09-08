using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class DiagnosticIsolationTests
{
    // lang=c#
    private const string Source =
"""
#nullable enable
#pragma warning disable CS1591
using Morphant;

namespace TestCase
{
    public class Source { public int Value { get; set; } }
    public class First { }
    public class Second { }
    public class Third { }
    public class Valid { public int Value { get; set; } }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            var captured = new Third();
            builder.Map<Source, First>().ForDerived<Source, First>();
            builder.Map<Source, Second>()
                .Convert(_ => new Second())
                .Convert(_ => new Second());
            builder.Map<Source, Third>().Convert(_ => captured);
            builder.Map<Source, Valid>();
        }

        public static void VerifyContracts(TestMapper mapper)
        {
            ITypeMapper<Source, First> first = mapper;
            ITypeMapper<Source, Second> second = mapper;
            ITypeMapper<Source, Third> third = mapper;
            ITypeMapper<Source, Valid> valid = mapper;
        }
    }
}
""";

    [TestCase(ReportDiagnostic.Default, DiagnosticSeverity.Error)]
    [TestCase(ReportDiagnostic.Warn, DiagnosticSeverity.Warning)]
    [TestCase(ReportDiagnostic.Info, DiagnosticSeverity.Info)]
    [TestCase(ReportDiagnostic.Hidden, DiagnosticSeverity.Hidden)]
    [TestCase(ReportDiagnostic.Suppress, DiagnosticSeverity.Error)]
    public void Independent_failures_keep_complete_contracts_at_every_severity(
        ReportDiagnostic option,
        DiagnosticSeverity severity)
    {
        var baseline = Run(Source);
        var result = Run(Source, new Dictionary<string, ReportDiagnostic>
        {
            ["MORPH0019"] = option,
            ["MORPH0030"] = option,
            ["MORPH0052"] = option
        });
        var diagnostics = result.EffectiveDiagnostics
            .OrderBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(baseline.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(Sources(result), Is.EqualTo(Sources(baseline)));
            Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(option == ReportDiagnostic.Suppress
                    ? Array.Empty<string>()
                    : new[] { "MORPH0019", "MORPH0030", "MORPH0052" }));
            Assert.That(diagnostics.Select(static diagnostic => diagnostic.Severity),
                Is.All.EqualTo(severity));
        });

        if (option == ReportDiagnostic.Suppress)
        {
            return;
        }

        AssertDiagnostic(diagnostics[0],
            "'Convert' is configured more than once for mapping " +
            "'TestCase.Source -> TestCase.Second' in mapper 'TestCase.TestMapper'.",
            Source.LastIndexOf(".Convert(_ => new Second())", StringComparison.Ordinal) + 1,
            "Convert", Source.IndexOf(".Convert(_ => new Second())", StringComparison.Ordinal) + 1);
        AssertDiagnostic(diagnostics[1],
            "Convert for mapping 'TestCase.Source -> TestCase.Third' cannot be used " +
            "by mapper 'TestCase.TestMapper': value 'captured' is only available while Configure runs.",
            Source.LastIndexOf("captured", StringComparison.Ordinal), "captured",
            Source.IndexOf("captured", StringComparison.Ordinal));
        AssertDiagnostic(diagnostics[2],
            "ForDerived source type 'TestCase.Source' is the exact source type " +
            "of mapping 'TestCase.Source -> TestCase.First'.",
            Source.IndexOf("ForDerived<Source", StringComparison.Ordinal) + "ForDerived<".Length,
            "Source");
    }

    [Test]
    public void A_compiler_error_keeps_independent_diagnostics_and_contracts()
    {
        var source = Source.Replace("Convert(_ => captured)", "Convert(_ => MissingValue)",
                StringComparison.Ordinal)
            .Replace("            var captured = new Third();\n", "", StringComparison.Ordinal);
        var result = Run(source);
        var errors = result.CompilerWarningsAndErrors;

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics.Select(static diagnostic => diagnostic.Id)
                    .Order(StringComparer.Ordinal),
                Is.EqualTo(new[] { "MORPH0019", "MORPH0052" }));
            Assert.That(errors.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "CS0103" }));
        });
        var error = errors.Single();
        Assert.Multiple(() =>
        {
            Assert.That(error.Severity, Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(error.Location.SourceTree!.FilePath, Is.EqualTo("TestCase.cs"));
            Assert.That(error.Location.SourceSpan,
                Is.EqualTo(new TextSpan(source.IndexOf("MissingValue", StringComparison.Ordinal), 12)));
            Assert.That(error.GetMessage(),
                Is.EqualTo("The name 'MissingValue' does not exist in the current context"));
        });
    }

    private static void AssertDiagnostic(Diagnostic diagnostic, string message,
        int start, string text, int? additionalStart = null)
    {
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.GetMessage(), Is.EqualTo(message));
            Assert.That(diagnostic.Location.SourceTree!.FilePath, Is.EqualTo("TestCase.cs"));
            Assert.That(diagnostic.Location.SourceSpan, Is.EqualTo(new TextSpan(start, text.Length)));
            Assert.That(diagnostic.AdditionalLocations.Select(static location => location.SourceSpan),
                Is.EqualTo(additionalStart is { } additional
                    ? new[] { new TextSpan(additional, text.Length) }
                    : Array.Empty<TextSpan>()));
        });
    }

    private static GeneratorTestDriverResult Run(string source,
        IReadOnlyDictionary<string, ReportDiagnostic>? options = null) =>
        GeneratorTestDriver.Run("DiagnosticIsolation", source, LanguageVersion.CSharp9, options);

    private static GeneratedSourceSnapshot[] Sources(GeneratorTestDriverResult result) =>
        result.GeneratedSources.Select(static source => new GeneratedSourceSnapshot(
                source.HintName, source.SourceText.ToString()))
            .OrderBy(static source => source.HintName, StringComparer.Ordinal).ToArray();
}
