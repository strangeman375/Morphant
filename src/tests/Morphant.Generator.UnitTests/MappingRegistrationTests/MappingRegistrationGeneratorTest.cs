using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.MappingRegistrationTests;

internal static class MappingRegistrationGeneratorTest
{
    public static MappingRegistrationGeneratorResult Run(
        string source,
        LanguageVersion languageVersion = LanguageVersion.Latest,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions =
            null,
        GeneratorDriver? driver = null)
    {
        return Run(
            [new RegistrationSourceFile("TestCase.cs", source)],
            languageVersion,
            diagnosticOptions,
            driver);
    }

    public static MappingRegistrationGeneratorResult Run(
        IReadOnlyCollection<RegistrationSourceFile> sourceFiles,
        LanguageVersion languageVersion = LanguageVersion.Latest,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions =
            null,
        GeneratorDriver? driver = null)
    {
        var result = GeneratorTestDriver.Run(
            "MappingRegistrationConsumer",
            sourceFiles
                .Select(static file =>
                    new GeneratorTestSourceFile(file.Name, file.Source))
                .ToArray(),
            languageVersion,
            diagnosticOptions,
            driver: driver);

        return new MappingRegistrationGeneratorResult(result);
    }

    public static string SourceText(Location location)
    {
        return GeneratorTestDriver.GetSourceText(location);
    }
}

internal sealed record MappingRegistrationGeneratorResult :
    GeneratorTestDriverResult
{
    public MappingRegistrationGeneratorResult(
        GeneratorTestDriverResult result)
        : base(result)
    {
    }
}

internal readonly record struct RegistrationSourceFile(
    string Name,
    string Source);
