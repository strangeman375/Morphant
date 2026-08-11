namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class InheritanceDiagnosticsTests
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
    public void Duplicate_base_configuration_rejects_every_known_pair()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InheritanceMapperRecovery_7c0f0024.Scenario.Verify();
    }

    [Test]
    public void Invalid_IncludeBase_edges_reject_only_dependent_pairs()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InheritancePairRecovery_7c0f0025.Scenario.Verify();
    }

    [Test]
    public void Inaccessible_inherited_callbacks_reject_every_effective_family()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InheritanceAccessibilityRecovery_7c0f0028.Scenario.Verify();
    }

    [Test]
    public async Task Editorconfig_overrides_all_inheritance_diagnostics()
    {
        var build = await _workspace.BuildConsumer("InheritanceOverrides");
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
                    "MORPH0024",
                    "MORPH0025",
                    "MORPH0026",
                    "MORPH0027",
                    "MORPH0028"
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
