namespace Morphant.Generator.IntegrationTests.TypeMapperConvertTests;

[TestFixture]
internal sealed class LifecycleTests
{
    [Test]
    public void Preserves_original_call_state_and_authoritative_results()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Lifecycle_a0564ce3.Scenario.Verify();
    }

    [Test]
    public void Applies_only_MappingMode_as_the_manual_operation_gate()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Lifecycle_7a235a44.Scenario.Verify();
    }
}
