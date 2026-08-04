using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class TemplateSurfaceGeneratorTest :
    CSharpSourceGeneratorTest<
        TestTemplateSurfaceGenerator,
        DefaultVerifier>
{
    private const string NewLine = "\r\n";

    private readonly LanguageVersion _languageVersion;

    private TemplateSurfaceGeneratorTest(
        LanguageVersion languageVersion)
    {
        _languageVersion = languageVersion;
        CompilerDiagnostics = CompilerDiagnostics.Warnings;
        TestState.AdditionalReferences.Add(
            typeof(TypeMapper).Assembly);
    }

    protected override ParseOptions CreateParseOptions()
    {
        return new CSharpParseOptions(
            _languageVersion,
            DocumentationMode.Diagnose);
    }

    public static Task RunAndAssert(
        LanguageVersion languageVersion,
        string sourceFileContent,
        params (string FileName, string Content)[] expectedSources)
    {
        return RunAndAssert(
            languageVersion,
            sourceFileContent,
            analyzerConfigContent: null,
            expectedSources);
    }

    public static Task RunAndAssertWithAnalyzerConfig(
        LanguageVersion languageVersion,
        string sourceFileContent,
        string analyzerConfigContent,
        params (string FileName, string Content)[] expectedSources)
    {
        return RunAndAssert(
            languageVersion,
            sourceFileContent,
            analyzerConfigContent,
            expectedSources);
    }

    private static async Task RunAndAssert(
        LanguageVersion languageVersion,
        string sourceFileContent,
        string? analyzerConfigContent,
        params (string FileName, string Content)[] expectedSources)
    {
        var test = new TemplateSurfaceGeneratorTest(languageVersion)
        {
            TestCode = sourceFileContent
        };

        if (analyzerConfigContent is not null)
        {
            test.TestState.AnalyzerConfigFiles.Add(
            (
                "/.globalconfig",
                analyzerConfigContent
            ));
        }

        foreach (var expectedSource in expectedSources)
        {
            test.TestState.GeneratedSources.Add(
            (
                typeof(TestTemplateSurfaceGenerator),
                expectedSource.FileName,
                NormalizeGeneratedSource(expectedSource.Content)
            ));
        }

        await test.RunAsync();
    }

    private static string NormalizeGeneratedSource(string source)
    {
        var normalized = source
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Replace("\n", NewLine);

        return normalized.EndsWith(NewLine, StringComparison.Ordinal)
            ? normalized
            : normalized + NewLine;
    }
}
