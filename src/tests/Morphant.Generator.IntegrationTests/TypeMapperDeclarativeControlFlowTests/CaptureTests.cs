namespace Morphant.Generator.IntegrationTests.TypeMapperDeclarativeControlFlowTests;

[TestFixture]
internal sealed class CaptureTests
{
    [Test]
    public void Transfers_constants_and_mapper_members_but_rejects_runtime_Configure_local()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Capture_744a52a8.Scenario.Verify();
    }

    [Test]
    public void Rejects_deferred_previous_and_result_captures_but_allows_snapshots()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.DeferredInputs_a11ce008.Scenario.Verify();
    }
}
