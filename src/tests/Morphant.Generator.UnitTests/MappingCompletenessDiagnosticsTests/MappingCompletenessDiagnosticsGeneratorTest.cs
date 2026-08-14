using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.MappingCompletenessDiagnosticsTests;

internal static class MappingCompletenessDiagnosticsGeneratorTest
{
    public static MappingCompletenessDiagnosticsGeneratorResult Run(
        string source,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions =
            null,
        IReadOnlyDictionary<string, string>? globalOptions = null,
        GeneratorDriver? driver = null,
        LanguageVersion languageVersion = LanguageVersion.CSharp9)
    {
        return new MappingCompletenessDiagnosticsGeneratorResult(
            GeneratorTestDriver.Run(
                "MappingCompletenessDiagnosticsConsumer",
                source,
                languageVersion,
                diagnosticOptions,
                globalOptions,
                driver));
    }

    public static string SourceText(Location location)
    {
        return GeneratorTestDriver.GetSourceText(location);
    }
}

internal sealed record MappingCompletenessDiagnosticsGeneratorResult :
    GeneratorTestDriverResult
{
    public MappingCompletenessDiagnosticsGeneratorResult(
        GeneratorTestDriverResult result)
        : base(result)
    {
    }

    public ImmutableArray<Diagnostic> CompletenessDiagnostics =>
        EffectiveDiagnostics
            .Where(static diagnostic => diagnostic.Id is
                "MORPH0047" or
                "MORPH0048")
            .ToImmutableArray();
}
