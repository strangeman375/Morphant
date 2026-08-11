namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class ConfigurationDiagnosticsTests
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
    public void Executes_a_source_connected_base_configuration()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConfigurationChain_308ba72c.Scenario.Verify();
    }

    [Test]
    public void Throws_for_both_operations_when_base_configuration_is_metadata_only()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConfigurationBaseUnavailable_4c0f0016.Scenario.Verify();
    }

    [Test]
    public void Recovers_only_directly_visible_pairs_after_root_flow_escape()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConfigurationRootFlow_4c0f0017.Scenario.Verify();
    }

    [Test]
    public void Recovers_one_pair_and_executes_an_independent_pair()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConfigurationPairFlow_4c0f0018.Scenario.Verify();
    }

    [Test]
    public void Leaves_all_callback_arguments_to_transfer_analysis()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.CallbackDiscovery_a11ce010.Scenario.Verify();
    }

    [Test]
    public async Task Missing_and_inherited_Configure_emit_no_artifacts()
    {
        var build = await _workspace.BuildConsumer("ConfigurationMissing");
        var diagnostics = GetCompilerDiagnostics(build.Process.Output);
        var generatedFiles = Directory.Exists(build.GeneratedDirectory)
            ? Directory.GetFiles(
                build.GeneratedDirectory,
                "*.cs",
                SearchOption.AllDirectories)
            : [];

        Assert.Multiple(() =>
        {
            Assert.That(build.Process.ExitCode, Is.Not.EqualTo(0));
            Assert.That(
                diagnostics.Select(GetDiagnosticId),
                Is.EqualTo(new[] { "MORPH0015", "MORPH0015" }),
                build.Process.Output);
            Assert.That(generatedFiles, Is.Empty);
        });
    }

    [Test]
    public async Task Editorconfig_overrides_all_configuration_diagnostics()
    {
        var build = await _workspace.BuildConsumer("ConfigurationOverrides");
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
                    "MORPH0015",
                    "MORPH0016",
                    "MORPH0017",
                    "MORPH0018"
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
