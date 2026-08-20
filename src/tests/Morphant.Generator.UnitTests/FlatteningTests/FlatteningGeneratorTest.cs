using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.FlatteningTests;

internal static class FlatteningGeneratorTest
{
    public static FlatteningGeneratorResult Run(
        string source,
        IReadOnlyDictionary<string, string>? globalOptions = null,
        GeneratorDriver? driver = null,
        LanguageVersion languageVersion = LanguageVersion.CSharp9) =>
        new(GeneratorTestDriver.Run(
            "FlatteningConsumer",
            source,
            languageVersion,
            globalOptions: globalOptions,
            driver: driver));

    public static string SourceText(Location location) =>
        GeneratorTestDriver.GetSourceText(location);
}

internal sealed record FlatteningGeneratorResult :
    GeneratorTestDriverResult
{
    public FlatteningGeneratorResult(GeneratorTestDriverResult result)
        : base(result)
    {
    }

    public ImmutableArray<Diagnostic> FlatteningDiagnostics =>
        EffectiveDiagnostics
            .Where(static diagnostic => diagnostic.Id == "MORPH0051")
            .ToImmutableArray();
}
