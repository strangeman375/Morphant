using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.UnitTests.TestUtils;

internal static class GeneratorIncrementalityTest
{
    private static readonly ImmutableArray<MetadataReference>
        DefaultReferences = BuildDefaultReferences();

    public static GeneratorIncrementalitySourceFile SourceFile(
        string path,
        string source)
    {
        return new GeneratorIncrementalitySourceFile(path, source);
    }

    public static ExpectedIncrementalOutput Expected(
        string hintName,
        IncrementalStepRunReason reason)
    {
        return new ExpectedIncrementalOutput(hintName, reason);
    }

    public static ExpectedIncrementalStage Stage(
        string name,
        params ExpectedIncrementalOutput[] outputs)
    {
        return new ExpectedIncrementalStage(
            name,
            outputs.ToImmutableArray(),
            []);
    }

    public static ExpectedIncrementalStage StageReasons(
        string name,
        params ExpectedIncrementalReasonCount[] reasons)
    {
        return new ExpectedIncrementalStage(
            name,
            [],
            reasons.ToImmutableArray());
    }

    public static ExpectedIncrementalReasonCount Reason(
        IncrementalStepRunReason reason,
        int count)
    {
        return new ExpectedIncrementalReasonCount(reason, count);
    }

    public static ExpectedCompilerDiagnostic CompilerDiagnostic(
        string id,
        DiagnosticSeverity severity,
        string path,
        int start,
        int length,
        params ExpectedCompilerDiagnosticLocation[] additionalLocations)
    {
        return new ExpectedCompilerDiagnostic(
            id,
            severity,
            path,
            start,
            length,
            SnapshotLocations(additionalLocations));
    }

    public static ExpectedCompilerDiagnosticLocation CompilerDiagnosticLocation(
        string path,
        int start,
        int length)
    {
        return new ExpectedCompilerDiagnosticLocation(path, start, length);
    }

    public static ExpectedIncrementalStage[] EarlyPipeline(
        params ExpectedIncrementalReasonCount[] reasons)
    {
        return
        [
            StageReasons("BuildMapperDeclarationInfos", reasons),
            StageReasons("BuildTypeMapperConfigureInfos", reasons),
            StageReasons(
                "BuildPairConfigurationDiscoveryModels",
                reasons),
            StageReasons("BuildPairConfigurationModels", reasons),
            StageReasons("BuildMapperContractAnalyses", reasons)
        ];
    }

    public static GeneratorIncrementalityStep Step(
        string name,
        IReadOnlyCollection<GeneratorIncrementalitySourceFile> sourceFiles,
        IReadOnlyCollection<string> generatedHintNames,
        params ExpectedIncrementalStage[] stages)
    {
        return new GeneratorIncrementalityStep(
            name,
            sourceFiles.ToImmutableArray(),
            [],
            ImmutableDictionary<string, string>.Empty,
            NullableContextOptions.Enable,
            [],
            null,
            generatedHintNames.ToImmutableArray(),
            [],
            stages.ToImmutableArray(),
            RecreateSyntaxTrees: false);
    }

    public static GeneratorIncrementalityStep StepWithRecreatedSyntaxTrees(
        string name,
        IReadOnlyCollection<GeneratorIncrementalitySourceFile> sourceFiles,
        IReadOnlyCollection<string> generatedHintNames,
        params ExpectedIncrementalStage[] stages)
    {
        return new GeneratorIncrementalityStep(
            name,
            sourceFiles.ToImmutableArray(),
            [],
            ImmutableDictionary<string, string>.Empty,
            NullableContextOptions.Enable,
            [],
            null,
            generatedHintNames.ToImmutableArray(),
            [],
            stages.ToImmutableArray(),
            RecreateSyntaxTrees: true);
    }

    public static GeneratorIncrementalityStep
        StepWithRecreatedSyntaxTreesAndDiagnostics(
        string name,
        IReadOnlyCollection<GeneratorIncrementalitySourceFile> sourceFiles,
        IReadOnlyCollection<string> generatedHintNames,
        IReadOnlyCollection<ExpectedCompilerDiagnostic> expectedDiagnostics,
        params ExpectedIncrementalStage[] stages)
    {
        return new GeneratorIncrementalityStep(
            name,
            sourceFiles.ToImmutableArray(),
            [],
            ImmutableDictionary<string, string>.Empty,
            NullableContextOptions.Enable,
            [],
            null,
            generatedHintNames.ToImmutableArray(),
            expectedDiagnostics.ToImmutableArray(),
            stages.ToImmutableArray(),
            RecreateSyntaxTrees: true);
    }

    public static GeneratorIncrementalityStep ExecutableStep(
        string name,
        IReadOnlyCollection<GeneratorIncrementalitySourceFile> sourceFiles,
        IReadOnlyCollection<string> generatedHintNames,
        string scenarioTypeName,
        params ExpectedIncrementalStage[] stages)
    {
        return new GeneratorIncrementalityStep(
            name,
            sourceFiles.ToImmutableArray(),
            [],
            ImmutableDictionary<string, string>.Empty,
            NullableContextOptions.Enable,
            [],
            scenarioTypeName,
            generatedHintNames.ToImmutableArray(),
            [],
            stages.ToImmutableArray(),
            RecreateSyntaxTrees: false);
    }

    public static GeneratorIncrementalityStep StepWithReferences(
        string name,
        IReadOnlyCollection<GeneratorIncrementalitySourceFile> sourceFiles,
        IReadOnlyCollection<MetadataReference> additionalReferences,
        IReadOnlyCollection<string> generatedHintNames,
        params ExpectedIncrementalStage[] stages)
    {
        return new GeneratorIncrementalityStep(
            name,
            sourceFiles.ToImmutableArray(),
            additionalReferences.ToImmutableArray(),
            ImmutableDictionary<string, string>.Empty,
            NullableContextOptions.Enable,
            [],
            null,
            generatedHintNames.ToImmutableArray(),
            [],
            stages.ToImmutableArray(),
            RecreateSyntaxTrees: false);
    }

    public static GeneratorIncrementalityStep StepWithReferencesAndDiagnostics(
        string name,
        IReadOnlyCollection<GeneratorIncrementalitySourceFile> sourceFiles,
        IReadOnlyCollection<MetadataReference> additionalReferences,
        IReadOnlyCollection<string> generatedHintNames,
        IReadOnlyCollection<ExpectedCompilerDiagnostic> expectedDiagnostics,
        params ExpectedIncrementalStage[] stages)
    {
        return new GeneratorIncrementalityStep(
            name,
            sourceFiles.ToImmutableArray(),
            additionalReferences.ToImmutableArray(),
            ImmutableDictionary<string, string>.Empty,
            NullableContextOptions.Enable,
            [],
            null,
            generatedHintNames.ToImmutableArray(),
            expectedDiagnostics.ToImmutableArray(),
            stages.ToImmutableArray(),
            RecreateSyntaxTrees: false);
    }

    public static GeneratorIncrementalityStep StepWithOptions(
        string name,
        IReadOnlyCollection<GeneratorIncrementalitySourceFile> sourceFiles,
        IReadOnlyDictionary<string, string> globalOptions,
        IReadOnlyCollection<string> generatedHintNames,
        params ExpectedIncrementalStage[] stages)
    {
        return new GeneratorIncrementalityStep(
            name,
            sourceFiles.ToImmutableArray(),
            [],
            globalOptions.ToImmutableDictionary(
                StringComparer.OrdinalIgnoreCase),
            NullableContextOptions.Enable,
            [],
            null,
            generatedHintNames.ToImmutableArray(),
            [],
            stages.ToImmutableArray(),
            RecreateSyntaxTrees: false);
    }

    public static GeneratorIncrementalityStep StepWithOptionsAndDiagnostics(
        string name,
        IReadOnlyCollection<GeneratorIncrementalitySourceFile> sourceFiles,
        IReadOnlyDictionary<string, string> globalOptions,
        IReadOnlyCollection<string> generatedHintNames,
        IReadOnlyCollection<ExpectedCompilerDiagnostic> expectedDiagnostics,
        params ExpectedIncrementalStage[] stages)
    {
        return new GeneratorIncrementalityStep(
            name,
            sourceFiles.ToImmutableArray(),
            [],
            globalOptions.ToImmutableDictionary(
                StringComparer.OrdinalIgnoreCase),
            NullableContextOptions.Enable,
            [],
            null,
            generatedHintNames.ToImmutableArray(),
            expectedDiagnostics.ToImmutableArray(),
            stages.ToImmutableArray(),
            RecreateSyntaxTrees: false);
    }

    public static GeneratorIncrementalityStep StepWithCompilerInputs(
        string name,
        IReadOnlyCollection<GeneratorIncrementalitySourceFile> sourceFiles,
        IReadOnlyCollection<string> generatedHintNames,
        NullableContextOptions nullableContextOptions,
        IReadOnlyCollection<string> preprocessorSymbols,
        params ExpectedIncrementalStage[] stages)
    {
        return new GeneratorIncrementalityStep(
            name,
            sourceFiles.ToImmutableArray(),
            [],
            ImmutableDictionary<string, string>.Empty,
            nullableContextOptions,
            preprocessorSymbols.ToImmutableArray(),
            null,
            generatedHintNames.ToImmutableArray(),
            [],
            stages.ToImmutableArray(),
            RecreateSyntaxTrees: false);
    }

    public static GeneratorIncrementalityStep StepWithDiagnostics(
        string name,
        IReadOnlyCollection<GeneratorIncrementalitySourceFile> sourceFiles,
        IReadOnlyCollection<string> generatedHintNames,
        IReadOnlyCollection<ExpectedCompilerDiagnostic> expectedDiagnostics,
        params ExpectedIncrementalStage[] stages)
    {
        return new GeneratorIncrementalityStep(
            name,
            sourceFiles.ToImmutableArray(),
            [],
            ImmutableDictionary<string, string>.Empty,
            NullableContextOptions.Enable,
            [],
            null,
            generatedHintNames.ToImmutableArray(),
            expectedDiagnostics.ToImmutableArray(),
            stages.ToImmutableArray(),
            RecreateSyntaxTrees: false);
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

    public static MetadataReference CreateCompilationReference(
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
            DefaultReferences,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        AssertNoWarningsOrErrors(
            $"Compilation reference '{assemblyName}'",
            compilation.GetDiagnostics());

        return compilation.ToMetadataReference();
    }

    public static void RunAndAssert(
        LanguageVersion languageVersion,
        Func<IIncrementalGenerator> generatorFactory,
        params GeneratorIncrementalityStep[] steps)
    {
        Assert.That(steps, Is.Not.Empty);

        var parseOptions = CreateParseOptions(languageVersion, []);
        var globalOptions = ImmutableDictionary<string, string>.Empty;
        var compilation = CSharpCompilation.Create(
            "MorphantIncrementality",
            references: DefaultReferences,
            options: CreateCompilationOptions(
                NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generatorFactory().AsSourceGenerator()],
            optionsProvider: new TestAnalyzerConfigOptionsProvider(
                globalOptions),
            parseOptions: parseOptions,
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));

        foreach (var step in steps)
        {
            var nextParseOptions = CreateParseOptions(
                languageVersion,
                step.PreprocessorSymbols);

            if (!parseOptions.Equals(nextParseOptions))
            {
                parseOptions = nextParseOptions;
                driver = driver.WithUpdatedParseOptions(parseOptions);
            }

            if (!GlobalOptionsEqual(globalOptions, step.GlobalOptions))
            {
                globalOptions = step.GlobalOptions;
                driver = driver.WithUpdatedAnalyzerConfigOptions(
                    new TestAnalyzerConfigOptionsProvider(globalOptions));
            }

            var compilationOptions = CreateCompilationOptions(
                step.NullableContextOptions);

            if (!compilation.Options.Equals(compilationOptions))
            {
                compilation = compilation.WithOptions(compilationOptions);
            }

            compilation = ApplyStep(
                compilation,
                step,
                parseOptions);
            driver = driver.RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out var generatorDiagnostics);

            var compilerDiagnostics = SnapshotCompilerDiagnostics(
                generatorDiagnostics.Concat(
                    outputCompilation.GetDiagnostics()));
            Assert.That(
                compilerDiagnostics,
                Is.EqualTo(step.ExpectedDiagnostics.ToArray()),
                $"Step '{step.Name}' produced unexpected compiler " +
                "warnings or errors." + Environment.NewLine +
                string.Join(Environment.NewLine, compilerDiagnostics));
            var runResult = driver.GetRunResult();

            AssertIncrementality(step, runResult);
            AssertMatchesFreshRun(
                step,
                compilation,
                parseOptions,
                generatorFactory,
                runResult,
                compilerDiagnostics);

            if (step.ScenarioTypeName is { } scenarioTypeName)
            {
                GeneratedCodeExecution.AssertScenario(
                    step.Name,
                    outputCompilation,
                    scenarioTypeName);
            }
        }
    }

    private static CSharpCompilation ApplyStep(
        CSharpCompilation compilation,
        GeneratorIncrementalityStep step,
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

            if (!step.RecreateSyntaxTrees &&
                previousTree is not null &&
                previousTree.GetText().ToString() == sourceFile.Source &&
                previousTree.Options.Equals(parseOptions))
            {
                continue;
            }

            var sourceTree = CSharpSyntaxTree.ParseText(
                SourceText.From(sourceFile.Source, Encoding.UTF8),
                parseOptions,
                sourceFile.Path);

            compilation = previousTree is null
                ? compilation.AddSyntaxTrees(sourceTree)
                : compilation.ReplaceSyntaxTree(previousTree, sourceTree);
        }

        var references = DefaultReferences.AddRange(
            step.AdditionalReferences);

        return ReferencesEqual(compilation.References, references)
            ? compilation
            : compilation.WithReferences(references);
    }

    private static bool GlobalOptionsEqual(
        ImmutableDictionary<string, string> left,
        ImmutableDictionary<string, string> right)
    {
        return left.Count == right.Count &&
               left.All(pair =>
                   right.TryGetValue(pair.Key, out var value) &&
                   StringComparer.Ordinal.Equals(pair.Value, value));
    }

    private static bool ReferencesEqual(
        IEnumerable<MetadataReference> left,
        IEnumerable<MetadataReference> right)
    {
        return left.SequenceEqual(
            right,
            MetadataReferenceIdentityComparer.Instance);
    }

    private static void AssertMatchesFreshRun(
        GeneratorIncrementalityStep step,
        CSharpCompilation compilation,
        CSharpParseOptions parseOptions,
        Func<IIncrementalGenerator> generatorFactory,
        GeneratorDriverRunResult warmRunResult,
        ExpectedCompilerDiagnostic[] warmCompilerDiagnostics)
    {
        GeneratorDriver freshDriver = CSharpGeneratorDriver.Create(
            [generatorFactory().AsSourceGenerator()],
            parseOptions: parseOptions);
        freshDriver = freshDriver.WithUpdatedAnalyzerConfigOptions(
            new TestAnalyzerConfigOptionsProvider(step.GlobalOptions));
        freshDriver = freshDriver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var freshOutputCompilation,
            out var freshGeneratorDiagnostics);

        var warmResult = warmRunResult.Results.Single();
        var freshResult = freshDriver.GetRunResult().Results.Single();

        Assert.That(
            freshResult.Exception,
            Is.Null,
            $"Fresh run for step '{step.Name}' must not throw from the " +
            "generator.");
        Assert.That(
            SnapshotGeneratedSources(warmResult),
            Is.EqualTo(SnapshotGeneratedSources(freshResult)),
            $"Step '{step.Name}' produced a different result with a warm " +
            "driver than with a fresh driver.");
        Assert.That(
            SnapshotDiagnostics(warmResult),
            Is.EqualTo(SnapshotDiagnostics(freshResult)),
            $"Step '{step.Name}' produced different diagnostics with a " +
            "warm driver than with a fresh driver.");
        Assert.That(
            warmCompilerDiagnostics,
            Is.EqualTo(SnapshotCompilerDiagnostics(
                freshGeneratorDiagnostics.Concat(
                    freshOutputCompilation.GetDiagnostics()))),
            $"Step '{step.Name}' produced different compiler diagnostics " +
            "with a warm driver than with a fresh driver.");
    }

    private static IncrementalGeneratedSource[] SnapshotGeneratedSources(
        GeneratorRunResult result)
    {
        return result.GeneratedSources
            .Select(static source =>
                new IncrementalGeneratedSource(
                    source.HintName,
                    source.SourceText.ToString()))
            .OrderBy(static source => source.HintName, StringComparer.Ordinal)
            .ToArray();
    }

    private static IncrementalDiagnostic[] SnapshotDiagnostics(
        GeneratorRunResult result)
    {
        return result.Diagnostics
            .Select(static diagnostic =>
                new IncrementalDiagnostic(
                    diagnostic.Id,
                    diagnostic.Severity,
                    diagnostic.WarningLevel,
                    diagnostic.GetMessage(),
                    diagnostic.Location.SourceTree?.FilePath,
                    diagnostic.Location.IsInSource
                        ? diagnostic.Location.SourceSpan.Start
                        : -1,
                    diagnostic.Location.IsInSource
                        ? diagnostic.Location.SourceSpan.Length
                        : 0,
                    diagnostic.IsSuppressed,
                    SnapshotLocations(diagnostic.AdditionalLocations),
                    SnapshotProperties(diagnostic.Properties)))
            .ToArray();
    }

    private static string SnapshotProperties(
        IReadOnlyDictionary<string, string?> properties)
    {
        return string.Join(
            "\u001f",
            properties
                .OrderBy(static property => property.Key, StringComparer.Ordinal)
                .Select(static property =>
                    $"{property.Key}\u001e{property.Value}"));
    }

    private static ExpectedCompilerDiagnostic[] SnapshotCompilerDiagnostics(
        IEnumerable<Diagnostic> diagnostics)
    {
        return diagnostics
            .Where(static diagnostic =>
                diagnostic.Severity is
                    DiagnosticSeverity.Warning or
                    DiagnosticSeverity.Error)
            .Select(static diagnostic =>
                new ExpectedCompilerDiagnostic(
                    diagnostic.Id,
                    diagnostic.Severity,
                    diagnostic.Location.SourceTree?.FilePath,
                    diagnostic.Location.IsInSource
                        ? diagnostic.Location.SourceSpan.Start
                        : -1,
                    diagnostic.Location.IsInSource
                        ? diagnostic.Location.SourceSpan.Length
                        : 0,
                    SnapshotLocations(diagnostic.AdditionalLocations)))
            .Distinct()
            .OrderBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ThenBy(
                static diagnostic => diagnostic.Path,
                StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Start)
            .ThenBy(static diagnostic => diagnostic.Length)
            .ToArray();
    }

    private static string SnapshotLocations(
        IEnumerable<Location> locations)
    {
        return SnapshotLocations(locations.Select(static location =>
            new ExpectedCompilerDiagnosticLocation(
                location.SourceTree?.FilePath,
                location.IsInSource ? location.SourceSpan.Start : -1,
                location.IsInSource ? location.SourceSpan.Length : 0)));
    }

    private static string SnapshotLocations(
        IEnumerable<ExpectedCompilerDiagnosticLocation> locations)
    {
        return string.Join(
            "\u001f",
            locations.Select(static location =>
                $"{location.Path}\u001e{location.Start}\u001e{location.Length}"));
    }

    private static CSharpParseOptions CreateParseOptions(
        LanguageVersion languageVersion,
        IEnumerable<string> preprocessorSymbols)
    {
        return new CSharpParseOptions(
            languageVersion,
            DocumentationMode.Diagnose,
            preprocessorSymbols: preprocessorSymbols);
    }

    private static CSharpCompilationOptions CreateCompilationOptions(
        NullableContextOptions nullableContextOptions)
    {
        return new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: nullableContextOptions);
    }

    private static void AssertIncrementality(
        GeneratorIncrementalityStep step,
        GeneratorDriverRunResult runResult)
    {
        var generatorResult = runResult.Results.Single();

        Assert.That(
            generatorResult.Exception,
            Is.Null,
            $"Step '{step.Name}' must not throw from the generator.");

        foreach (var stage in step.ExpectedStages)
        {
            AssertTrackedOutputs(step.Name, stage, generatorResult);
        }

        var actualHintNames = generatorResult.GeneratedSources
            .Select(static source => source.HintName)
            .OrderBy(static hintName => hintName, StringComparer.Ordinal)
            .ToArray();
        var expectedHintNames = step.GeneratedHintNames
            .OrderBy(static hintName => hintName, StringComparer.Ordinal)
            .ToArray();

        Assert.That(
            actualHintNames,
            Is.EqualTo(expectedHintNames),
            $"Step '{step.Name}' generated an unexpected file set.");
    }

    private static void AssertTrackedOutputs(
        string stepName,
        ExpectedIncrementalStage expectedStage,
        GeneratorRunResult generatorResult)
    {
        Assert.That(
            generatorResult.TrackedSteps.TryGetValue(
                expectedStage.Name,
                out var trackedSteps),
            Is.True,
            $"Step '{stepName}' did not track stage " +
            $"'{expectedStage.Name}'.");

        if (!expectedStage.ReasonCounts.IsEmpty)
        {
            AssertTrackedReasons(
                stepName,
                expectedStage,
                trackedSteps);
            return;
        }

        var actualOutputs = trackedSteps
            .SelectMany(static trackedStep => trackedStep.Outputs)
            .Select(output =>
                new TrackedIncrementalOutput(
                    GetHintName(
                        output.Value,
                        stepName,
                        expectedStage.Name),
                    output.Reason))
            .OrderBy(static output => output.HintName, StringComparer.Ordinal)
            .ThenBy(static output => output.Reason)
            .ToArray();
        var expectedOutputs = expectedStage.Outputs
            .Select(static output =>
                new TrackedIncrementalOutput(
                    output.HintName,
                    output.Reason))
            .OrderBy(static output => output.HintName, StringComparer.Ordinal)
            .ThenBy(static output => output.Reason)
            .ToArray();

        Assert.That(
            actualOutputs,
            Is.EqualTo(expectedOutputs),
            $"Step '{stepName}', stage '{expectedStage.Name}'.");
    }

    private static void AssertTrackedReasons(
        string stepName,
        ExpectedIncrementalStage expectedStage,
        ImmutableArray<IncrementalGeneratorRunStep> trackedSteps)
    {
        var actual = trackedSteps
            .SelectMany(static trackedStep => trackedStep.Outputs)
            .GroupBy(static output => output.Reason)
            .Select(static group =>
                new ExpectedIncrementalReasonCount(
                    group.Key,
                    group.Count()))
            .OrderBy(static count => count.Reason)
            .ToArray();
        var expected = expectedStage.ReasonCounts
            .OrderBy(static count => count.Reason)
            .ToArray();

        Assert.That(
            actual,
            Is.EqualTo(expected),
            $"Step '{stepName}', stage '{expectedStage.Name}'.");
    }

    private static string GetHintName(
        object value,
        string stepName,
        string stageName)
    {
        var property = value.GetType().GetProperty("HintName");

        Assert.That(
            property,
            Is.Not.Null,
            $"Step '{stepName}', stage '{stageName}' produced an " +
            "output without a HintName property.");

        var hintName = property!.GetValue(value);

        Assert.That(
            hintName,
            Is.TypeOf<string>(),
            $"Step '{stepName}', stage '{stageName}' produced an " +
            "output without a string HintName value.");

        return (string)hintName!;
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

internal sealed record GeneratorIncrementalityStep(
    string Name,
    ImmutableArray<GeneratorIncrementalitySourceFile> SourceFiles,
    ImmutableArray<MetadataReference> AdditionalReferences,
    ImmutableDictionary<string, string> GlobalOptions,
    NullableContextOptions NullableContextOptions,
    ImmutableArray<string> PreprocessorSymbols,
    string? ScenarioTypeName,
    ImmutableArray<string> GeneratedHintNames,
    ImmutableArray<ExpectedCompilerDiagnostic> ExpectedDiagnostics,
    ImmutableArray<ExpectedIncrementalStage> ExpectedStages,
    bool RecreateSyntaxTrees);

internal sealed record GeneratorIncrementalitySourceFile(
    string Path,
    string Source);

internal sealed record ExpectedIncrementalStage(
    string Name,
    ImmutableArray<ExpectedIncrementalOutput> Outputs,
    ImmutableArray<ExpectedIncrementalReasonCount> ReasonCounts);

internal sealed record ExpectedIncrementalReasonCount(
    IncrementalStepRunReason Reason,
    int Count);

internal sealed record ExpectedIncrementalOutput(
    string HintName,
    IncrementalStepRunReason Reason);

internal sealed record TrackedIncrementalOutput(
    string HintName,
    IncrementalStepRunReason Reason);

internal sealed record IncrementalGeneratedSource(
    string HintName,
    string Source);

internal sealed record IncrementalDiagnostic(
    string Id,
    DiagnosticSeverity Severity,
    int WarningLevel,
    string Message,
    string? Path,
    int Start,
    int Length,
    bool IsSuppressed,
    string AdditionalLocations,
    string Properties);

internal sealed record ExpectedCompilerDiagnostic(
    string Id,
    DiagnosticSeverity Severity,
    string? Path,
    int Start,
    int Length,
    string AdditionalLocations);

internal sealed record ExpectedCompilerDiagnosticLocation(
    string? Path,
    int Start,
    int Length);

internal sealed class MetadataReferenceIdentityComparer :
    IEqualityComparer<MetadataReference>
{
    public static MetadataReferenceIdentityComparer Instance { get; } =
        new();

    private MetadataReferenceIdentityComparer()
    {
    }

    public bool Equals(MetadataReference? left, MetadataReference? right)
    {
        return ReferenceEquals(left, right);
    }

    public int GetHashCode(MetadataReference reference)
    {
        return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(
            reference);
    }
}
