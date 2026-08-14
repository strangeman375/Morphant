using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.MappingSettingsDiagnosticsTests;

internal static class MappingSettingsDiagnosticsGeneratorTest
{
    public static MappingSettingsDiagnosticsGeneratorResult Run(
        string source,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions =
            null,
        IReadOnlyDictionary<string, string>? globalOptions = null,
        GeneratorDriver? driver = null,
        LanguageVersion languageVersion = LanguageVersion.CSharp9)
    {
        return new MappingSettingsDiagnosticsGeneratorResult(
            GeneratorTestDriver.Run(
                "MappingSettingsDiagnosticsConsumer",
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

    public static int Line(Location location)
    {
        return GeneratorTestDriver.GetLine(location);
    }
}

internal sealed record MappingSettingsDiagnosticsGeneratorResult :
    GeneratorTestDriverResult
{
    public MappingSettingsDiagnosticsGeneratorResult(
        GeneratorTestDriverResult result)
        : base(result)
    {
    }
}
