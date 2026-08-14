using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.InheritanceDiagnosticsTests;

internal static class InheritanceDiagnosticsGeneratorTest
{
    public static InheritanceDiagnosticsGeneratorResult Run(
        string source,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions =
            null,
        GeneratorDriver? driver = null,
        IEnumerable<MetadataReference>? additionalReferences = null)
    {
        return new InheritanceDiagnosticsGeneratorResult(
            GeneratorTestDriver.Run(
                "InheritanceDiagnosticsConsumer",
                source,
                LanguageVersion.CSharp9,
                diagnosticOptions,
                driver: driver,
                additionalReferences: additionalReferences));
    }

    public static MetadataReference CompileReference(
        string assemblyName,
        string source)
    {
        return GeneratorTestDriver.CompileReference(
            assemblyName,
            source);
    }

    public static string SourceText(Location location)
    {
        return GeneratorTestDriver.GetSourceText(location);
    }
}

internal sealed record InheritanceDiagnosticsGeneratorResult :
    GeneratorTestDriverResult
{
    public InheritanceDiagnosticsGeneratorResult(
        GeneratorTestDriverResult result)
        : base(result)
    {
    }
}
