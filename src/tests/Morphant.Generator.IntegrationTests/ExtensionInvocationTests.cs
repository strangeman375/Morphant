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
    public void Conditional_fallback_preserves_member_indexer_method_and_void_calls() =>
        CSharp9.Scenarios.ExtensionInvocations.ConditionalFallbackMembers.Mappers.Scenario.Verify();

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

    [Test]
    public void CollectionCallerInformation_preserves_runtime_semantics() =>
        CSharp9.Scenarios.ExtensionInvocations.CollectionCallerInformation.Scenario.Verify();

    [Test]
    public void CollectionNested_preserves_runtime_semantics() =>
        CSharp9.Scenarios.ExtensionInvocations.CollectionNested.Scenario.Verify();

    [Test]
    public void CollectionPattern_preserves_runtime_semantics() =>
        CSharp9.Scenarios.ExtensionInvocations.CollectionPattern.Scenario.Verify();

    [Test]
    public void CollectionQuery_preserves_runtime_semantics() =>
        CSharp9.Scenarios.ExtensionInvocations.CollectionQuery.Scenario.Verify();

    [Test]
    public void CollectionSurfaces_preserves_runtime_semantics() =>
        CSharp9.Scenarios.ExtensionInvocations.CollectionSurfaces.Scenario.Verify();

    [Test]
    public void CollectionEffects_preserves_runtime_semantics() =>
        CSharp9.Scenarios.ExtensionInvocations.CollectionEffects.Scenario.Verify();

    [Test]
    public void CollectionOutArgument_preserves_runtime_semantics() =>
        CSharp9.Scenarios.ExtensionInvocations.CollectionOutArgument.Scenario.Verify();

    [Test]
    public void CollectionRefLike_preserves_runtime_semantics() =>
        CSharp9.Scenarios.ExtensionInvocations.CollectionRefLike.Scenario.Verify();

    [Test]
    public void CollectionAsyncConditional_preserves_runtime_semantics() =>
        CSharp9.Scenarios.ExtensionInvocations.CollectionAsyncConditional.Scenario.Verify();

    [Test]
    public void CollectionTypedLocal_preserves_runtime_semantics() =>
        CSharp9.Scenarios.ExtensionInvocations.CollectionTypedLocal.Scenario.Verify();

    [Test]
    public void CollectionIndex_preserves_runtime_semantics() =>
        CSharp9.Scenarios.ExtensionInvocations.CollectionIndex.Scenario.Verify();

    [Test]
    public void CollectionKeyword_preserves_runtime_semantics() =>
        CSharp9.Scenarios.ExtensionInvocations.CollectionKeyword.Scenario.Verify();

    [Test]
    public void CollectionIndexExpression_preserves_runtime_semantics() =>
        CSharp9.Scenarios.ExtensionInvocations.CollectionIndexExpression.Scenario.Verify();
}
