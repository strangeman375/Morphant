namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class SettingsDiagnosticsTests
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
        var build = await _workspace.BuildConsumer("SettingsOverrides");
        var diagnostics = GetCompilerDiagnostics(build.Process.Output);

        Assert.Multiple(() =>
        {
            Assert.That(
                build.Process.ExitCode,
                Is.EqualTo(0),
                build.Process.Output);
            Assert.That(
                diagnostics.Select(GetDiagnosticId),
                Is.EqualTo(new[]
                {
                    "MORPH0021",
                    "MORPH0022",
                    "MORPH0023"
                }));
            Assert.That(diagnostics, Has.All.Contains("warning MORPH"));
            Assert.That(
                Directory.GetFiles(
                    build.GeneratedDirectory,
                    "*.cs",
                    SearchOption.AllDirectories),
                Is.Not.Empty);
        });
    }

    [Test]
    public async Task Reports_every_invalid_MSBuild_setting_independently()
    {
        var build = await _workspace.BuildConsumer(
            "SettingsMsBuildMatrix");
        var diagnostics = GetCompilerDiagnostics(build.Process.Output);

        Assert.Multiple(() =>
        {
            Assert.That(
                build.Process.ExitCode,
                Is.EqualTo(1),
                build.Process.Output);
            Assert.That(diagnostics, Has.Length.EqualTo(6));
            Assert.That(
                diagnostics.Select(GetDiagnosticId),
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
            Assert.That(
                diagnostics,
                Has.Some.Contains("UnmappedMemberValidation"));
            Assert.That(
                Directory.GetFiles(
                    build.GeneratedDirectory,
                    "*.cs",
                    SearchOption.AllDirectories),
                Is.Not.Empty);
        });
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
