using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ConstructionDiagnosticsTests;

internal static class ConstructionDiagnosticsGeneratorTest
{
    public static ConstructionDiagnosticsGeneratorResult Run(
        string source,
        LanguageVersion languageVersion = LanguageVersion.CSharp9,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions =
            null,
        GeneratorDriver? driver = null)
    {
        return new ConstructionDiagnosticsGeneratorResult(
            GeneratorTestDriver.Run(
                "ConstructionDiagnosticsConsumer",
                source,
                languageVersion,
                diagnosticOptions,
                driver: driver));
    }

    public static string SourceText(Location location)
    {
        return GeneratorTestDriver.GetSourceText(location);
    }
}

internal sealed record ConstructionDiagnosticsGeneratorResult :
    GeneratorTestDriverResult
{
    public ConstructionDiagnosticsGeneratorResult(
        GeneratorTestDriverResult result)
        : base(result)
    {
    }

    public ImmutableArray<Diagnostic> ConstructionDiagnostics =>
        EffectiveDiagnostics
            .Where(static diagnostic => diagnostic.Id is
                "MORPH0035" or
                "MORPH0036" or
                "MORPH0037" or
                "MORPH0038" or
                "MORPH0039")
            .ToImmutableArray();
}
