namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class ConstructionDiagnosticsTests
{
    [Test]
    public void Previous_value_aliases_preserve_struct_copies_and_member_updates() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .StructuredResultValues.Scenario.VerifyValueCopies();

    [Test]
    public void Previous_nullable_values_distinguish_default_from_absence() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .StructuredResultValues.Scenario.VerifyNullableValues();

    [Test]
    public void Suppressed_invalid_results_keep_valid_reuse_and_typed_failures() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .StructuredResultValues.Scenario.VerifySuppressedInvalidResults();

    [Test]
    public void Suppressed_construction_failures_keep_path_sensitive_recovery()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .ConstructionDiagnosticsRecovery_9c0f0035.Scenario.Verify();
    }

    [Test]
    public async Task Editorconfig_overrides_all_construction_diagnostics()
    {
        using var workspace = new ConsumerBuildWorkspace();
        var build = await workspace.BuildConsumer("ConstructionOverrides");
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
                    "MORPH0035",
                    "MORPH0036",
                    "MORPH0037",
                    "MORPH0038",
                    "MORPH0039",
                    "MORPH0062"
                }));
            Assert.That(diagnostics, Has.All.Contains("warning MORPH"));
            Assert.That(build.GetGeneratedFiles(), Is.Not.Empty);
        });
    }
}
