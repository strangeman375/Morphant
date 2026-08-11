using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.MappingSettingsDiagnosticsTests;

internal static class MappingSettingsDiagnosticsGeneratorTest
{
    private static readonly ImmutableArray<MetadataReference>
        FrameworkReferences = BuildFrameworkReferences();

    public static MappingSettingsDiagnosticsGeneratorResult Run(
        string source,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions =
            null,
        IReadOnlyDictionary<string, string>? globalOptions = null,
        GeneratorDriver? driver = null,
        LanguageVersion languageVersion = LanguageVersion.CSharp9)
    {
        var parseOptions = new CSharpParseOptions(
            languageVersion,
            DocumentationMode.Diagnose);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            Microsoft.CodeAnalysis.Text.SourceText.From(source, Encoding.UTF8),
            parseOptions,
            "TestCase.cs");
        var options = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: NullableContextOptions.Enable,
            specificDiagnosticOptions: diagnosticOptions is null
                ? ImmutableDictionary<string, ReportDiagnostic>.Empty
                : diagnosticOptions.ToImmutableDictionary(
                    StringComparer.Ordinal));
        var compilation = CSharpCompilation.Create(
            "MappingSettingsDiagnosticsConsumer",
            [syntaxTree],
            FrameworkReferences.Add(
                MetadataReference.CreateFromFile(
                    typeof(TypeMapper).Assembly.Location)),
            options);
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

        return new MappingSettingsDiagnosticsGeneratorResult(
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

    public static int Line(Location location)
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

internal sealed record MappingSettingsDiagnosticsGeneratorResult(
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
