namespace Morphant.Generator.IntegrationTests.TupleMappingTests;

[TestFixture]
internal sealed class FactoryTests
{
    [Test]
    public void Keeps_factory_results_authoritative_and_allows_nested_update()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .TupleFactories_a7b10003.Scenario.Verify();
    }
}
