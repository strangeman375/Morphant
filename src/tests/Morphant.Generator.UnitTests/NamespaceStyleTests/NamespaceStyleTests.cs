using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.NamespaceStyleTests;

[TestFixture]
internal sealed partial class NamespaceStyleTests
{
    private static GeneratorTestDriverResult Verify(
        string source,
        LanguageVersion languageVersion,
        (string FileName, string Content)[] expected,
        GeneratorDriver? driver = null)
    {
        var result = GeneratorTestDriver.Run(
            "NamespaceStyles", source, languageVersion, driver: driver);
        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(result.GeneratedSources.Select(item =>
                    (item.HintName, item.SourceText.ToString())),
                Is.EquivalentTo(expected.Select(item =>
                    (item.FileName, GeneratedSourceText.Normalize(item.Content)))));
        });
        return result;
    }

    [Test]
    public void Updates_every_artifact_when_the_language_version_changes()
    {
        GeneratorDriver? driver = null;
        foreach (var version in new[]
        {
            LanguageVersion.CSharp9,
            LanguageVersion.CSharp10,
            LanguageVersion.CSharp11,
            LanguageVersion.CSharp9,
            LanguageVersion.Latest,
            LanguageVersion.CSharp9
        })
        {
            var result = Verify(NamedSource, version,
                version == LanguageVersion.CSharp9 ? NamedBlockSources : NamedFileSources,
                driver);
            driver = result.Driver;
        }
    }

    [Test]
    public void Keeps_file_scoped_output_cached_when_only_newer_language_options_change()
    {
        var options = new CSharpParseOptions(LanguageVersion.CSharp10);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new MorphantGenerator().AsSourceGenerator()],
            parseOptions: options,
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
        driver = Verify(NamedSource, LanguageVersion.CSharp10, NamedFileSources, driver).Driver;
        driver = Verify(NamedSource, LanguageVersion.CSharp11, NamedFileSources, driver).Driver;
        var tracked = driver.GetRunResult().Results.Single().TrackedSteps;
        foreach (var stage in new[]
        {
            "BuildConstructionPlanRequests", "BuildMemberPlanRequests",
            "BuildMappingExtensionRequests", "BuildMemberExtensionRequests",
            "BuildTypeMapperRequests"
        })
        {
            Assert.That(tracked[stage].SelectMany(step => step.Outputs)
                    .Select(output => output.Reason),
                Has.All.Matches<IncrementalStepRunReason>(reason =>
                    reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged),
                stage);
        }
    }
}
