using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class TypeMapperGeneratorTest : CSharpSourceGeneratorTest<TestTypeMapperGenerator, DefaultVerifier>
{
    private const string NewLine = "\r\n";

    private readonly LanguageVersion _languageVersion;

    public TypeMapperGeneratorTest(LanguageVersion languageVersion)
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

    public static async Task RunAndAssert(
        LanguageVersion languageVersion,
        string sourceFileContent,
        params (string FileName, string Content)[] expectedSources)
    {
        await RunAndAssert(
            languageVersion,
            sourceFileContent,
            allowUnsafe: false,
            expectedSources);
    }

    public static async Task RunAndAssert(
        LanguageVersion languageVersion,
        string sourceFileContent,
        bool allowUnsafe,
        params (string FileName, string Content)[] expectedSources)
    {
        await RunAndAssert(
            languageVersion,
            sourceFileContent,
            allowUnsafe,
            Array.Empty<Assembly>(),
            expectedSources);
    }

    public static async Task RunAndAssert(
        LanguageVersion languageVersion,
        string sourceFileContent,
        IReadOnlyCollection<Assembly> additionalReferences,
        params (string FileName, string Content)[] expectedSources)
    {
        await RunAndAssert(
            languageVersion,
            sourceFileContent,
            allowUnsafe: false,
            additionalReferences,
            expectedSources);
    }

    public static async Task RunAndAssertWithAnalyzerConfig(
        LanguageVersion languageVersion,
        string sourceFileContent,
        string analyzerConfigContent,
        params (string FileName, string Content)[] expectedSources)
    {
        var test = new TypeMapperGeneratorTest(languageVersion)
        {
            TestCode = sourceFileContent
        };

        test.TestState.AnalyzerConfigFiles.Add(
        (
            "/.globalconfig",
            analyzerConfigContent
        ));

        AddExpectedSources(test, expectedSources);

        await test.RunAsync();
    }

    private static async Task RunAndAssert(
        LanguageVersion languageVersion,
        string sourceFileContent,
        bool allowUnsafe,
        IReadOnlyCollection<Assembly> additionalReferences,
        params (string FileName, string Content)[] expectedSources)
    {
        var test = new TypeMapperGeneratorTest(languageVersion)
        {
            TestCode = sourceFileContent
        };

        if (allowUnsafe)
        {
            test.SolutionTransforms.Add(
                static (solution, projectId) =>
                {
                    var project = solution.GetProject(projectId)!;
                    var options =
                        ((CSharpCompilationOptions)project.CompilationOptions!)
                        .WithAllowUnsafe(true);

                    return solution.WithProjectCompilationOptions(
                        project.Id,
                        options);
                });
        }

        foreach (var additionalReference in additionalReferences)
        {
            test.TestState.AdditionalReferences.Add(
                additionalReference);
        }

        AddExpectedSources(test, expectedSources);

        await test.RunAsync();
    }

    private static void AddExpectedSources(
        TypeMapperGeneratorTest test,
        IEnumerable<(string FileName, string Content)> expectedSources)
    {
        foreach (var expectedSource in expectedSources)
        {
            test.TestState.GeneratedSources.Add(
            (
                typeof(TestTypeMapperGenerator),
                expectedSource.FileName,
                NormalizeGeneratedSource(expectedSource.Content)
            ));
        }
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
