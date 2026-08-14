using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.NestedMappingDiagnosticsTests;

internal static class NestedMappingDiagnosticsGeneratorTest
{
    public static NestedMappingDiagnosticsGeneratorResult Run(
        string source,
        LanguageVersion languageVersion = LanguageVersion.CSharp9,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions =
            null,
        GeneratorDriver? driver = null)
    {
        return new NestedMappingDiagnosticsGeneratorResult(
            GeneratorTestDriver.Run(
                "NestedMappingDiagnosticsConsumer",
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

internal sealed record NestedMappingDiagnosticsGeneratorResult :
    GeneratorTestDriverResult
{
    public NestedMappingDiagnosticsGeneratorResult(
        GeneratorTestDriverResult result)
        : base(result)
    {
    }

    public ImmutableArray<Diagnostic> NestedMappingDiagnostics =>
        EffectiveDiagnostics
            .Where(static diagnostic => diagnostic.Id is
                "MORPH0044" or
                "MORPH0045" or
                "MORPH0046")
            .ToImmutableArray();
}
