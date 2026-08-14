namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class CompositionDiagnosticsTests
{
    [Test]
    public void Rejects_every_duplicate_slot_without_executing_callbacks()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.CompositionDuplicates_5c0f0019.Scenario.Verify();
    }

    [Test]
    public void Rejects_both_mixed_orders_and_executes_an_independent_pair()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.CompositionMixed_5c0f0020.Scenario.Verify();
    }

    [Test]
    public void Executes_every_result_policy_with_Members_and_an_imported_boundary()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.CompositionPositive_5c0f0000.Scenario.Verify();
    }

    [Test]
    public async Task Editorconfig_overrides_both_composition_diagnostics()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var build = await workspace.BuildConsumer("CompositionOverrides");
        var diagnostics = CompilerDiagnosticOutput.Read(build.Process.Output);

        Assert.Multiple(() =>
        {
            Assert.That(
                build.Process.ExitCode,
                Is.EqualTo(0),
                build.Process.Output);
            Assert.That(
                diagnostics.Select(CompilerDiagnosticOutput.GetId),
                Is.EqualTo(new[] { "MORPH0019", "MORPH0020" }));
            Assert.That(diagnostics, Has.All.Contains("warning MORPH"));
            Assert.That(build.GetGeneratedFiles(), Is.Not.Empty);
        });
    }
}
