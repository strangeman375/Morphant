namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class InheritanceDiagnosticsTests
{
    [Test]
    public void Duplicate_base_configuration_rejects_every_known_pair()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .InheritanceMapperRecovery_7c0f0024.Scenario.Verify();
    }

    [Test]
    public void Invalid_IncludeBase_edges_reject_only_dependent_pairs()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .InheritancePairRecovery_7c0f0025.Scenario.Verify();
    }

    [Test]
    public void Inaccessible_inherited_callbacks_reject_every_effective_family()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .InheritanceAccessibilityRecovery_7c0f0028.Scenario.Verify();
    }

    [Test]
    public async Task Editorconfig_overrides_all_inheritance_diagnostics()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var build = await workspace.BuildConsumer("InheritanceOverrides");
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
                    "MORPH0024",
                    "MORPH0025",
                    "MORPH0026",
                    "MORPH0027",
                    "MORPH0028"
                }));
            Assert.That(diagnostics, Has.All.Contains("warning MORPH"));
            Assert.That(build.GetGeneratedFiles(), Is.Not.Empty);
        });
    }
}
