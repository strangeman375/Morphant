using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class TemplateExtensionGeneratorTest :
    CSharpSourceGeneratorTest<
        TestTemplateExtensionGenerator,
        DefaultVerifier>
{
    private const string NewLine = "\r\n";

    private readonly LanguageVersion _languageVersion;

    private TemplateExtensionGeneratorTest(
        LanguageVersion languageVersion)
    {
        _languageVersion = languageVersion;
        CompilerDiagnostics = CompilerDiagnostics.Warnings;
        ReferenceAssemblies = ReferenceAssemblies.Net.Net80;
        TestState.AdditionalReferences.Add(typeof(TypeMapper).Assembly);
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
            Array.Empty<Assembly>(),
            expectedSources);
    }

    public static async Task RunAndAssert(
        LanguageVersion languageVersion,
        string sourceFileContent,
        IReadOnlyCollection<Assembly> additionalReferences,
        params (string FileName, string Content)[] expectedSources)
    {
        var test = new TemplateExtensionGeneratorTest(languageVersion)
        {
            TestCode = sourceFileContent
        };

        foreach (var additionalReference in additionalReferences)
        {
            test.TestState.AdditionalReferences.Add(
                additionalReference);
        }

        foreach (var expectedSource in expectedSources)
        {
            test.TestState.GeneratedSources.Add(
            (
                typeof(TestTemplateExtensionGenerator),
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
