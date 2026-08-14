namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class ConstructionDiagnosticsTests
{
    [Test]
    public void Suppressed_construction_failures_keep_path_sensitive_recovery()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .ConstructionDiagnosticsRecovery_9c0f0035.Scenario.Verify();
    }

    [Test]
    public async Task Editorconfig_overrides_all_construction_diagnostics()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var build = await workspace.BuildConsumer("ConstructionOverrides");
        var diagnostics = CompilerDiagnosticOutput.Read(build.Process.Output);

        Assert.Multiple(() =>
        {
            Assert.That(
                build.Process.ExitCode,
                Is.EqualTo(0),
                build.Process.Output);
            Assert.That(
                diagnostics.Select(CompilerDiagnosticOutput.GetId),
                Is.EqualTo(new[]
                {
                    "MORPH0035",
                    "MORPH0036",
                    "MORPH0037",
                    "MORPH0038",
                    "MORPH0039"
                }));
            Assert.That(diagnostics, Has.All.Contains("warning MORPH"));
            Assert.That(build.GetGeneratedFiles(), Is.Not.Empty);
        });
    }
}
