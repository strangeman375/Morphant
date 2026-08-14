namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class ConfigurationDiagnosticsTests
{
    [Test]
    public void Executes_a_source_connected_base_configuration()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .ConfigurationDiagnosticsChain_9d7a0207.Scenario.Verify();
    }

    [Test]
    public void Throws_for_both_operations_when_base_configuration_is_metadata_only()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .ConfigurationBaseUnavailable_4c0f0016.Scenario.Verify();
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
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .ConfigurationDiagnosticsCallbackDiscovery_9d7a0206.Scenario
            .Verify();
    }

    [Test]
    public async Task Missing_and_inherited_Configure_emit_no_artifacts()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var build = await workspace.BuildConsumer("ConfigurationMissing");
        var diagnostics = CompilerDiagnosticOutput.Read(build.Process.Output);
        var generatedFiles = build.GetGeneratedFiles();

        Assert.Multiple(() =>
        {
            Assert.That(build.Process.ExitCode, Is.Not.EqualTo(0));
            Assert.That(
                diagnostics.Select(CompilerDiagnosticOutput.GetId),
                Is.EqualTo(new[] { "MORPH0015", "MORPH0015" }),
                build.Process.Output);
            Assert.That(generatedFiles, Is.Empty);
        });
    }

    [Test]
    public async Task Editorconfig_overrides_all_configuration_diagnostics()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var build = await workspace.BuildConsumer("ConfigurationOverrides");
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
                    "MORPH0015",
                    "MORPH0016",
                    "MORPH0017",
                    "MORPH0018"
                }));
            Assert.That(diagnostics, Has.All.Contains("warning MORPH"));
            Assert.That(build.GetGeneratedFiles(), Is.Not.Empty);
        });
    }
}
