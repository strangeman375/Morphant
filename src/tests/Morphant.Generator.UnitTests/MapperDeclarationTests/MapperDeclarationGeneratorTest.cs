using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.UnitTests.MapperDeclarationTests;

internal static class MapperDeclarationGeneratorTest
{
    private static readonly ImmutableArray<MetadataReference>
        FrameworkReferences = BuildFrameworkReferences();

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
        var parseOptions = new CSharpParseOptions(
            LanguageVersion.Latest,
            DocumentationMode.Diagnose);
        var syntaxTrees = sourceFiles
            .Select(file => CSharpSyntaxTree.ParseText(
                Microsoft.CodeAnalysis.Text.SourceText.From(
                    file.Source,
                    Encoding.UTF8),
                parseOptions,
                file.Name))
            .ToImmutableArray();
        var options = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: NullableContextOptions.Enable,
            specificDiagnosticOptions: diagnosticOptions is null
                ? ImmutableDictionary<string, ReportDiagnostic>.Empty
                : diagnosticOptions.ToImmutableDictionary(
                    StringComparer.Ordinal));
        var compilation = CSharpCompilation.Create(
            "MapperDeclarationConsumer",
            syntaxTrees,
            FrameworkReferences.Add(
                MetadataReference.CreateFromFile(
                    typeof(TypeMapper).Assembly.Location)),
            options);

        driver ??= CSharpGeneratorDriver.Create(
            [new MorphantGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out _);

        var generatorResult = driver.GetRunResult().Results.Single();

        Assert.That(
            generatorResult.Exception,
            Is.Null,
            "The production generator must not throw.");

        return new MapperDeclarationGeneratorResult(
            driver,
            outputCompilation,
            generatorResult.Diagnostics,
            generatorResult.GeneratedSources);
    }

    public static string SourceText(Location location)
    {
        return location.SourceTree!
            .GetText()
            .ToString(location.SourceSpan);
    }

    private static ImmutableArray<MetadataReference>
        BuildFrameworkReferences()
    {
        var trustedPlatformAssemblies =
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ??
            throw new InvalidOperationException(
                "Trusted platform assemblies are unavailable.");

        return trustedPlatformAssemblies
            .Split(Path.PathSeparator)
            .Where(path => !Path.GetFileName(path).Equals(
                "Morphant.dll",
                StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path =>
                (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }
}

internal sealed record MapperDeclarationGeneratorResult(
    GeneratorDriver Driver,
    Compilation OutputCompilation,
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<GeneratedSourceResult> GeneratedSources)
{
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
