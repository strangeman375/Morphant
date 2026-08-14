namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class NestedMappingDiagnosticsTests
{
    [Test]
    public void Preserves_suppressed_nested_mapping_diagnostic_recovery()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .NestedMappingDiagnosticsRecovery_11c0f0044.Scenario.Verify();
    }

    [Test]
    public async Task Editorconfig_overrides_all_nested_mapping_diagnostics()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var build = await workspace.BuildConsumer("NestedMappingOverrides");
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
                    "MORPH0044",
                    "MORPH0045",
                    "MORPH0046"
                }));
            Assert.That(diagnostics, Has.All.Contains("warning MORPH"));
            Assert.That(build.GetGeneratedFiles(), Is.Not.Empty);
        });
    }
}
