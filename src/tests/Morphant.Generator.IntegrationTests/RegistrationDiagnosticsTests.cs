namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class RegistrationDiagnosticsTests
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
    public void Throws_for_both_operations_of_a_suppressed_unsupported_contract()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RegistrationUnsupported_31a8b6c2.Scenario.Verify();
    }

    [Test]
    public void Executes_only_the_first_suppressed_duplicate_plan()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RegistrationDuplicate_6fd24b81.Scenario.Verify();
    }

    [Test]
    public void Preserves_surfaces_and_an_independent_pair_when_unification_is_suppressed()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RegistrationUnification_f1407a2c.Scenario.Verify();
    }

    [Test]
    public void Executes_runtime_and_manual_policies_for_every_opaque_root_family()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RegistrationOpaque_48bd10ee.Scenario.Verify();
    }

    [Test]
    public void Executes_an_independent_plan_when_unavailable_pair_is_suppressed()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RegistrationUnavailable_2d0c31af.Scenario.Verify();
    }

    [Test]
    public async Task Reports_an_unavailable_file_local_pair_in_a_package_build()
    {
        var build = await _workspace.BuildConsumer(
            "RegistrationUnavailable");
        var diagnostics = GetCompilerDiagnostics(build.Process.Output);

        Assert.Multiple(() =>
        {
            Assert.That(build.Process.ExitCode, Is.Not.EqualTo(0));
            Assert.That(diagnostics, Has.Length.EqualTo(1), build.Process.Output);
            Assert.That(diagnostics.Single(), Does.Contain("error MORPH0011"));
            Assert.That(diagnostics.Single(), Does.Contain("HiddenSource"));
        });
    }

    [Test]
    public async Task Editorconfig_overrides_diagnostic_presentation()
    {
        var build = await _workspace.BuildConsumer("RegistrationOverrides");
        var diagnostics = GetCompilerDiagnostics(build.Process.Output);

        Assert.Multiple(() =>
        {
            Assert.That(
                build.Process.ExitCode,
                Is.EqualTo(0),
                build.Process.Output);
            Assert.That(
                diagnostics.Select(GetDiagnosticId),
                Is.EqualTo(new[] { "MORPH0013", "MORPH0014" }));
            Assert.That(diagnostics, Has.All.Contains("warning MORPH"));
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
