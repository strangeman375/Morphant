namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class MappingCompletenessDiagnosticsTests
{
    [Test]
    public void Suppressed_warnings_do_not_change_create_or_update_behavior()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .MappingCompleteness_a11ce011.Scenario.Verify();
    }

    [Test]
    public async Task MSBuild_setting_and_editorconfig_configure_both_diagnostics()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var build = await workspace.BuildConsumer(
            "MappingCompletenessOverrides");
        var diagnostics = CompilerDiagnosticOutput.Read(build.Process.Output);

        Assert.Multiple(() =>
        {
            Assert.That(
                build.Process.ExitCode,
                Is.EqualTo(1),
                build.Process.Output);
            Assert.That(
                diagnostics.Select(CompilerDiagnosticOutput.GetId),
                Is.EqualTo(new[] { "MORPH0047", "MORPH0048" }));
            Assert.That(diagnostics[0], Does.Contain("warning MORPH0047"));
            Assert.That(diagnostics[1], Does.Contain("error MORPH0048"));
            Assert.That(build.GetGeneratedFiles(), Is.Not.Empty);
        });
    }

    [Test]
    public async Task Resolves_every_validation_value_and_configuration_level()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var build = await workspace.BuildConsumer(
            "MappingCompletenessMatrix");
        var diagnostics = CompilerDiagnosticOutput.Read(build.Process.Output);
        var sourceDiagnostics = diagnostics
            .Where(static diagnostic =>
                CompilerDiagnosticOutput.GetId(diagnostic) == "MORPH0047")
            .ToArray();
        var destinationDiagnostics = diagnostics
            .Where(static diagnostic =>
                CompilerDiagnosticOutput.GetId(diagnostic) == "MORPH0048")
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                build.Process.ExitCode,
                Is.EqualTo(0),
                build.Process.Output);
            Assert.That(sourceDiagnostics, Has.Length.EqualTo(3));
            Assert.That(destinationDiagnostics, Has.Length.EqualTo(4));
            Assert.That(
                sourceDiagnostics,
                Has.Some.Contains("AssemblyUnused"));
            Assert.That(
                sourceDiagnostics,
                Has.Some.Contains("RootUnused"));
            Assert.That(
                sourceDiagnostics,
                Has.Some.Contains("DefaultUnused"));
            Assert.That(
                destinationDiagnostics,
                Has.Some.Contains("AssemblyUnmapped"));
            Assert.That(
                destinationDiagnostics,
                Has.Some.Contains("PairUnmapped"));
            Assert.That(
                destinationDiagnostics,
                Has.Some.Contains("IncludedUnmapped"));
            Assert.That(
                destinationDiagnostics,
                Has.Some.Contains("BaseUnmapped"));
            Assert.That(diagnostics, Has.All.Contains("warning MORPH"));
            Assert.That(build.GetGeneratedFiles(), Is.Not.Empty);
        });
    }

    [Test]
    public async Task Library_default_disables_completeness_validation()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var build = await workspace.BuildConsumer(
            "MappingCompletenessDefault");
        var diagnostics = CompilerDiagnosticOutput.Read(build.Process.Output);

        Assert.Multiple(() =>
        {
            Assert.That(
                build.Process.ExitCode,
                Is.EqualTo(0),
                build.Process.Output);
            Assert.That(
                diagnostics.Where(static diagnostic =>
                    CompilerDiagnosticOutput.GetId(diagnostic) is
                        "MORPH0047" or "MORPH0048"),
                Is.Empty);
            Assert.That(build.GetGeneratedFiles(), Is.Not.Empty);
        });
    }
}
