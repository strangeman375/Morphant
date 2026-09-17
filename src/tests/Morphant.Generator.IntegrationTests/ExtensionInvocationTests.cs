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

    [Test]
    public Task Imports_preserve_implicit_awaiter_binding() =>
        CSharp11.Scenarios.ExtensionInvocations.ImplicitBindings.Scenario.VerifyAwait();

    [Test]
    public void Imports_preserve_deconstruction_binding() =>
        CSharp11.Scenarios.ExtensionInvocations.ImplicitBindings.Scenario.VerifyDeconstruction();

    [Test]
    public void Imports_preserve_foreach_deconstruction_binding() =>
        CSharp11.Scenarios.ExtensionInvocations.ImplicitBindings.Scenario.VerifyLoopDeconstruction();

    [Test]
    public void Imports_preserve_positional_pattern_binding() =>
        CSharp11.Scenarios.ExtensionInvocations.ImplicitBindings.Scenario.VerifyPattern();

    [Test]
    public void Imports_preserve_collection_initializer_binding() =>
        CSharp11.Scenarios.ExtensionInvocations.ImplicitBindings.Scenario.VerifyInitializer();
}
