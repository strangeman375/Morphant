using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class ProductionGeneratorTest :
    CSharpSourceGeneratorTest<MorphantGenerator, DefaultVerifier>
{
    private readonly LanguageVersion _languageVersion;

    private ProductionGeneratorTest(LanguageVersion languageVersion)
    {
        _languageVersion = languageVersion;
        CompilerDiagnostics = CompilerDiagnostics.Warnings;
        TestState.AdditionalReferences.Add(typeof(TypeMapper<>).Assembly);
    }

    protected override ParseOptions CreateParseOptions()
    {
        return new CSharpParseOptions(
            _languageVersion,
            DocumentationMode.Diagnose);
    }

    public static async Task RunAndAssert(
        LanguageVersion languageVersion,
        string sourceFileContent,
        params (string FileName, string Content)[] expectedSources)
    {
        var test = new ProductionGeneratorTest(languageVersion)
        {
            TestCode = sourceFileContent
        };

        foreach (var expectedSource in expectedSources)
        {
            test.TestState.GeneratedSources.Add(
            (
                typeof(MorphantGenerator),
                expectedSource.FileName,
                GeneratedSourceText.Normalize(expectedSource.Content)
            ));
        }

        await test.RunAsync();
    }

}
