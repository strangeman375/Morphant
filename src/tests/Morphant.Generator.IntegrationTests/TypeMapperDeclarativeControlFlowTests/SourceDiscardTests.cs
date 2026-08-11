namespace Morphant.Generator.IntegrationTests.TypeMapperDeclarativeControlFlowTests;

[TestFixture]
internal sealed class SourceDiscardTests
{
    [Test]
    public void Removes_structured_source_discards_without_changing_runtime_callbacks()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.SourceDiscard_a11ce00e.Scenario.Verify();
    }
}
