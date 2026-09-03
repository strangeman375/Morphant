using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.UnitTests.CompatibilityDiagnosticsTests;

internal static class CompatibilityGeneratorTest
{
    private static readonly ImmutableArray<MetadataReference>
        FrameworkReferences = BuildFrameworkReferences();

    public static PortableExecutableReference ActualRuntimeReference =>
        MetadataReference.CreateFromFile(typeof(TypeMapper<>).Assembly.Location);

    public static CompatibilityGeneratorResult Run(
        LanguageVersion languageVersion,
        IReadOnlyCollection<string>? sources = null,
        IReadOnlyCollection<MetadataReference>? references = null,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null,
        GeneratorDriver? driver = null)
    {
        var parseOptions = new CSharpParseOptions(
            languageVersion,
            DocumentationMode.Diagnose);
        var syntaxTrees = (sources ?? [EmptySource])
            .Select((source, index) =>
                CSharpSyntaxTree.ParseText(
                    SourceText.From(source, Encoding.UTF8),
                    parseOptions,
                    $"TestCase{index}.cs"))
            .ToImmutableArray();
        var specificDiagnosticOptions = diagnosticOptions is null
            ? ImmutableDictionary<string, ReportDiagnostic>.Empty
            : diagnosticOptions.ToImmutableDictionary(StringComparer.Ordinal);
        var compilation = CSharpCompilation.Create(
            "CompatibilityConsumer",
            syntaxTrees,
            FrameworkReferences.AddRange(references ?? []),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                specificDiagnosticOptions: specificDiagnosticOptions));
        driver ??= CSharpGeneratorDriver.Create(
            [new MorphantGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);
        driver = driver.WithUpdatedParseOptions(parseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out _);
        var runResult = driver.GetRunResult();
        var generatorResult = runResult.Results.Single();
        var unexpectedCompilerDiagnostics = outputCompilation.GetDiagnostics()
            .Where(diagnostic =>
                !diagnostic.Id.StartsWith("MORPH", StringComparison.Ordinal) &&
                diagnostic.Severity is
                    DiagnosticSeverity.Warning or
                    DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.That(
            generatorResult.Exception,
            Is.Null,
            "The production generator must not throw.");
        Assert.That(
            unexpectedCompilerDiagnostics,
            Is.Empty,
            "The consumer must not have unrelated compiler diagnostics." +
            Environment.NewLine +
            string.Join(Environment.NewLine, unexpectedCompilerDiagnostics));

        return new CompatibilityGeneratorResult(
            driver,
            compilation,
            outputCompilation,
            generatorResult.Diagnostics,
            generatorResult.GeneratedSources);
    }

    public static PortableExecutableReference CreateReference(
        string assemblyName,
        string source)
    {
        var parseOptions = new CSharpParseOptions(
            LanguageVersion.CSharp9,
            DocumentationMode.Diagnose);
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [
                CSharpSyntaxTree.ParseText(
                    SourceText.From(source, Encoding.UTF8),
                    parseOptions,
                    assemblyName + ".cs")
            ],
            FrameworkReferences,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        var failures = emitResult.Diagnostics
            .Where(static diagnostic =>
                diagnostic.Severity is
                    DiagnosticSeverity.Warning or
                    DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .ToArray();

        Assert.That(
            failures,
            Is.Empty,
            $"Reference '{assemblyName}' must compile without diagnostics." +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));

        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    public static void AssertDiagnostics(
        CompatibilityGeneratorResult result,
        params ExpectedCompatibilityDiagnostic[] expected)
    {
        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Id),
            Is.EqualTo(expected.Select(static diagnostic => diagnostic.Id)),
            string.Join(
                Environment.NewLine,
                result.Diagnostics.Select(static diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage())));
        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.GetMessage()),
            Is.EqualTo(expected.Select(static diagnostic => diagnostic.Message)));

        foreach (var diagnostic in result.Diagnostics)
        {
            Assert.Multiple(() =>
            {
                Assert.That(diagnostic.Location, Is.EqualTo(Location.None));
                Assert.That(diagnostic.AdditionalLocations, Is.Empty);
            });
        }
    }

    private static ImmutableArray<MetadataReference> BuildFrameworkReferences()
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

    public const string EmptySource =
"""
#nullable enable
#pragma warning disable CS1591

namespace TestCase
{
    public sealed class Placeholder
    {
    }
}
""";

    public const string MapperSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; set; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
        }
    }
}
""";

}

internal sealed record CompatibilityGeneratorResult(
    GeneratorDriver Driver,
    CSharpCompilation InputCompilation,
    Compilation OutputCompilation,
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<GeneratedSourceResult> GeneratedSources);

internal sealed record ExpectedCompatibilityDiagnostic(
    string Id,
    string Message);
