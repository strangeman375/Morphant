namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class DeclarationDiagnosticsTests
{
    private CompatibilityTestWorkspace _workspace = null!;

    [SetUp]
    public void SetUp()
    {
        _workspace = new CompatibilityTestWorkspace();
    }

    [TearDown]
    public void TearDown()
    {
        _workspace.Dispose();
    }

    [Test]
    public async Task Missing_TypeMapper_base_produces_no_generated_artifacts()
    {
        var build = await _workspace.BuildConsumer(
            "DeclarationMissingBase");

        AssertFailedWithOnly(build.Process, "MORPH0005");
        Assert.That(GeneratedFiles(build), Is.Empty);
    }

    [Test]
    public async Task Legal_mapper_forms_compile_as_generated_contracts()
    {
        var build = await _workspace.BuildConsumer("DeclarationValidForms");
        var mapperFiles = GeneratedFiles(build)
            .Where(file => Path.GetFileName(file).Contains(
                ".TypeMapper.",
                StringComparison.Ordinal))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                build.Process.ExitCode,
                Is.EqualTo(0),
                build.Process.Output);
            Assert.That(GetCompilerDiagnostics(build.Process.Output), Is.Empty);
            Assert.That(mapperFiles, Has.Length.EqualTo(7));
        });
    }

    [Test]
    public async Task Structural_failures_keep_DSL_surfaces_without_cascades()
    {
        var build = await _workspace.BuildConsumer(
            "DeclarationStructuralFailures");
        var diagnostics = GetCompilerDiagnostics(build.Process.Output);

        Assert.Multiple(() =>
        {
            Assert.That(build.Process.ExitCode, Is.Not.EqualTo(0));
            Assert.That(diagnostics, Has.Length.EqualTo(3), build.Process.Output);
            Assert.That(
                diagnostics.Select(GetDiagnosticId),
                Is.EquivalentTo(new[]
                {
                    "MORPH0006",
                    "MORPH0007",
                    "MORPH0008"
                }));
            Assert.That(GeneratedFiles(build), Is.Not.Empty);
            Assert.That(
                GeneratedFiles(build).Any(file =>
                    Path.GetFileName(file).Contains(
                        ".TypeMapper.",
                        StringComparison.Ordinal)),
                Is.False);
        });
    }

    [Test]
    public async Task Exact_contract_removes_only_the_conflicting_pair()
    {
        var build = await _workspace.BuildConsumer(
            "DeclarationPairRecovery");

        AssertFailedWithOnly(build.Process, "MORPH0009");

        var mapperFile = GeneratedFiles(build)
            .Single(file => Path.GetFileName(file).Contains(
                ".TypeMapper.",
                StringComparison.Ordinal));
        var mapperSource = await File.ReadAllTextAsync(mapperFile);
        var allGeneratedSources = string.Join(
            Environment.NewLine,
            await Task.WhenAll(
                GeneratedFiles(build).Select(static file =>
                    File.ReadAllTextAsync(file))));

        Assert.Multiple(() =>
        {
            Assert.That(
                mapperSource,
                Does.Not.Contain("ConflictDestination>"));
            Assert.That(
                mapperSource,
                Does.Contain("IndependentDestination>"));
            Assert.That(
                allGeneratedSources,
                Does.Contain("ConflictDestination"));
        });
    }

    [Test]
    public async Task Suppression_hides_MORPH0034_but_keeps_the_gate()
    {
        var build = await _workspace.BuildConsumer(
            "DeclarationSuppressedSupports");

        Assert.Multiple(() =>
        {
            Assert.That(build.Process.ExitCode, Is.EqualTo(0), build.Process.Output);
            Assert.That(GetCompilerDiagnostics(build.Process.Output), Is.Empty);
            Assert.That(GeneratedFiles(build), Is.Not.Empty);
            Assert.That(
                GeneratedFiles(build).Any(file =>
                    Path.GetFileName(file).Contains(
                        ".TypeMapper.",
                        StringComparison.Ordinal)),
                Is.False);
        });
    }

    private static void AssertFailedWithOnly(
        ProcessResult result,
        string diagnosticId)
    {
        var diagnostics = GetCompilerDiagnostics(result.Output);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.Not.EqualTo(0), result.Output);
            Assert.That(diagnostics, Has.Length.EqualTo(1), result.Output);
            Assert.That(
                diagnostics.Single(),
                Does.Contain("error " + diagnosticId));
        });
    }

    private static string[] GeneratedFiles(CompatibilityBuild build)
    {
        return Directory.Exists(build.GeneratedDirectory)
            ? Directory.GetFiles(
                build.GeneratedDirectory,
                "Morphant.Generated.*.cs",
                SearchOption.AllDirectories)
            : [];
    }

    private static string[] GetCompilerDiagnostics(string output)
    {
        return output
            .Split('\n')
            .TakeWhile(static line =>
                !line.Contains("Build FAILED.", StringComparison.Ordinal) &&
                !line.Contains("Build succeeded.", StringComparison.Ordinal))
            .Where(static line =>
                line.Contains(": error ", StringComparison.Ordinal) ||
                line.Contains(": warning ", StringComparison.Ordinal))
            .Select(static line => line.Trim())
            .ToArray();
    }

    private static string GetDiagnosticId(string diagnostic)
    {
        var marker = diagnostic.Contains(": error ", StringComparison.Ordinal)
            ? ": error "
            : ": warning ";
        var start = diagnostic.IndexOf(marker, StringComparison.Ordinal) +
                    marker.Length;
        var end = diagnostic.IndexOf(':', start);

        return diagnostic[start..end];
    }
}
