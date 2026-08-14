using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.MemberDiagnosticsTests;

internal static class MemberDiagnosticsGeneratorTest
{
    public static MemberDiagnosticsGeneratorResult Run(
        string source,
        LanguageVersion languageVersion = LanguageVersion.CSharp9,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions =
            null,
        GeneratorDriver? driver = null)
    {
        return new MemberDiagnosticsGeneratorResult(
            GeneratorTestDriver.Run(
                "MemberDiagnosticsConsumer",
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

internal sealed record MemberDiagnosticsGeneratorResult :
    GeneratorTestDriverResult
{
    public MemberDiagnosticsGeneratorResult(
        GeneratorTestDriverResult result)
        : base(result)
    {
    }

    public ImmutableArray<Diagnostic> MemberDiagnostics =>
        EffectiveDiagnostics
            .Where(static diagnostic => diagnostic.Id is
                "MORPH0040" or
                "MORPH0041" or
                "MORPH0042" or
                "MORPH0043")
            .ToImmutableArray();
}
