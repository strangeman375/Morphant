namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class DeclarationDiagnosticsTests
{
    [Test]
    public async Task Missing_TypeMapper_base_produces_no_generated_artifacts()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var build = await workspace.BuildConsumer(
            "DeclarationMissingBase");

        AssertFailedWithOnly(build.Process, "MORPH0005");
        Assert.That(build.GetGeneratedFiles(), Is.Empty);
    }

    [Test]
    public async Task Legal_mapper_forms_compile_as_generated_contracts()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var build = await workspace.BuildConsumer("DeclarationValidForms");
        var mapperFiles = build.GetGeneratedFiles()
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
            Assert.That(
                CompilerDiagnosticOutput.Read(build.Process.Output),
                Is.Empty);
            Assert.That(mapperFiles, Has.Length.EqualTo(7));
        });
    }

    [Test]
    public async Task Structural_and_unifiable_failures_keep_DSL_surfaces_without_cascades()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var build = await workspace.BuildConsumer(
            "DeclarationStructuralFailures");
        var diagnostics = CompilerDiagnosticOutput.Read(build.Process.Output);
        var generatedFiles = build.GetGeneratedFiles();

        Assert.Multiple(() =>
        {
            Assert.That(build.Process.ExitCode, Is.Not.EqualTo(0));
            Assert.That(diagnostics, Has.Length.EqualTo(4), build.Process.Output);
            Assert.That(
                diagnostics.Select(CompilerDiagnosticOutput.GetId),
                Is.EquivalentTo(new[]
                {
                    "MORPH0006",
                    "MORPH0007",
                    "MORPH0008",
                    "MORPH0010"
                }));
            Assert.That(generatedFiles, Is.Not.Empty);
            Assert.That(
                generatedFiles.Any(file =>
                    Path.GetFileName(file).Contains(
                        ".TypeMapper.",
                        StringComparison.Ordinal)),
                Is.False);
        });
    }

    [Test]
    public async Task Exact_contract_removes_only_the_conflicting_pair()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var build = await workspace.BuildConsumer(
            "DeclarationPairRecovery");

        AssertFailedWithOnly(build.Process, "MORPH0009");

        var generatedFiles = build.GetGeneratedFiles();
        var mapperFile = generatedFiles
            .Single(file => Path.GetFileName(file).Contains(
                ".TypeMapper.",
                StringComparison.Ordinal));
        var mapperSource = await File.ReadAllTextAsync(mapperFile);
        var allGeneratedSources = string.Join(
            Environment.NewLine,
            await Task.WhenAll(
                generatedFiles.Select(static file =>
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
        using var workspace = new ConsumerBuildWorkspace();
        var build = await workspace.BuildConsumer(
            "DeclarationSuppressedSupports");
        var generatedFiles = build.GetGeneratedFiles();

        Assert.Multiple(() =>
        {
            Assert.That(build.Process.ExitCode, Is.EqualTo(0), build.Process.Output);
            Assert.That(
                CompilerDiagnosticOutput.Read(build.Process.Output),
                Is.Empty);
            Assert.That(generatedFiles, Is.Not.Empty);
            Assert.That(
                generatedFiles.Any(file =>
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
        var diagnostics = CompilerDiagnosticOutput.Read(result.Output);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.Not.EqualTo(0), result.Output);
            Assert.That(diagnostics, Has.Length.EqualTo(1), result.Output);
            Assert.That(
                diagnostics.Single(),
                Does.Contain("error " + diagnosticId));
        });
    }

}
