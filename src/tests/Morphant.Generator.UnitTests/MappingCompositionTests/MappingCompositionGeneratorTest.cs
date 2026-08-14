using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.MappingCompositionTests;

internal static class MappingCompositionGeneratorTest
{
    public static MappingCompositionGeneratorResult Run(
        string source,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions =
            null,
        GeneratorDriver? driver = null,
        IEnumerable<MetadataReference>? additionalReferences = null,
        LanguageVersion languageVersion = LanguageVersion.Latest)
    {
        var result = GeneratorTestDriver.Run(
            "MappingCompositionConsumer",
            source,
            languageVersion,
            diagnosticOptions,
            driver: driver,
            additionalReferences: additionalReferences);

        return new MappingCompositionGeneratorResult(result);
    }

    public static MetadataReference CompileReference(
        string assemblyName,
        string source)
    {
        return GeneratorTestDriver.CompileReference(assemblyName, source);
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

internal sealed record MappingCompositionGeneratorResult :
    GeneratorTestDriverResult
{
    public MappingCompositionGeneratorResult(
        GeneratorTestDriverResult result)
        : base(result)
    {
    }
}
