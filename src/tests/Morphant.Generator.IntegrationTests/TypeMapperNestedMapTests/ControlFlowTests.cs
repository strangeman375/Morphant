namespace Morphant.Generator.IntegrationTests.TypeMapperNestedMapTests;

[TestFixture]
internal sealed class ControlFlowTests
{
    [Test]
    public void Executes_only_selected_nested_branches_and_shares_a_local()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ControlFlow_5548f36e.Scenario.Verify();
    }
}
