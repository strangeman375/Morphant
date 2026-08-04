using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class MemberSurfaceGeneratorTest :
    CSharpSourceGeneratorTest<
        TestMemberSurfaceGenerator,
        DefaultVerifier>
{
    private const string NewLine = "\r\n";

    private readonly LanguageVersion _languageVersion;

    private MemberSurfaceGeneratorTest(
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
        var test = new MemberSurfaceGeneratorTest(languageVersion)
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
                typeof(TestMemberSurfaceGenerator),
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
