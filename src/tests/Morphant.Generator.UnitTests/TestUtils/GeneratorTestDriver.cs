using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.UnitTests.TestUtils;

internal static class GeneratorTestDriver
{
    private static readonly ImmutableArray<MetadataReference>
        FrameworkReferences = BuildFrameworkReferences();

    public static GeneratorTestDriverResult Run(
        string assemblyName,
        string source,
        LanguageVersion languageVersion,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions =
            null,
        IReadOnlyDictionary<string, string>? globalOptions = null,
        GeneratorDriver? driver = null,
        IEnumerable<MetadataReference>? additionalReferences = null)
    {
        return Run(
            assemblyName,
            [new GeneratorTestSourceFile("TestCase.cs", source)],
            languageVersion,
            diagnosticOptions,
            globalOptions,
            driver,
            additionalReferences);
    }

    public static GeneratorTestDriverResult Run(
        string assemblyName,
        IReadOnlyCollection<GeneratorTestSourceFile> sourceFiles,
        LanguageVersion languageVersion,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions =
            null,
        IReadOnlyDictionary<string, string>? globalOptions = null,
        GeneratorDriver? driver = null,
        IEnumerable<MetadataReference>? additionalReferences = null)
    {
        var parseOptions = new CSharpParseOptions(
            languageVersion,
            DocumentationMode.Diagnose);
        var syntaxTrees = sourceFiles
            .Select(file => CSharpSyntaxTree.ParseText(
                SourceText.From(file.Source, Encoding.UTF8),
                parseOptions,
                file.Name))
            .ToImmutableArray();
        var references = FrameworkReferences.Add(
            MetadataReference.CreateFromFile(
                typeof(TypeMapper).Assembly.Location));

        if (additionalReferences is not null)
        {
            references = references.AddRange(additionalReferences);
        }

        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                specificDiagnosticOptions: diagnosticOptions is null
                    ? ImmutableDictionary<string, ReportDiagnostic>.Empty
                    : diagnosticOptions.ToImmutableDictionary(
                        StringComparer.Ordinal)));
        var analyzerOptions = new TestAnalyzerConfigOptionsProvider(
            globalOptions is null
                ? ImmutableDictionary<string, string>.Empty
                : globalOptions.ToImmutableDictionary(StringComparer.Ordinal));

        driver = driver is null
            ? CSharpGeneratorDriver.Create(
                [new MorphantGenerator().AsSourceGenerator()],
                optionsProvider: analyzerOptions,
                parseOptions: parseOptions)
            : driver.WithUpdatedAnalyzerConfigOptions(analyzerOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out _);
        var generatorResult = driver.GetRunResult().Results.Single();

        Assert.That(
            generatorResult.Exception,
            Is.Null,
            "The production generator must not throw.");

        return new GeneratorTestDriverResult(
            driver,
            outputCompilation,
            generatorResult.Diagnostics,
            generatorResult.GeneratedSources);
    }

    public static MetadataReference CompileReference(
        string assemblyName,
        string source)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            FrameworkReferences.Add(
                MetadataReference.CreateFromFile(
                    typeof(TypeMapper).Assembly.Location)),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);

        Assert.That(
            emit.Success,
            Is.True,
            string.Join(Environment.NewLine, emit.Diagnostics));

        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    public static string GetSourceText(Location location)
    {
        return location.SourceTree!
            .GetText()
            .ToString(location.SourceSpan);
    }

    public static int GetLine(Location location)
    {
        return location.GetLineSpan().StartLinePosition.Line + 1;
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

internal record GeneratorTestDriverResult(
    GeneratorDriver Driver,
    Compilation OutputCompilation,
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<GeneratedSourceResult> GeneratedSources)
{
    public ImmutableArray<Diagnostic> EffectiveDiagnostics =>
        CompilationWithAnalyzers.GetEffectiveDiagnostics(
            Diagnostics,
            OutputCompilation).ToImmutableArray();

    public ImmutableArray<Diagnostic> CompilerWarningsAndErrors =>
        OutputCompilation
            .GetDiagnostics()
            .Where(static diagnostic =>
                !diagnostic.Id.StartsWith("MORPH", StringComparison.Ordinal) &&
                diagnostic.Severity is
                    DiagnosticSeverity.Warning or DiagnosticSeverity.Error)
            .ToImmutableArray();

    public string TypeMapperSource => GeneratedSources
        .Single(static source => source.HintName.Contains(
            ".TypeMapper.",
            StringComparison.Ordinal))
        .SourceText
        .ToString();
}

internal readonly record struct GeneratorTestSourceFile(
    string Name,
    string Source);
