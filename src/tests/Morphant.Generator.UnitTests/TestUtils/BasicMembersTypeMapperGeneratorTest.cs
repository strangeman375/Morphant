using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Morphant.Generator.UnitTests.TestAssets;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class BasicMembersTypeMapperGeneratorTest
    : CSharpSourceGeneratorTest<
        TestBasicMembersTypeMapperGenerator,
        DefaultVerifier>
{
    private const string NewLine = "\r\n";

    private readonly LanguageVersion _languageVersion;

    private BasicMembersTypeMapperGeneratorTest(
        LanguageVersion languageVersion)
    {
        _languageVersion = languageVersion;
        CompilerDiagnostics = CompilerDiagnostics.Warnings;
        TestState.AdditionalReferences.Add(typeof(TypeMapper).Assembly);
        TestState.AdditionalReferences.Add(
            typeof(ReferencedNestedSource).Assembly);
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
        params (string FileName, string Content)[] expectedSources)
    {
        var test = new BasicMembersTypeMapperGeneratorTest(
            languageVersion)
        {
            TestCode = source
        };

        AddExpectedSources(test, expectedSources);
        await test.RunAsync();
    }

    public static async Task RunAndAssertWithAnalyzerConfig(
        LanguageVersion languageVersion,
        string source,
        string analyzerConfig,
        params (string FileName, string Content)[] expectedSources)
    {
        var test = new BasicMembersTypeMapperGeneratorTest(
            languageVersion)
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
        BasicMembersTypeMapperGeneratorTest test,
        IEnumerable<(string FileName, string Content)> expectedSources)
    {
        foreach (var expectedSource in expectedSources)
        {
            test.TestState.GeneratedSources.Add(
            (
                typeof(TestBasicMembersTypeMapperGenerator),
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

        return normalized.EndsWith(
            NewLine,
            StringComparison.Ordinal)
            ? normalized
            : normalized + NewLine;
    }
}
