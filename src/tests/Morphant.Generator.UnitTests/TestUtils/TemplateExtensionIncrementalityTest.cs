using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.UnitTests.TestUtils;

internal static class TemplateExtensionIncrementalityTest
{
    // Literal contract: do not import the generator's stage-name constants.
    private const string BuildTemplateExtensionRequestsStage =
        "BuildTemplateExtensionRequests";

    private static readonly CSharpParseOptions DefaultParseOptions = new(
        LanguageVersion.CSharp9,
        DocumentationMode.Diagnose);

    private static readonly ImmutableArray<MetadataReference>
        DefaultReferences = BuildDefaultReferences();

    public static TemplateExtensionIncrementalitySourceFile SourceFile(
        string path,
        string source)
    {
        return new TemplateExtensionIncrementalitySourceFile(path, source);
    }

    public static TemplateExtensionIncrementalityExpectedOutput Expected(
        string hintName,
        IncrementalStepRunReason reason)
    {
        return new TemplateExtensionIncrementalityExpectedOutput(
            hintName,
            reason);
    }

    public static TemplateExtensionIncrementalityStep Step(
        string name,
        IReadOnlyCollection<TemplateExtensionIncrementalitySourceFile>
            sourceFiles,
        params TemplateExtensionIncrementalityExpectedOutput[]
            expectedOutputs)
    {
        return Step(
            name,
            sourceFiles,
            Array.Empty<MetadataReference>(),
            NullableContextOptions.Enable,
            expectedOutputs);
    }

    public static TemplateExtensionIncrementalityStep Step(
        string name,
        IReadOnlyCollection<TemplateExtensionIncrementalitySourceFile>
            sourceFiles,
        IReadOnlyCollection<MetadataReference> additionalReferences,
        params TemplateExtensionIncrementalityExpectedOutput[]
            expectedOutputs)
    {
        return Step(
            name,
            sourceFiles,
            additionalReferences,
            NullableContextOptions.Enable,
            expectedOutputs);
    }

    public static TemplateExtensionIncrementalityStep Step(
        string name,
        IReadOnlyCollection<TemplateExtensionIncrementalitySourceFile>
            sourceFiles,
        NullableContextOptions nullableContextOptions,
        params TemplateExtensionIncrementalityExpectedOutput[]
            expectedOutputs)
    {
        return Step(
            name,
            sourceFiles,
            Array.Empty<MetadataReference>(),
            nullableContextOptions,
            expectedOutputs);
    }

    private static TemplateExtensionIncrementalityStep Step(
        string name,
        IReadOnlyCollection<TemplateExtensionIncrementalitySourceFile>
            sourceFiles,
        IReadOnlyCollection<MetadataReference> additionalReferences,
        NullableContextOptions nullableContextOptions,
        params TemplateExtensionIncrementalityExpectedOutput[]
            expectedOutputs)
    {
        return new TemplateExtensionIncrementalityStep(
            name,
            sourceFiles.ToImmutableArray(),
            additionalReferences.ToImmutableArray(),
            nullableContextOptions,
            expectedOutputs.ToImmutableArray());
    }

    public static void RunAndAssert(
        params TemplateExtensionIncrementalityStep[] steps)
    {
        RunAndAssert(LanguageVersion.CSharp9, steps);
    }

    public static void RunAndAssert(
        LanguageVersion languageVersion,
        params TemplateExtensionIncrementalityStep[] steps)
    {
        Assert.That(steps, Is.Not.Empty);

        var compilation = CreateCompilation();
        var parseOptions = new CSharpParseOptions(
            languageVersion,
            DocumentationMode.Diagnose);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[]
            {
                new TestTemplateExtensionGenerator().AsSourceGenerator()
            },
            additionalTexts: Array.Empty<AdditionalText>(),
            parseOptions: parseOptions,
            optionsProvider: null,
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));

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

            AssertNoErrors(
                step.Name,
                generatorDiagnostics,
                outputCompilation.GetDiagnostics());

            AssertIncrementality(
                step,
                driver.GetRunResult());
        }
    }

    public static PortableExecutableReference CreateReference(
        string assemblyName,
        string source)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[]
            {
                ParseSource(
                    source,
                    assemblyName + ".cs",
                    DefaultParseOptions)
            },
            DefaultReferences,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);

        Assert.That(
            emitResult.Diagnostics
                .Where(static diagnostic =>
                    diagnostic.Severity == DiagnosticSeverity.Error),
            Is.Empty,
            $"Reference '{assemblyName}' must compile successfully.");

        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static CSharpCompilation CreateCompilation()
    {
        return CSharpCompilation.Create(
            "TemplateExtensionIncrementality",
            references: DefaultReferences,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    private static CSharpCompilation ApplyStep(
        CSharpCompilation compilation,
        TemplateExtensionIncrementalityStep step,
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

        return compilation
            .WithOptions(
                ((CSharpCompilationOptions)compilation.Options)
                .WithNullableContextOptions(
                    step.NullableContextOptions))
            .WithReferences(
                DefaultReferences.AddRange(step.AdditionalReferences));
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

    private static ImmutableArray<MetadataReference>
        BuildDefaultReferences()
    {
        var trustedPlatformAssemblies =
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ??
            throw new InvalidOperationException(
                "TRUSTED_PLATFORM_ASSEMBLIES is unavailable.");

        var referencePaths = trustedPlatformAssemblies
            .Split(Path.PathSeparator)
            .Append(typeof(TypeMapper).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return referencePaths
            .Select(static path =>
                MetadataReference.CreateFromFile(path))
            .ToImmutableArray<MetadataReference>();
    }

    private static void AssertNoErrors(
        string stepName,
        params IEnumerable<Diagnostic>[] diagnosticGroups)
    {
        var errors = diagnosticGroups
            .SelectMany(static diagnostics => diagnostics)
            .Where(static diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.That(
            errors,
            Is.Empty,
            $"Step '{stepName}' must compile without errors.");
    }

    private static void AssertIncrementality(
        TemplateExtensionIncrementalityStep step,
        GeneratorDriverRunResult runResult)
    {
        var generatorResult = runResult.Results.Single();

        Assert.That(
            generatorResult.Exception,
            Is.Null,
            $"Step '{step.Name}' must not throw from the generator.");

        if (!generatorResult.TrackedSteps.TryGetValue(
                BuildTemplateExtensionRequestsStage,
                out var trackedSteps))
        {
            Assert.That(
                step.ExpectedOutputs,
                Is.Empty,
                $"Step '{step.Name}' did not track stage " +
                $"'{BuildTemplateExtensionRequestsStage}'.");

            Assert.That(
                generatorResult.GeneratedSources,
                Is.Empty,
                $"Step '{step.Name}' generated an unexpected file set.");

            return;
        }

        var actualOutputs = trackedSteps
            .SelectMany(static trackedStep => trackedStep.Outputs)
            .Select(output =>
                new TemplateExtensionTrackedOutput(
                    GetTrackedHintName(
                        output.Value,
                        step.Name),
                    output.Reason))
            .OrderBy(static output => output.HintName, StringComparer.Ordinal)
            .ThenBy(static output => output.Reason)
            .ToArray();

        var expectedOutputs = step.ExpectedOutputs
            .Select(static expected =>
                new TemplateExtensionTrackedOutput(
                    expected.HintName,
                    expected.Reason))
            .OrderBy(static output => output.HintName, StringComparer.Ordinal)
            .ThenBy(static output => output.Reason)
            .ToArray();

        Assert.That(
            actualOutputs,
            Is.EqualTo(expectedOutputs),
            $"Step '{step.Name}', stage " +
            $"'{BuildTemplateExtensionRequestsStage}'.");

        var expectedGeneratedHintNames = step.ExpectedOutputs
            .Where(static expected =>
                expected.Reason != IncrementalStepRunReason.Removed)
            .Select(static expected => expected.HintName)
            .OrderBy(static hintName => hintName, StringComparer.Ordinal);

        var actualGeneratedHintNames = generatorResult.GeneratedSources
            .Select(static source => source.HintName)
            .OrderBy(static hintName => hintName, StringComparer.Ordinal);

        Assert.That(
            actualGeneratedHintNames,
            Is.EqualTo(expectedGeneratedHintNames),
            $"Step '{step.Name}' generated an unexpected file set.");
    }

    private static string GetTrackedHintName(
        object value,
        string stepName)
    {
        var hintNameProperty = value.GetType().GetProperty("HintName");

        Assert.That(
            hintNameProperty,
            Is.Not.Null,
            $"Step '{stepName}', stage " +
            $"'{BuildTemplateExtensionRequestsStage}' produced an " +
            "output without a HintName property.");

        var hintName = hintNameProperty!.GetValue(value);

        Assert.That(
            hintName,
            Is.TypeOf<string>(),
            $"Step '{stepName}', stage " +
            $"'{BuildTemplateExtensionRequestsStage}' produced an " +
            "output without a string HintName value.");

        return (string)hintName!;
    }
}

internal sealed record TemplateExtensionIncrementalityStep(
    string Name,
    ImmutableArray<TemplateExtensionIncrementalitySourceFile> SourceFiles,
    ImmutableArray<MetadataReference> AdditionalReferences,
    NullableContextOptions NullableContextOptions,
    ImmutableArray<TemplateExtensionIncrementalityExpectedOutput>
        ExpectedOutputs);

internal sealed record TemplateExtensionIncrementalitySourceFile(
    string Path,
    string Source);

internal sealed record TemplateExtensionIncrementalityExpectedOutput(
    string HintName,
    IncrementalStepRunReason Reason);

internal sealed record TemplateExtensionTrackedOutput(
    string HintName,
    IncrementalStepRunReason Reason);
