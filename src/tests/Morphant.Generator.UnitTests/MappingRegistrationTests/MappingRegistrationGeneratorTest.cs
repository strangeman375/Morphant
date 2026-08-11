using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Morphant.Generator.UnitTests.MappingRegistrationTests;

internal static class MappingRegistrationGeneratorTest
{
    private static readonly ImmutableArray<MetadataReference>
        FrameworkReferences = BuildFrameworkReferences();

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
        var parseOptions = new CSharpParseOptions(
            languageVersion,
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
            "MappingRegistrationConsumer",
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

        return new MappingRegistrationGeneratorResult(
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

internal sealed record MappingRegistrationGeneratorResult(
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

internal readonly record struct RegistrationSourceFile(
    string Name,
    string Source);
