using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.UnitTests.TestUtils;

internal static class GeneratorActualizationTest
{
    private const string SourcePath = "TestCase.cs";
    private static readonly ImmutableArray<MetadataReference>
        DefaultReferences = BuildDefaultReferences();

    public static GeneratorActualizationStep Step(
        string name,
        string source,
        params (string HintName, string Source)[] expectedSources)
    {
        return CreateStep(
            name,
            [SourceFile(SourcePath, source)],
            [],
            null,
            expectedSources);
    }

    public static GeneratorActualizationStep Step(
        string name,
        IReadOnlyCollection<GeneratorActualizationSourceFile> sourceFiles,
        params (string HintName, string Source)[] expectedSources)
    {
        return CreateStep(
            name,
            sourceFiles,
            [],
            null,
            expectedSources);
    }

    public static GeneratorActualizationStep StepWithReferences(
        string name,
        string source,
        IReadOnlyCollection<MetadataReference> additionalReferences,
        params (string HintName, string Source)[] expectedSources)
    {
        return CreateStep(
            name,
            [SourceFile(SourcePath, source)],
            additionalReferences,
            null,
            expectedSources);
    }

    public static GeneratorActualizationStep ExecutableStep(
        string name,
        string source,
        string scenarioTypeName,
        params (string HintName, string Source)[] expectedSources)
    {
        return CreateStep(
            name,
            [SourceFile(SourcePath, source)],
            [],
            scenarioTypeName,
            expectedSources);
    }

    public static GeneratorActualizationSourceFile SourceFile(
        string path,
        string source)
    {
        return new GeneratorActualizationSourceFile(path, source);
    }

    public static PortableExecutableReference CreateReference(
        string assemblyName,
        string source)
    {
        var parseOptions = CreateParseOptions(LanguageVersion.CSharp9);
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [ParseSource(source, assemblyName + ".cs", parseOptions)],
            DefaultReferences,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);

        AssertNoWarningsOrErrors(
            $"Reference '{assemblyName}'",
            emitResult.Diagnostics);

        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    public static void RunAndAssert(
        LanguageVersion languageVersion,
        IIncrementalGenerator generator,
        params GeneratorActualizationStep[] steps)
    {
        Assert.That(steps, Is.Not.Empty);

        var parseOptions = CreateParseOptions(languageVersion);
        var compilation = CSharpCompilation.Create(
            "MorphantActualization",
            references: DefaultReferences,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            parseOptions: parseOptions);

        foreach (var step in steps)
        {
            compilation = ApplyStep(
                compilation,
                step,
                parseOptions);
            driver = driver.RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out var generatorDiagnostics);

            AssertNoWarningsOrErrors(
                $"Step '{step.Name}'",
                generatorDiagnostics.Concat(
                    outputCompilation.GetDiagnostics()));
            AssertGeneratedSources(step, driver.GetRunResult());

            if (step.ScenarioTypeName is { } scenarioTypeName)
            {
                GeneratedCodeExecution.AssertScenario(
                    step.Name,
                    outputCompilation,
                    scenarioTypeName);
            }
        }
    }

    private static GeneratorActualizationStep CreateStep(
        string name,
        IReadOnlyCollection<GeneratorActualizationSourceFile> sourceFiles,
        IReadOnlyCollection<MetadataReference> additionalReferences,
        string? scenarioTypeName,
        IReadOnlyCollection<(string HintName, string Source)> expectedSources)
    {
        return new GeneratorActualizationStep(
            name,
            sourceFiles.ToImmutableArray(),
            additionalReferences.ToImmutableArray(),
            expectedSources
                .Select(static expectedSource =>
                    new ExpectedActualizedSource(
                        expectedSource.HintName,
                        GeneratedSourceText.Normalize(expectedSource.Source)))
                .ToImmutableArray(),
            scenarioTypeName);
    }

    private static CSharpCompilation ApplyStep(
        CSharpCompilation compilation,
        GeneratorActualizationStep step,
        CSharpParseOptions parseOptions)
    {
        var sourceFilesByPath = step.SourceFiles.ToDictionary(
            static sourceFile => sourceFile.Path,
            StringComparer.Ordinal);

        foreach (var previousTree in compilation.SyntaxTrees)
        {
            if (!sourceFilesByPath.ContainsKey(previousTree.FilePath))
            {
                compilation = compilation.RemoveSyntaxTrees(previousTree);
            }
        }

        foreach (var sourceFile in step.SourceFiles)
        {
            var previousTree = compilation.SyntaxTrees.SingleOrDefault(
                tree => tree.FilePath == sourceFile.Path);

            if (previousTree is not null &&
                previousTree.GetText().ToString() == sourceFile.Source)
            {
                continue;
            }

            var sourceTree = ParseSource(
                sourceFile.Source,
                sourceFile.Path,
                parseOptions);

            compilation = previousTree is null
                ? compilation.AddSyntaxTrees(sourceTree)
                : compilation.ReplaceSyntaxTree(previousTree, sourceTree);
        }

        return compilation.WithReferences(
            DefaultReferences.AddRange(step.AdditionalReferences));
    }

    private static void AssertGeneratedSources(
        GeneratorActualizationStep step,
        GeneratorDriverRunResult runResult)
    {
        var generatorResult = runResult.Results.Single();

        Assert.That(
            generatorResult.Exception,
            Is.Null,
            $"Step '{step.Name}' must not throw from the generator.");

        var actualSources = generatorResult.GeneratedSources
            .OrderBy(
                static generatedSource => generatedSource.HintName,
                StringComparer.Ordinal)
            .ToArray();
        var expectedSources = step.ExpectedSources
            .OrderBy(
                static expectedSource => expectedSource.HintName,
                StringComparer.Ordinal)
            .ToArray();

        Assert.That(
            actualSources.Select(static source => source.HintName),
            Is.EqualTo(
                expectedSources.Select(static source => source.HintName)),
            $"Step '{step.Name}' generated an unexpected file set.");

        for (var index = 0; index < expectedSources.Length; index++)
        {
            Assert.That(
                actualSources[index].SourceText.ToString(),
                Is.EqualTo(expectedSources[index].Source),
                $"Step '{step.Name}', file " +
                $"'{expectedSources[index].HintName}'.");
        }
    }

    private static SyntaxTree ParseSource(
        string source,
        string path,
        CSharpParseOptions parseOptions)
    {
        return CSharpSyntaxTree.ParseText(
            SourceText.From(source, Encoding.UTF8),
            parseOptions,
            path);
    }

    private static CSharpParseOptions CreateParseOptions(
        LanguageVersion languageVersion)
    {
        return new CSharpParseOptions(
            languageVersion,
            DocumentationMode.Diagnose);
    }

    private static ImmutableArray<MetadataReference>
        BuildDefaultReferences()
    {
        var trustedPlatformAssemblies =
            (string?)AppContext.GetData(
                "TRUSTED_PLATFORM_ASSEMBLIES") ??
            throw new InvalidOperationException(
                "Trusted platform assemblies are unavailable.");
        var referencePaths = trustedPlatformAssemblies
            .Split(Path.PathSeparator)
            .Append(typeof(TypeMapper).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return referencePaths
            .Select(static path =>
                (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    private static void AssertNoWarningsOrErrors(
        string scope,
        IEnumerable<Diagnostic> diagnostics)
    {
        var failures = diagnostics
            .Where(static diagnostic =>
                diagnostic.Severity is
                    DiagnosticSeverity.Warning or
                    DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.That(
            failures,
            Is.Empty,
            scope + " must compile without warnings or errors." +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));
    }

}

internal sealed record GeneratorActualizationStep(
    string Name,
    ImmutableArray<GeneratorActualizationSourceFile> SourceFiles,
    ImmutableArray<MetadataReference> AdditionalReferences,
    ImmutableArray<ExpectedActualizedSource> ExpectedSources,
    string? ScenarioTypeName);

internal sealed record GeneratorActualizationSourceFile(
    string Path,
    string Source);

internal sealed record ExpectedActualizedSource(
    string HintName,
    string Source);
