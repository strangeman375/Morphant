namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class ExtensionInvocationTests
{
    [Test]
    public void All_callback_surfaces_preserve_evaluation_and_destination_lifecycle() =>
        CSharp9.Scenarios.ExtensionInvocations.Surfaces.Scenario.Verify();

    [Test]
    public void Chains_preserves_runtime_semantics() =>
        CSharp9.Scenarios.ExtensionInvocations.Chains.Scenario.Verify();

    [Test]
    public void Conversions_preserves_runtime_semantics() =>
        CSharp9.Scenarios.ExtensionInvocations.Conversions.Scenario.Verify();

    [Test]
    public void CallerInformation_preserves_runtime_semantics() =>
        CSharp9.Scenarios.ExtensionInvocations.CallerInformation.Scenario.Verify();

    [Test]
    public void SourceScope_preserves_runtime_semantics() =>
        CSharp9.Scenarios.ExtensionInvocations.SourceScope.Mappers.Scenario.Verify();

    [Test]
    public void ConditionalFallback_preserves_runtime_semantics() =>
        CSharp9.Scenarios.ExtensionInvocations.ConditionalFallback.Mappers.Scenario.Verify();

    [Test]
    public void ImportIsolation_preserves_runtime_semantics() =>
        CSharp9.Scenarios.ExtensionInvocations.ImportIsolation.Scenario.Verify();
    [Test]
    public void Multiline_layout_preserves_literal_values_and_conditional_effects() =>
        CSharp9.Scenarios.ExtensionInvocations.Layout.Scenario.Verify();
}
