using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.MapperDeclarationTests;

internal static class MapperDeclarationGeneratorTest
{
    public static MapperDeclarationGeneratorResult Run(
        string source,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null,
        GeneratorDriver? driver = null)
    {
        return Run(
            [new MapperSourceFile("TestCase.cs", source)],
            diagnosticOptions,
            driver);
    }

    public static MapperDeclarationGeneratorResult Run(
        IReadOnlyCollection<MapperSourceFile> sourceFiles,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null,
        GeneratorDriver? driver = null)
    {
        var result = GeneratorTestDriver.Run(
            "MapperDeclarationConsumer",
            sourceFiles
                .Select(static file =>
                    new GeneratorTestSourceFile(file.Name, file.Source))
                .ToArray(),
            LanguageVersion.Latest,
            diagnosticOptions,
            driver: driver);

        return new MapperDeclarationGeneratorResult(result);
    }

    public static string SourceText(Location location)
    {
        return GeneratorTestDriver.GetSourceText(location);
    }
}

internal sealed record MapperDeclarationGeneratorResult :
    GeneratorTestDriverResult
{
    public MapperDeclarationGeneratorResult(
        GeneratorTestDriverResult result)
        : base(result)
    {
    }

    public ImmutableArray<Diagnostic> CompilerErrors => OutputCompilation
        .GetDiagnostics()
        .Where(static diagnostic =>
            !diagnostic.Id.StartsWith("MORPH", StringComparison.Ordinal) &&
            diagnostic.Severity == DiagnosticSeverity.Error)
        .ToImmutableArray();

    public bool HasGeneratedFile(string fileName)
    {
        return GeneratedSources.Any(source => source.HintName == fileName);
    }

    public string GeneratedFile(string fileName)
    {
        return GeneratedSources
            .Single(source => source.HintName == fileName)
            .SourceText
            .ToString();
    }
}

internal readonly record struct MapperSourceFile(
    string Name,
    string Source);
