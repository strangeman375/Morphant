namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class MemberDiagnosticsTests
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
    public void Preserves_suppressed_member_diagnostic_recovery()
    {
        global::Morphant.Generator.IntegrationTests.CSharp11.Scenarios
            .MemberDiagnosticsRecovery_10c0f0040.Scenario.Verify();
    }

    [Test]
    public async Task Editorconfig_overrides_all_member_diagnostics()
    {
        var build = await _workspace.BuildConsumer("MemberOverrides");
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
                    "MORPH0040",
                    "MORPH0041",
                    "MORPH0042",
                    "MORPH0043"
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
