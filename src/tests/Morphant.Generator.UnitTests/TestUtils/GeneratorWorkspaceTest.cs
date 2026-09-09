using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Host.Mef;

namespace Morphant.Generator.UnitTests.TestUtils;

internal sealed class GeneratorWorkspaceTest : IDisposable
{
    private static readonly ImmutableArray<MetadataReference> References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Append(typeof(TypeMapper<>).Assembly.Location)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(static path =>
            (MetadataReference)MetadataReference.CreateFromFile(path))
        .ToImmutableArray();

    private readonly AdhocWorkspace _workspace = new(
        MefHostServices.Create(MefHostServices.DefaultAssemblies.Add(
            typeof(CSharpFormattingOptions).Assembly)));

    public Solution Solution => _workspace.CurrentSolution;

    public ProjectId AddProject(string name)
    {
        return _workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(name),
            VersionStamp.Create(),
            name,
            name,
            LanguageNames.CSharp,
            compilationOptions: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable),
            parseOptions: new CSharpParseOptions(
                LanguageVersion.CSharp9,
                DocumentationMode.Diagnose),
            metadataReferences: References,
            analyzerReferences: [new MorphantAnalyzerReference()])).Id;
    }

    public void Apply(Solution solution)
    {
        Assert.That(_workspace.TryApplyChanges(solution), Is.True);
    }

    public async Task<IReadOnlyDictionary<string, string>> AssertProjectAsync(
        ProjectId projectId,
        IReadOnlyCollection<string> expectedHints,
        IReadOnlyCollection<ExpectedCompilerDiagnostic>? expectedDiagnostics = null,
        string? scenarioTypeName = null)
    {
        var project = Solution.GetProject(projectId)!;
        var documents = (await project.GetSourceGeneratedDocumentsAsync())
            .OrderBy(static document => document.Name, StringComparer.Ordinal)
            .ToArray();
        var compilation = (await project.GetCompilationAsync())!;
        var generatedTrees = new List<SyntaxTree>();
        var actualSources = new SortedDictionary<string, string>(
            StringComparer.Ordinal);

        foreach (var document in documents)
        {
            var tree = (await document.GetSyntaxTreeAsync())!;
            Assert.That(compilation.ContainsSyntaxTree(tree), Is.True);
            generatedTrees.Add(tree);
            actualSources.Add(document.Name, (await document.GetTextAsync()).ToString());
        }

        Assert.That(actualSources.Keys, Is.EqualTo(
            expectedHints.OrderBy(static hint => hint, StringComparer.Ordinal)));

        // The Workspace owns the incremental driver. A separate fresh driver
        // checks its complete output, using the current source project references.
        var input = compilation.RemoveSyntaxTrees(generatedTrees);
        GeneratorDriver fresh = CSharpGeneratorDriver.Create(
            [new MorphantGenerator().AsSourceGenerator()],
            parseOptions: (CSharpParseOptions)project.ParseOptions!);
        fresh = fresh.RunGeneratorsAndUpdateCompilation(
            input, out var freshCompilation, out var generatorDiagnostics);
        var result = fresh.GetRunResult().Results.Single();
        Assert.That(result.Exception, Is.Null);
        Assert.That(actualSources.ToArray(), Is.EqualTo(
            result.GeneratedSources
                .OrderBy(static source => source.HintName, StringComparer.Ordinal)
                .Select(static source => new KeyValuePair<string, string>(
                    source.HintName, source.SourceText.ToString())).ToArray()));
        Assert.That(SnapshotDiagnostics(compilation.GetDiagnostics()), Is.EqualTo(
            SnapshotDiagnostics(freshCompilation.GetDiagnostics())));
        Assert.That(SnapshotDiagnostics(generatorDiagnostics.Concat(
                compilation.GetDiagnostics())),
            Is.EqualTo((expectedDiagnostics ?? []).ToArray()));

        if (scenarioTypeName is not null)
        {
            GeneratedCodeExecution.AssertScenario(
                project.Name, compilation, scenarioTypeName);
        }

        return actualSources;
    }

    public void Dispose() => _workspace.Dispose();

    private static ExpectedCompilerDiagnostic[] SnapshotDiagnostics(
        IEnumerable<Diagnostic> diagnostics)
    {
        return diagnostics
            .Where(static diagnostic => diagnostic.Severity is
                DiagnosticSeverity.Warning or DiagnosticSeverity.Error)
            .Select(static diagnostic => new ExpectedCompilerDiagnostic(
                diagnostic.Id,
                diagnostic.Severity,
                diagnostic.Location.SourceTree?.FilePath,
                diagnostic.Location.IsInSource ? diagnostic.Location.SourceSpan.Start : -1,
                diagnostic.Location.IsInSource ? diagnostic.Location.SourceSpan.Length : 0,
                string.Join("\u001f", diagnostic.AdditionalLocations.Select(
                    static location => $"{location.SourceTree?.FilePath}\u001e" +
                        $"{(location.IsInSource ? location.SourceSpan.Start : -1)}\u001e" +
                        $"{(location.IsInSource ? location.SourceSpan.Length : 0)}"))))
            .Distinct()
            .OrderBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Path, StringComparer.Ordinal)
            .ThenBy(static diagnostic => diagnostic.Start)
            .ThenBy(static diagnostic => diagnostic.Length)
            .ToArray();
    }

    private sealed class MorphantAnalyzerReference : AnalyzerReference
    {
        private readonly ImmutableArray<ISourceGenerator> _generators =
            [new MorphantGenerator().AsSourceGenerator()];

        public override string FullPath => typeof(MorphantGenerator).Assembly.Location;
        public override object Id => FullPath;
        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzersForAllLanguages() => [];
        public override ImmutableArray<DiagnosticAnalyzer> GetAnalyzers(string language) => [];
        public override ImmutableArray<ISourceGenerator> GetGeneratorsForAllLanguages() => _generators;
        public override ImmutableArray<ISourceGenerator> GetGenerators(string language) =>
            language == LanguageNames.CSharp ? _generators : [];
    }
}
