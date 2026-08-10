using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class ConventionTypeMapperGeneratorTest
    : CSharpSourceGeneratorTest<
        TestConventionTypeMapperGenerator,
        DefaultVerifier>
{
    private const string NewLine = "\r\n";

    private readonly LanguageVersion _languageVersion;

    private ConventionTypeMapperGeneratorTest(
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

    public static async Task RunAndAssert(
        LanguageVersion languageVersion,
        string source,
        string expected)
    {
        var test = new ConventionTypeMapperGeneratorTest(languageVersion)
        {
            TestCode = source
        };

        test.TestState.GeneratedSources.Add(
        (
            typeof(TestConventionTypeMapperGenerator),
            "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs",
            NormalizeGeneratedSource(expected)
        ));

        await test.RunAsync();
    }

    public static async Task RunAndAssertIgnoringCompilerDiagnostics(
        LanguageVersion languageVersion,
        string source,
        string expected,
        bool allowUnsafe = false)
    {
        var test = new ConventionTypeMapperGeneratorTest(languageVersion)
        {
            TestCode = source,
            CompilerDiagnostics = CompilerDiagnostics.None
        };

        if (allowUnsafe)
        {
            test.SolutionTransforms.Add((solution, projectId) =>
            {
                var project = solution.GetProject(projectId) ??
                    throw new InvalidOperationException(
                        "The test project is unavailable.");
                var options = project.CompilationOptions as
                    CSharpCompilationOptions ??
                    throw new InvalidOperationException(
                        "C# compilation options are unavailable.");

                return solution.WithProjectCompilationOptions(
                    projectId,
                    options.WithAllowUnsafe(true));
            });
        }

        test.TestState.GeneratedSources.Add(
        (
            typeof(TestConventionTypeMapperGenerator),
            "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs",
            NormalizeGeneratedSource(expected)
        ));

        await test.RunAsync();
    }

    public static async Task RunAndAssertWithAnalyzerConfig(
        LanguageVersion languageVersion,
        string source,
        string analyzerConfig,
        params (string FileName, string Content)[] expectedSources)
    {
        var test = new ConventionTypeMapperGeneratorTest(languageVersion)
        {
            TestCode = source
        };

        test.TestState.AnalyzerConfigFiles.Add(
        (
            "/.globalconfig",
            analyzerConfig
        ));
        AddExpectedSources(test, expectedSources);

        await test.RunAsync();
    }

    private static void AddExpectedSources(
        ConventionTypeMapperGeneratorTest test,
        IEnumerable<(string FileName, string Content)> expectedSources)
    {
        foreach (var expectedSource in expectedSources)
        {
            test.TestState.GeneratedSources.Add(
            (
                typeof(TestConventionTypeMapperGenerator),
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
