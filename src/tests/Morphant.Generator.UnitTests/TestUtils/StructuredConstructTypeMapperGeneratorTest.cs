using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class StructuredConstructTypeMapperGeneratorTest
    : CSharpSourceGeneratorTest<
        TestStructuredConstructTypeMapperGenerator,
        DefaultVerifier>
{
    private readonly LanguageVersion _languageVersion;

    private StructuredConstructTypeMapperGeneratorTest(
        LanguageVersion languageVersion)
    {
        _languageVersion = languageVersion;
        CompilerDiagnostics = CompilerDiagnostics.Warnings;
        TestState.AdditionalReferences.Add(typeof(TypeMapper).Assembly);
    }

    protected override ParseOptions CreateParseOptions()
    {
        return new CSharpParseOptions(
            _languageVersion,
            DocumentationMode.Diagnose);
    }

    public static async Task RunAndAssert(
        LanguageVersion languageVersion,
        string source,
        params (string FileName, string Content)[] expectedSources)
    {
        var test =
            new StructuredConstructTypeMapperGeneratorTest(
                languageVersion)
            {
                TestCode = source
            };

        foreach (var expectedSource in expectedSources)
        {
            test.TestState.GeneratedSources.Add(
            (
                typeof(TestStructuredConstructTypeMapperGenerator),
                expectedSource.FileName,
                GeneratedSourceText.Normalize(expectedSource.Content)
            ));
        }

        await test.RunAsync();
    }

    public static void RunAndAssertDiagnostics(
        LanguageVersion languageVersion,
        string source,
        params string[] expectedDiagnostics)
    {
        var parseOptions = new CSharpParseOptions(
            languageVersion,
            DocumentationMode.Diagnose);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            parseOptions,
            "TestCase.cs");
        var compilation = CSharpCompilation.Create(
            "MorphantStructuredConstructDiagnostics_" +
            Guid.NewGuid().ToString("N"),
            [syntaxTree],
            BuildReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions:
                    NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [
                new TestStructuredConstructTypeMapperGenerator()
                    .AsSourceGenerator()
            ],
            parseOptions: parseOptions);

        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);

        var actualDiagnostics = generatorDiagnostics
            .Concat(outputCompilation.GetDiagnostics())
            .Where(diagnostic =>
                diagnostic.Severity is
                    DiagnosticSeverity.Warning or
                    DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString())
            .ToArray();

        Assert.That(
            actualDiagnostics,
            Is.EqualTo(expectedDiagnostics));
    }

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var trustedPlatformAssemblies =
            (string?)AppContext.GetData(
                "TRUSTED_PLATFORM_ASSEMBLIES") ??
            throw new InvalidOperationException(
                "Trusted platform assemblies are unavailable.");
        var references = trustedPlatformAssemblies
            .Split(Path.PathSeparator)
            .Select(path =>
                (MetadataReference)
                MetadataReference.CreateFromFile(path))
            .ToImmutableArray()
            .Add(
                MetadataReference.CreateFromFile(
                    typeof(TypeMapper).Assembly.Location));

        return references;
    }

}
