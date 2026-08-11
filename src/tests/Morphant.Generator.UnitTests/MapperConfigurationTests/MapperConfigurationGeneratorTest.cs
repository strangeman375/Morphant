using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Morphant.Generator.UnitTests.MapperConfigurationTests;

internal static class MapperConfigurationGeneratorTest
{
    private static readonly ImmutableArray<MetadataReference>
        FrameworkReferences = BuildFrameworkReferences();

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
        var references = FrameworkReferences.Add(
            MetadataReference.CreateFromFile(
                typeof(TypeMapper).Assembly.Location));

        if (additionalReferences is not null)
        {
            references = references.AddRange(additionalReferences);
        }

        var compilation = CSharpCompilation.Create(
            "MapperConfigurationConsumer",
            syntaxTrees,
            references,
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

        return new MapperConfigurationGeneratorResult(
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

internal sealed record MapperConfigurationGeneratorResult(
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
}

internal readonly record struct ConfigurationSourceFile(
    string Name,
    string Source);
