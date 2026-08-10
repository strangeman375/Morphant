namespace Morphant.Generator.IntegrationTests.TypeMapperCallbackTests;

[TestFixture]
internal sealed class ContextMarkerTests
{
    [Test]
    public void Rejects_runtime_use_of_declarative_context() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ContextMarker_deadbeef.Scenario.Verify();

    [Test]
    public void Rejects_deferred_context_capture_and_allows_extracted_operation() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.DeferredContext_a11ce004.Scenario.Verify();
}
