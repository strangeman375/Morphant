using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.CallbackDiagnosticsTests;

internal static class CallbackDiagnosticsGeneratorTest
{
    public static CallbackDiagnosticsGeneratorResult Run(
        string source,
        LanguageVersion languageVersion = LanguageVersion.CSharp9,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions =
            null,
        GeneratorDriver? driver = null)
    {
        return new CallbackDiagnosticsGeneratorResult(
            GeneratorTestDriver.Run(
                "CallbackDiagnosticsConsumer",
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

internal sealed record CallbackDiagnosticsGeneratorResult :
    GeneratorTestDriverResult
{
    public CallbackDiagnosticsGeneratorResult(
        GeneratorTestDriverResult result)
        : base(result)
    {
    }
}
