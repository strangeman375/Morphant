using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestAssets;

namespace Morphant.Generator.IntegrationTests.TestUtils;

internal static class ProductionGeneratorIntegrationTest
{
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
            "MorphantProductionRuntime_" + Guid.NewGuid().ToString("N"),
            [syntaxTree],
            BuildReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new MorphantGenerator().AsSourceGenerator()],
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

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var trustedPlatformAssemblies =
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ??
            throw new InvalidOperationException(
                "Trusted platform assemblies are unavailable.");

        return trustedPlatformAssemblies
            .Split(Path.PathSeparator)
            .Select(static path =>
                (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray()
            .Add(
                MetadataReference.CreateFromFile(
                    typeof(TypeMapper).Assembly.Location))
            .Add(
                MetadataReference.CreateFromFile(
                    typeof(ReferencedNestedSource).Assembly.Location));
    }

    private static void AssertNoWarningsOrErrors(
        IEnumerable<Diagnostic> diagnostics)
    {
        var failures = diagnostics
            .Where(static diagnostic =>
                diagnostic.Severity is
                    DiagnosticSeverity.Warning or
                    DiagnosticSeverity.Error)
            .ToArray();

        Assert.That(
            failures,
            Is.Empty,
            string.Join(
                Environment.NewLine,
                failures.Select(static diagnostic =>
                    diagnostic.ToString())));
    }
}
