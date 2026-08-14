using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.MapperConfigurationTests;

internal static class MapperConfigurationGeneratorTest
{
    public static MapperConfigurationGeneratorResult Run(
        string source,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions =
            null,
        GeneratorDriver? driver = null,
        IEnumerable<MetadataReference>? additionalReferences = null)
    {
        return Run(
            [new ConfigurationSourceFile("TestCase.cs", source)],
            diagnosticOptions,
            driver,
            additionalReferences);
    }

    public static MapperConfigurationGeneratorResult Run(
        IReadOnlyCollection<ConfigurationSourceFile> sourceFiles,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions =
            null,
        GeneratorDriver? driver = null,
        IEnumerable<MetadataReference>? additionalReferences = null)
    {
        var result = GeneratorTestDriver.Run(
            "MapperConfigurationConsumer",
            sourceFiles
                .Select(static file =>
                    new GeneratorTestSourceFile(file.Name, file.Source))
                .ToArray(),
            LanguageVersion.Latest,
            diagnosticOptions,
            driver: driver,
            additionalReferences: additionalReferences);

        return new MapperConfigurationGeneratorResult(result);
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
}

internal sealed record MapperConfigurationGeneratorResult :
    GeneratorTestDriverResult
{
    public MapperConfigurationGeneratorResult(
        GeneratorTestDriverResult result)
        : base(result)
    {
    }
}

internal readonly record struct ConfigurationSourceFile(
    string Name,
    string Source);
