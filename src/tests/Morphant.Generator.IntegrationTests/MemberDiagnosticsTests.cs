namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class MemberDiagnosticsTests
{
    [Test]
    public void Preserves_suppressed_member_diagnostic_recovery()
    {
        global::Morphant.Generator.IntegrationTests.CSharp11.Scenarios
            .MemberDiagnosticsRecovery_10c0f0040.Scenario.Verify();
    }

    [Test]
    public async Task Editorconfig_overrides_all_member_diagnostics()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var build = await workspace.BuildConsumer("MemberOverrides");
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
                    "MORPH0040",
                    "MORPH0041",
                    "MORPH0042",
                    "MORPH0043"
                }));
            Assert.That(diagnostics, Has.All.Contains("warning MORPH"));
            Assert.That(build.GetGeneratedFiles(), Is.Not.Empty);
        });
    }
}
