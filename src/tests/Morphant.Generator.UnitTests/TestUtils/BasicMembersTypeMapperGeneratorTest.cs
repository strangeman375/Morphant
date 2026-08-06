using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Morphant.Generator.UnitTests.TestAssets;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class BasicMembersTypeMapperGeneratorTest
    : CSharpSourceGeneratorTest<
        TestBasicMembersTypeMapperGenerator,
        DefaultVerifier>
{
    private const string NewLine = "\r\n";

    private readonly LanguageVersion _languageVersion;

    private BasicMembersTypeMapperGeneratorTest(
        LanguageVersion languageVersion)
    {
        _languageVersion = languageVersion;
        CompilerDiagnostics = CompilerDiagnostics.Warnings;
        TestState.AdditionalReferences.Add(typeof(TypeMapper).Assembly);
        TestState.AdditionalReferences.Add(
            typeof(ReferencedNestedSource).Assembly);
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
        var test = new BasicMembersTypeMapperGeneratorTest(
            languageVersion)
        {
            TestCode = source
        };

        AddExpectedSources(test, expectedSources);
        await test.RunAsync();
    }

    public static async Task RunAndAssertWithAnalyzerConfig(
        LanguageVersion languageVersion,
        string source,
        string analyzerConfig,
        params (string FileName, string Content)[] expectedSources)
    {
        var test = new BasicMembersTypeMapperGeneratorTest(
            languageVersion)
        {
            TestCode = source
        };

        test.TestState.AnalyzerConfigFiles.Add(
        (
            "/.globalconfig",
            analyzerConfig
        ));
        AddExpectedSources(test, expectedSources);
        await test.RunAsync();
    }

    // TODO: Move runtime compilation and execution to
    // Morphant.Generator.IntegrationTests.
    public static void RunAndExecute(
        LanguageVersion languageVersion,
        string source,
        string scenarioTypeName)
    {
        var parseOptions = new CSharpParseOptions(
            languageVersion,
            DocumentationMode.Diagnose);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            parseOptions,
            "TestCase.cs");
        var compilation = CSharpCompilation.Create(
            "MorphantBasicMembersRuntime_" +
            Guid.NewGuid().ToString("N"),
            [syntaxTree],
            BuildReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions:
                    NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [
                new TestBasicMembersTypeMapperGenerator()
                    .AsSourceGenerator()
            ],
            parseOptions: parseOptions);

        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);

        AssertNoWarningsOrErrors(
            generatorDiagnostics.Concat(
                outputCompilation.GetDiagnostics()));

        using var stream = new MemoryStream();
        var emitResult = outputCompilation.Emit(stream);

        AssertNoWarningsOrErrors(emitResult.Diagnostics);

        var assembly = Assembly.Load(stream.ToArray());
        var verify = assembly
            .GetType(scenarioTypeName, throwOnError: true)!
            .GetMethod(
                "Verify",
                BindingFlags.Public | BindingFlags.Static) ??
            throw new InvalidOperationException(
                $"{scenarioTypeName}.Verify was not found.");

        try
        {
            verify.Invoke(null, null);
        }
        catch (TargetInvocationException exception)
        {
            throw new AssertionException(
                exception.InnerException?.ToString() ??
                exception.ToString());
        }
    }

    private static void AddExpectedSources(
        BasicMembersTypeMapperGeneratorTest test,
        IEnumerable<(string FileName, string Content)> expectedSources)
    {
        foreach (var expectedSource in expectedSources)
        {
            test.TestState.GeneratedSources.Add(
            (
                typeof(TestBasicMembersTypeMapperGenerator),
                expectedSource.FileName,
                NormalizeGeneratedSource(expectedSource.Content)
            ));
        }
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
                    typeof(TypeMapper).Assembly.Location))
            .Add(
                MetadataReference.CreateFromFile(
                    typeof(ReferencedNestedSource).Assembly.Location));

        return references;
    }

    private static void AssertNoWarningsOrErrors(
        IEnumerable<Diagnostic> diagnostics)
    {
        var failures = diagnostics
            .Where(diagnostic =>
                diagnostic.Severity is
                    DiagnosticSeverity.Warning or
                    DiagnosticSeverity.Error)
            .ToArray();

        Assert.That(
            failures,
            Is.Empty,
            string.Join(
                Environment.NewLine,
                failures.Select(diagnostic =>
                    diagnostic.ToString())));
    }

    private static string NormalizeGeneratedSource(string source)
    {
        var normalized = source
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Replace("\n", NewLine);

        return normalized.EndsWith(
            NewLine,
            StringComparison.Ordinal)
            ? normalized
            : normalized + NewLine;
    }
}
