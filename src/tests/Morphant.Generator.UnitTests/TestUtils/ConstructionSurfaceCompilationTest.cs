using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.UnitTests.TestUtils;

internal static class ConstructionSurfaceCompilationTest
{
    private static readonly ImmutableArray<MetadataReference>
        DefaultReferences = BuildDefaultReferences();

    public static void RunAndAssert(
        LanguageVersion languageVersion,
        string source,
        params MetadataReference[] additionalReferences)
    {
        RunAndGetGeneratedSources(
            languageVersion,
            source,
            additionalReferences);
    }

    public static IReadOnlyDictionary<string, string>
        RunAndGetGeneratedSources(
            LanguageVersion languageVersion,
            string source,
            params MetadataReference[] additionalReferences)
    {
        return RunAndGetGeneratedSources(
            languageVersion,
            source,
            new TestConstructionSurfaceGenerator().AsSourceGenerator(),
            additionalReferences);
    }

    public static IReadOnlyDictionary<string, string>
        RunProductionAndGetGeneratedSources(
            LanguageVersion languageVersion,
            string source,
            params MetadataReference[] additionalReferences)
    {
        return RunAndGetGeneratedSources(
            languageVersion,
            source,
            new MorphantGenerator().AsSourceGenerator(),
            additionalReferences);
    }

    private static IReadOnlyDictionary<string, string>
        RunAndGetGeneratedSources(
            LanguageVersion languageVersion,
            string source,
            ISourceGenerator generator,
            params MetadataReference[] additionalReferences)
    {
        var parseOptions = new CSharpParseOptions(
            languageVersion,
            DocumentationMode.Diagnose);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            SourceText.From(source, Encoding.UTF8),
            parseOptions,
            "TestCase.cs");
        var compilation = CSharpCompilation.Create(
            "ConstructionSurfaceCompilation",
            new[] { syntaxTree },
            DefaultReferences.AddRange(additionalReferences),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { generator },
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);

        var errors = generatorDiagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .Where(static diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.That(errors, Is.Empty);

        var generatorResult = driver.GetRunResult().Results.Single();

        Assert.That(generatorResult.Exception, Is.Null);

        return generatorResult.GeneratedSources.ToDictionary(
            static generatedSource => generatedSource.HintName,
            static generatedSource => generatedSource.SourceText.ToString(),
            StringComparer.Ordinal);
    }

    private static ImmutableArray<MetadataReference>
        BuildDefaultReferences()
    {
        var trustedPlatformAssemblies =
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ??
            throw new InvalidOperationException(
                "TRUSTED_PLATFORM_ASSEMBLIES is unavailable.");

        return trustedPlatformAssemblies
            .Split(Path.PathSeparator)
            .Append(typeof(TypeMapper).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToImmutableArray<MetadataReference>();
    }
}
