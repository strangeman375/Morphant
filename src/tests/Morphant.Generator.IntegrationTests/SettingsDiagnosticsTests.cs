namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class SettingsDiagnosticsTests
{
    [Test]
    public void Applies_each_invalid_value_recovery_family()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.SettingsRecovery_6c0f0021.Scenario.Verify();
    }

    [Test]
    public void Rejects_inapplicable_settings_and_preserves_an_independent_pair()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.SettingsInapplicable_6c0f0023.Scenario.Verify();
    }

    [Test]
    public async Task Globalconfig_overrides_all_settings_diagnostics()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var build = await workspace.BuildConsumer("SettingsOverrides");
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
                    "MORPH0021",
                    "MORPH0022",
                    "MORPH0023"
                }));
            Assert.That(diagnostics, Has.All.Contains("warning MORPH"));
            Assert.That(build.GetGeneratedFiles(), Is.Not.Empty);
        });
    }

    [Test]
    public async Task Reports_every_invalid_MSBuild_setting_independently()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var build = await workspace.BuildConsumer(
            "SettingsMsBuildMatrix");
        var diagnostics = CompilerDiagnosticOutput.Read(build.Process.Output);

        Assert.Multiple(() =>
        {
            Assert.That(
                build.Process.ExitCode,
                Is.EqualTo(1),
                build.Process.Output);
            Assert.That(diagnostics, Has.Length.EqualTo(7));
            Assert.That(
                diagnostics.Select(CompilerDiagnosticOutput.GetId),
                Is.All.EqualTo("MORPH0022"));
            Assert.That(diagnostics, Has.Some.Contains("MappingMode"));
            Assert.That(
                diagnostics,
                Has.Some.Contains("NullSourceHandling"));
            Assert.That(
                diagnostics,
                Has.Some.Contains("NullDestinationHandling"));
            Assert.That(
                diagnostics,
                Has.Some.Contains("ConstructorSelection"));
            Assert.That(
                diagnostics,
                Has.Some.Contains("MemberSelection"));
            Assert.That(diagnostics, Has.Some.Contains("Flattening"));
            Assert.That(
                diagnostics,
                Has.Some.Contains("UnmappedMemberValidation"));
            Assert.That(build.GetGeneratedFiles(), Is.Not.Empty);
        });
    }
}
