using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.UnitTests.CompatibilityDiagnosticsTests;

[TestFixture]
internal sealed class DescriptorAndLanguageTests
{
    [Test]
    public void Publishes_the_exact_four_descriptor_contracts()
    {
        var runtime =
            CompatibilityGeneratorTest.ActualRuntimeReference;
        var duplicate =
            RuntimeContractFixture.Compatible().CreateReference();
        var metadataOnly = CompatibilityGeneratorTest.CreateReference(
            "MetadataOnlyRuntime",
"""
using System.Reflection;
[assembly: AssemblyMetadata("Morphant.GeneratorContractVersion", "1")]
""");
        var diagnostics = new[]
        {
            CompatibilityGeneratorTest.Run(
                LanguageVersion.CSharp8,
                references: [runtime]).Diagnostics.Single(),
            CompatibilityGeneratorTest.Run(
                LanguageVersion.CSharp9).Diagnostics.Single(),
            CompatibilityGeneratorTest.Run(
                LanguageVersion.CSharp9,
                references: [runtime, duplicate]).Diagnostics.Single(),
            CompatibilityGeneratorTest.Run(
                LanguageVersion.CSharp9,
                references: [metadataOnly]).Diagnostics.Single()
        };
        var expected = new[]
        {
            (
                Id: "MORPH0001",
                Title: "Unsupported C# language version",
                Message: "Morphant requires C# 9.0 or later, but this compilation uses C# {0}."),
            (
                Id: "MORPH0002",
                Title: "Morphant runtime not found",
                Message: "Morphant requires a reference to a compatible runtime library."),
            (
                Id: "MORPH0003",
                Title: "Multiple Morphant runtimes found",
                Message: "Multiple Morphant runtime libraries were found. Reference exactly one."),
            (
                Id: "MORPH0004",
                Title: "Incompatible Morphant runtime",
                Message: "The Morphant runtime is incompatible with this generator: {0}.")
        };

        for (var index = 0; index < expected.Length; index++)
        {
            var descriptor = diagnostics[index].Descriptor;
            var contract = expected[index];

            Assert.Multiple(() =>
            {
                Assert.That(descriptor.Id, Is.EqualTo(contract.Id));
                Assert.That(
                    descriptor.Title.ToString(),
                    Is.EqualTo(contract.Title));
                Assert.That(
                    descriptor.MessageFormat.ToString(),
                    Is.EqualTo(contract.Message));
                Assert.That(
                    descriptor.Category,
                    Is.EqualTo("Morphant.Compatibility"));
                Assert.That(
                    descriptor.DefaultSeverity,
                    Is.EqualTo(DiagnosticSeverity.Error));
                Assert.That(descriptor.IsEnabledByDefault, Is.True);
                Assert.That(descriptor.Description.ToString(), Is.Empty);
                Assert.That(descriptor.HelpLinkUri, Is.Empty);
                Assert.That(descriptor.CustomTags, Is.Empty);
                Assert.That(
                    descriptor.CustomTags,
                    Does.Not.Contain(WellKnownDiagnosticTags.NotConfigurable));
                Assert.That(diagnostics[index].Location, Is.EqualTo(Location.None));
                Assert.That(diagnostics[index].AdditionalLocations, Is.Empty);
            });
        }
    }

    [Test]
    public void Reports_the_effective_CSharp8_display_name_and_gates_generation()
    {
        var result = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp8,
            sources: [CompatibilityGeneratorTest.MapperSource],
            references: [CompatibilityGeneratorTest.ActualRuntimeReference]);

        CompatibilityGeneratorTest.AssertDiagnostics(
            result,
            new ExpectedCompatibilityDiagnostic(
                "MORPH0001",
                "Morphant requires C# 9.0 or later, but this compilation uses C# 8.0."));
        Assert.That(result.GeneratedSources, Is.Empty);
    }

    [TestCase(LanguageVersion.CSharp9)]
    [TestCase(LanguageVersion.Default)]
    [TestCase(LanguageVersion.Latest)]
    [TestCase(LanguageVersion.LatestMajor)]
    [TestCase(LanguageVersion.Preview)]
    public void Accepts_CSharp9_and_newer_effective_aliases(
        LanguageVersion languageVersion)
    {
        var result = CompatibilityGeneratorTest.Run(
            languageVersion,
            references:
            [
                CompatibilityGeneratorTest.ActualRuntimeReference
            ]);

        CompatibilityGeneratorTest.AssertDiagnostics(result);
        Assert.That(result.GeneratedSources, Is.Empty);
    }

    [Test]
    public void Publishes_language_then_runtime_diagnostics_in_fixed_order()
    {
        var result = CompatibilityGeneratorTest.Run(
            LanguageVersion.CSharp8);

        CompatibilityGeneratorTest.AssertDiagnostics(
            result,
            new ExpectedCompatibilityDiagnostic(
                "MORPH0001",
                "Morphant requires C# 9.0 or later, but this compilation uses C# 8.0."),
            new ExpectedCompatibilityDiagnostic(
                "MORPH0002",
                "Morphant requires a reference to a compatible runtime library."));
        Assert.That(result.GeneratedSources, Is.Empty);
    }
}
