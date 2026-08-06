using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class PairConfigurationGeneratorTest :
    CSharpSourceGeneratorTest<
        TestPairConfigurationGenerator,
        DefaultVerifier>
{
    private const string NewLine = "\r\n";

    // The production pipeline binds against its in-memory generated surface.
    // This test-only fallback keeps the final verification compilation valid
    // without adding unrelated surface snapshots to this model category.
    // The exact generated overloads are non-generic and win overload
    // resolution while the semantic model is built.
    private const string CompilerFallbackSource =
"""
#nullable enable

using Morphant.Context;
using Morphant.Delegates;

namespace Morphant
{
    internal static class PairConfigurationCompilerFallback
    {
        public static MapperBuilder<TSource, TDestination> Construct<TSource, TDestination>(
            this MapperBuilder<TSource, TDestination> builder,
            Construct<TSource, object> construct) => builder;

        public static MapperBuilder<TSource, TDestination> Construct<TSource, TDestination>(
            this MapperBuilder<TSource, TDestination> builder,
            Construct<TSource, TDestination, object> construct) => builder;

        public static MapperBuilder<TSource, TDestination> Members<TSource, TDestination>(
            this MapperBuilder<TSource, TDestination> builder,
            Members<TSource, TDestination, object> members) => builder;

        public static MapperBuilder<TSource, TDestination> Members<TSource, TDestination>(
            this MapperBuilder<TSource, TDestination> builder,
            Members<TSource, TDestination, TDestination, object> members) => builder;

        public static MapperBuilder<TSource, TDestination> Convert<TSource, TDestination>(
            this MapperBuilder<TSource, TDestination> builder,
            Convert<TSource, TDestination, TDestination> convert) => builder;
    }
}
""";

    private readonly LanguageVersion _languageVersion;

    private PairConfigurationGeneratorTest(LanguageVersion languageVersion)
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
        string expectedSource)
    {
        var test = new PairConfigurationGeneratorTest(languageVersion)
        {
            TestCode = sourceFileContent
        };

        test.TestState.Sources.Add(CompilerFallbackSource);

        test.TestState.GeneratedSources.Add(
        (
            typeof(TestPairConfigurationGenerator),
            "PairConfigurationModel.g.cs",
            NormalizeGeneratedSource(expectedSource)
        ));

        await test.RunAsync();
    }

    public static async Task RunAndAssertWithAnalyzerConfig(
        LanguageVersion languageVersion,
        string sourceFileContent,
        string analyzerConfig,
        string expectedSource)
    {
        var test = new PairConfigurationGeneratorTest(languageVersion)
        {
            TestCode = sourceFileContent
        };

        test.TestState.Sources.Add(CompilerFallbackSource);
        test.TestState.AnalyzerConfigFiles.Add(
        (
            "/.globalconfig",
            analyzerConfig
        ));
        test.TestState.GeneratedSources.Add(
        (
            typeof(TestPairConfigurationGenerator),
            "PairConfigurationModel.g.cs",
            NormalizeGeneratedSource(expectedSource)
        ));

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
