using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class ConstructionSurfaceGeneratorTest :
    CSharpSourceGeneratorTest<
        TestConstructionSurfaceGenerator,
        DefaultVerifier>
{
    private readonly LanguageVersion _languageVersion;

    private ConstructionSurfaceGeneratorTest(
        LanguageVersion languageVersion)
    {
        _languageVersion = languageVersion;
        CompilerDiagnostics = CompilerDiagnostics.Warnings;
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
        return RunAndAssertCore(
            languageVersion,
            sourceFileContent,
            Array.Empty<Assembly>(),
            expectedSources);
    }

    public static Task RunAndAssert(
        LanguageVersion languageVersion,
        string sourceFileContent,
        IReadOnlyCollection<Assembly> additionalReferences,
        params (string FileName, string Content)[] expectedSources)
    {
        return RunAndAssertCore(
            languageVersion,
            sourceFileContent,
            additionalReferences,
            expectedSources);
    }

    private static async Task RunAndAssertCore(
        LanguageVersion languageVersion,
        string sourceFileContent,
        IReadOnlyCollection<Assembly> additionalReferences,
        params (string FileName, string Content)[] expectedSources)
    {
        var test = new ConstructionSurfaceGeneratorTest(languageVersion)
        {
            TestCode = sourceFileContent
        };

        foreach (var additionalReference in additionalReferences)
        {
            test.TestState.AdditionalReferences.Add(additionalReference);
        }

        foreach (var expectedSource in expectedSources)
        {
            test.TestState.GeneratedSources.Add(
            (
                typeof(TestConstructionSurfaceGenerator),
                expectedSource.FileName,
                GeneratedSourceText.Normalize(expectedSource.Content)
            ));
        }

        await test.RunAsync();
    }

}
