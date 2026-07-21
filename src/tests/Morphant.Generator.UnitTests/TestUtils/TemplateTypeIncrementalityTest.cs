using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.TemplateSurface.TemplateType;

namespace Morphant.Generator.UnitTests.TestUtils;

internal static class TemplateTypeIncrementalityTest
{
    private static readonly CSharpParseOptions DefaultParseOptions = new(
        LanguageVersion.CSharp9,
        DocumentationMode.Diagnose);

    private static readonly ImmutableArray<MetadataReference>
        DefaultReferences = BuildDefaultReferences();

    public static TemplateTypeIncrementalitySourceFile SourceFile(
        string path,
        string source)
    {
        return new TemplateTypeIncrementalitySourceFile(path, source);
    }

    public static TemplateTypeIncrementalityExpectedOutput Expected(
        string hintName,
        IncrementalStepRunReason reason)
    {
        return Expected(hintName, reason, reason);
    }

    public static TemplateTypeIncrementalityExpectedOutput Expected(
        string hintName,
        IncrementalStepRunReason modelReason,
        IncrementalStepRunReason requestReason)
    {
        return new TemplateTypeIncrementalityExpectedOutput(
            hintName,
            modelReason,
            requestReason);
    }

    public static TemplateTypeIncrementalityStep Step(
        string name,
        IReadOnlyCollection<TemplateTypeIncrementalitySourceFile> sourceFiles,
        params TemplateTypeIncrementalityExpectedOutput[] expectedOutputs)
    {
        return Step(
            name,
            sourceFiles,
            Array.Empty<MetadataReference>(),
            NullableContextOptions.Enable,
            expectedOutputs);
    }

    public static TemplateTypeIncrementalityStep Step(
        string name,
        IReadOnlyCollection<TemplateTypeIncrementalitySourceFile> sourceFiles,
        IReadOnlyCollection<MetadataReference> additionalReferences,
        params TemplateTypeIncrementalityExpectedOutput[] expectedOutputs)
    {
        return Step(
            name,
            sourceFiles,
            additionalReferences,
            NullableContextOptions.Enable,
            expectedOutputs);
    }

    public static TemplateTypeIncrementalityStep Step(
        string name,
        IReadOnlyCollection<TemplateTypeIncrementalitySourceFile> sourceFiles,
        NullableContextOptions nullableContextOptions,
        params TemplateTypeIncrementalityExpectedOutput[] expectedOutputs)
    {
        return Step(
            name,
            sourceFiles,
            Array.Empty<MetadataReference>(),
            nullableContextOptions,
            expectedOutputs);
    }

    private static TemplateTypeIncrementalityStep Step(
        string name,
        IReadOnlyCollection<TemplateTypeIncrementalitySourceFile> sourceFiles,
        IReadOnlyCollection<MetadataReference> additionalReferences,
        NullableContextOptions nullableContextOptions,
        params TemplateTypeIncrementalityExpectedOutput[] expectedOutputs)
    {
        return new TemplateTypeIncrementalityStep(
            name,
            sourceFiles.ToImmutableArray(),
            additionalReferences.ToImmutableArray(),
            nullableContextOptions,
            expectedOutputs.ToImmutableArray());
    }

    public static void RunAndAssert(
        params TemplateTypeIncrementalityStep[] steps)
    {
        RunAndAssert(LanguageVersion.CSharp9, steps);
    }

    public static void RunAndAssert(
        LanguageVersion languageVersion,
        params TemplateTypeIncrementalityStep[] steps)
    {
        Assert.That(steps, Is.Not.Empty);

        var compilation = CreateCompilation();
        var parseOptions = new CSharpParseOptions(
            languageVersion,
            DocumentationMode.Diagnose);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[]
            {
                new TestTemplateTypeGenerator().AsSourceGenerator()
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
            "TemplateTypeIncrementality",
            references: DefaultReferences,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    private static CSharpCompilation ApplyStep(
        CSharpCompilation compilation,
        TemplateTypeIncrementalityStep step,
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
        TemplateTypeIncrementalityStep step,
        GeneratorDriverRunResult runResult)
    {
        var generatorResult = runResult.Results.Single();

        Assert.That(
            generatorResult.Exception,
            Is.Null,
            $"Step '{step.Name}' must not throw from the generator.");

        AssertTrackedOutputs<TemplateTypeModelResult>(
            step,
            generatorResult,
            MorphantGeneratorStageNames.BuildTemplateTypeModels,
            static model => model.HintName,
            static expected => expected.ModelReason);

        AssertTrackedOutputs<TemplateTypeRequest>(
            step,
            generatorResult,
            MorphantGeneratorStageNames.BuildTemplateTypeRequests,
            static request => request.HintName,
            static expected => expected.RequestReason);

        var expectedGeneratedHintNames = step.ExpectedOutputs
            .Where(static expected =>
                expected.RequestReason !=
                IncrementalStepRunReason.Removed)
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

    private static void AssertTrackedOutputs<T>(
        TemplateTypeIncrementalityStep step,
        GeneratorRunResult generatorResult,
        string stageName,
        Func<T, string> getHintName,
        Func<TemplateTypeIncrementalityExpectedOutput,
            IncrementalStepRunReason> getExpectedReason)
        where T : struct
    {
        Assert.That(
            generatorResult.TrackedSteps.TryGetValue(
                stageName,
                out var trackedSteps),
            Is.True,
            $"Step '{step.Name}' did not track stage '{stageName}'.");

        var actualOutputs = trackedSteps
            .SelectMany(static trackedStep => trackedStep.Outputs)
            .Select(output =>
            {
                Assert.That(
                    output.Value,
                    Is.TypeOf<T>(),
                    $"Step '{step.Name}', stage '{stageName}' " +
                    "produced an unexpected value type.");

                return new TemplateTypeTrackedOutput(
                    getHintName((T)output.Value),
                    output.Reason);
            })
            .OrderBy(static output => output.HintName, StringComparer.Ordinal)
            .ThenBy(static output => output.Reason)
            .ToArray();

        var expectedOutputs = step.ExpectedOutputs
            .Select(expected =>
                new TemplateTypeTrackedOutput(
                    expected.HintName,
                    getExpectedReason(expected)))
            .OrderBy(static output => output.HintName, StringComparer.Ordinal)
            .ThenBy(static output => output.Reason)
            .ToArray();

        Assert.That(
            actualOutputs,
            Is.EqualTo(expectedOutputs),
            $"Step '{step.Name}', stage '{stageName}'.");
    }
}

internal sealed record TemplateTypeIncrementalityStep(
    string Name,
    ImmutableArray<TemplateTypeIncrementalitySourceFile> SourceFiles,
    ImmutableArray<MetadataReference> AdditionalReferences,
    NullableContextOptions NullableContextOptions,
    ImmutableArray<TemplateTypeIncrementalityExpectedOutput> ExpectedOutputs);

internal sealed record TemplateTypeIncrementalitySourceFile(
    string Path,
    string Source);

internal sealed record TemplateTypeIncrementalityExpectedOutput(
    string HintName,
    IncrementalStepRunReason ModelReason,
    IncrementalStepRunReason RequestReason);

internal sealed record TemplateTypeTrackedOutput(
    string HintName,
    IncrementalStepRunReason Reason);
