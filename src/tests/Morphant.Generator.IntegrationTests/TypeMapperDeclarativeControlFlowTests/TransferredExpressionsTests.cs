namespace Morphant.Generator.IntegrationTests.TypeMapperDeclarativeControlFlowTests;

[TestFixture]
internal sealed class TransferredExpressionsTests
{
    [Test]
    public void Executes_queries_and_deferred_local_functions_in_all_structured_surfaces()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TransferredExpressions_a11ce003.Scenario.Verify();
    }

    [Test]
    public void Rejects_custom_query_pattern_extensions_in_all_structured_surfaces()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.QueryPattern_a11ce007.Scenario.Verify();
    }
}
