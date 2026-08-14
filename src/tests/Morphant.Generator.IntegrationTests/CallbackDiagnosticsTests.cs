namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class CallbackDiagnosticsTests
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
    public void Suppressed_structured_method_groups_keep_typed_recovery()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.CallbackDiagnosticsRecovery_8c0f0029.Scenario.Verify();
    }

    [Test]
    public void Suppressed_transfer_failures_keep_atomic_and_independent_paths()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.CallbackDiagnosticsDiscovery_9d7a0201.Scenario.Verify();
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.CallbackDiagnosticsDeferredContext_9d7a0202.Scenario.Verify();
    }

    [Test]
    public void Suppressed_grammar_mutation_and_marker_failures_do_not_escape()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.CallbackDiagnosticsUnsupportedForms_9d7a0205.Scenario.Verify();
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.CallbackDiagnosticsReadOnlyInputs_9d7a0203.Scenario.Verify();
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.CallbackDiagnosticsTerminalMarker_9d7a0204.Scenario.Verify();
    }

    [Test]
    public async Task Editorconfig_overrides_all_callback_diagnostics()
    {
        var build = await _workspace.BuildConsumer("CallbackOverrides");
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
                    "MORPH0029",
                    "MORPH0030",
                    "MORPH0031",
                    "MORPH0032",
                    "MORPH0033"
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
