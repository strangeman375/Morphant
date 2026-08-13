using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Morphant.Generator.UnitTests.CompatibilityDiagnosticsTests;

[TestFixture]
internal sealed class GateAndActualizationTests
{
    [Test]
    public void Suppression_does_not_resume_generation()
    {
        var result = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp8,
            sources: [CompatibilityGeneratorTest.MapperSource],
            references: [CompatibilityGeneratorTest.ActualRuntimeReference],
            diagnosticOptions: new Dictionary<string, ReportDiagnostic>
            {
                ["MORPH0001"] = ReportDiagnostic.Suppress
            });

        Assert.That(result.GeneratedSources, Is.Empty);
        Assert.That(
            result.OutputCompilation.GetDiagnostics()
                .Where(static diagnostic => diagnostic.Id == "MORPH0001"),
            Is.Empty);
    }

    [Test]
    public void Severity_override_changes_presentation_but_not_the_gate()
    {
        var result = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp8,
            sources: [CompatibilityGeneratorTest.MapperSource],
            references: [CompatibilityGeneratorTest.ActualRuntimeReference],
            diagnosticOptions: new Dictionary<string, ReportDiagnostic>
            {
                ["MORPH0001"] = ReportDiagnostic.Warn
            });
        var diagnostic = CompilationWithAnalyzers.GetEffectiveDiagnostics(
                result.Diagnostics,
                result.OutputCompilation)
            .Single(diagnostic => diagnostic.Id == "MORPH0001");

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostic.Severity,
                Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(result.GeneratedSources, Is.Empty);
        });
    }

    [Test]
    public void Actualizes_language_and_reference_gates_in_one_driver()
    {
        var compatible = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            references:
            [
                CompatibilityGeneratorTest.ActualRuntimeReference
            ]);
        CompatibilityGeneratorTest.AssertDiagnostics(compatible);

        var languageFailure = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp8,
            references:
            [
                CompatibilityGeneratorTest.ActualRuntimeReference
            ],
            driver: compatible.Driver);
        CompatibilityGeneratorTest.AssertDiagnostics(
            languageFailure,
            new ExpectedCompatibilityDiagnostic(
                "MORPH0001",
                "Morphant requires C# 9.0 or later, but this compilation uses C# 8.0."));

        var missing = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            driver: languageFailure.Driver);
        CompatibilityGeneratorTest.AssertDiagnostics(
            missing,
            new ExpectedCompatibilityDiagnostic(
                "MORPH0002",
                "Morphant requires a reference to a compatible runtime library."));

        var incompatibleReference =
            RuntimeContractFixture.Compatible()
                .WithRevision("2")
                .CreateReference();
        var incompatible = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            references: [incompatibleReference],
            driver: missing.Driver);
        CompatibilityGeneratorTest.AssertDiagnostics(
            incompatible,
            new ExpectedCompatibilityDiagnostic(
                "MORPH0004",
                "The Morphant runtime is incompatible with this generator: " +
                "the runtime and generator versions do not match."));

        var restored = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp9,
            references:
            [
                CompatibilityGeneratorTest.ActualRuntimeReference
            ],
            driver: incompatible.Driver);
        CompatibilityGeneratorTest.AssertDiagnostics(restored);

        Assert.Multiple(() =>
        {
            Assert.That(compatible.GeneratedSources, Is.Empty);
            Assert.That(languageFailure.GeneratedSources, Is.Empty);
            Assert.That(missing.GeneratedSources, Is.Empty);
            Assert.That(incompatible.GeneratedSources, Is.Empty);
            Assert.That(restored.GeneratedSources, Is.Empty);
        });
    }
}
