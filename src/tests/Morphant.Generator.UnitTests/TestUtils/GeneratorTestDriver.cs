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
        IEnumerable<MetadataReference>? additionalReferences = null,
        CSharpCompilationOptions? compilationOptions = null)
    {
        return Run(
            assemblyName,
            [new GeneratorTestSourceFile("TestCase.cs", source)],
            languageVersion,
            diagnosticOptions,
            globalOptions,
            driver,
            additionalReferences,
            compilationOptions);
    }

    public static GeneratorTestDriverResult Run(
        string assemblyName,
        IReadOnlyCollection<GeneratorTestSourceFile> sourceFiles,
        LanguageVersion languageVersion,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions =
            null,
        IReadOnlyDictionary<string, string>? globalOptions = null,
        GeneratorDriver? driver = null,
        IEnumerable<MetadataReference>? additionalReferences = null,
        CSharpCompilationOptions? compilationOptions = null)
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
                typeof(TypeMapper<>).Assembly.Location));

        if (additionalReferences is not null)
        {
            references = references.AddRange(additionalReferences);
        }

        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            references,
            compilationOptions ?? new CSharpCompilationOptions(
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

        var isWarmRun = driver is not null;
        driver = driver is null
            ? CSharpGeneratorDriver.Create(
                [new MorphantGenerator().AsSourceGenerator()],
                optionsProvider: analyzerOptions,
                parseOptions: parseOptions)
            : driver
                .WithUpdatedParseOptions(parseOptions)
                .WithUpdatedAnalyzerConfigOptions(analyzerOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out _);
        var generatorResult = driver.GetRunResult().Results.Single();

        Assert.That(
            generatorResult.Exception,
            Is.Null,
            "The production generator must not throw.");

        if (isWarmRun)
        {
            AssertMatchesFreshRun(
                compilation,
                parseOptions,
                analyzerOptions,
                generatorResult,
                outputCompilation);
        }

        return new GeneratorTestDriverResult(
            driver,
            outputCompilation,
            generatorResult.Diagnostics,
            generatorResult.GeneratedSources);
    }

    private static void AssertMatchesFreshRun(
        CSharpCompilation compilation,
        CSharpParseOptions parseOptions,
        TestAnalyzerConfigOptionsProvider analyzerOptions,
        GeneratorRunResult warmResult,
        Compilation warmOutputCompilation)
    {
        GeneratorDriver freshDriver = CSharpGeneratorDriver.Create(
            [new MorphantGenerator().AsSourceGenerator()],
            optionsProvider: analyzerOptions,
            parseOptions: parseOptions);
        freshDriver = freshDriver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var freshOutputCompilation,
            out _);
        var freshResult = freshDriver.GetRunResult().Results.Single();

        Assert.Multiple(() =>
        {
            Assert.That(
                freshResult.Exception,
                Is.Null,
                "The production generator must not throw on a fresh run.");
            Assert.That(
                GeneratedSources(warmResult),
                Is.EqualTo(GeneratedSources(freshResult)),
                "A reused generator driver produced stale generated " +
                "sources.");
            Assert.That(
                Diagnostics(warmResult.Diagnostics),
                Is.EqualTo(Diagnostics(freshResult.Diagnostics)),
                "A reused generator driver produced stale generator " +
                "diagnostics.");
            Assert.That(
                Diagnostics(warmOutputCompilation.GetDiagnostics()),
                Is.EqualTo(Diagnostics(
                    freshOutputCompilation.GetDiagnostics())),
                "A reused generator driver produced stale compiler " +
                "diagnostics.");
        });
    }

    private static GeneratedSourceSnapshot[] GeneratedSources(
        GeneratorRunResult result)
    {
        return result.GeneratedSources
            .Select(static source =>
                new GeneratedSourceSnapshot(
                    source.HintName,
                    source.SourceText.ToString()))
            .OrderBy(static source => source.HintName, StringComparer.Ordinal)
            .ToArray();
    }

    private static DiagnosticSnapshot[] Diagnostics(
        IEnumerable<Diagnostic> diagnostics)
    {
        return diagnostics
            .Where(static diagnostic =>
                diagnostic.Severity is
                    DiagnosticSeverity.Warning or DiagnosticSeverity.Error)
            .Select(static diagnostic =>
                new DiagnosticSnapshot(
                    diagnostic.Id,
                    diagnostic.Severity,
                    diagnostic.WarningLevel,
                    diagnostic.GetMessage(),
                    diagnostic.Location.SourceTree?.FilePath,
                    diagnostic.Location.IsInSource
                        ? diagnostic.Location.SourceSpan.Start
                        : -1,
                    diagnostic.Location.IsInSource
                        ? diagnostic.Location.SourceSpan.Length
                        : 0,
                    diagnostic.IsSuppressed,
                    Locations(diagnostic.AdditionalLocations),
                    Properties(diagnostic.Properties)))
            .OrderBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ThenBy(
                static diagnostic => diagnostic.Path,
                StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Start)
            .ThenBy(static diagnostic => diagnostic.Length)
            .ThenBy(
                static diagnostic => diagnostic.Message,
                StringComparer.Ordinal)
            .ToArray();
    }

    private static string Locations(IEnumerable<Location> locations)
    {
        return string.Join(
            "\u001f",
            locations.Select(static location =>
                $"{location.SourceTree?.FilePath}\u001e" +
                $"{(location.IsInSource ? location.SourceSpan.Start : -1)}" +
                "\u001e" +
                $"{(location.IsInSource ? location.SourceSpan.Length : 0)}"));
    }

    private static string Properties(
        IReadOnlyDictionary<string, string?> properties)
    {
        return string.Join(
            "\u001f",
            properties
                .OrderBy(static property => property.Key, StringComparer.Ordinal)
                .Select(static property =>
                    $"{property.Key}\u001e{property.Value}"));
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
                    typeof(TypeMapper<>).Assembly.Location)),
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

internal sealed record GeneratedSourceSnapshot(
    string HintName,
    string Source);

internal sealed record DiagnosticSnapshot(
    string Id,
    DiagnosticSeverity Severity,
    int WarningLevel,
    string Message,
    string? Path,
    int Start,
    int Length,
    bool IsSuppressed,
    string AdditionalLocations,
    string Properties);
