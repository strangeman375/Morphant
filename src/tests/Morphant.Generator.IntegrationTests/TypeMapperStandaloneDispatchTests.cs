namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class TypeMapperStandaloneDispatchTests
{
    [Test]
    public void Uses_generated_exact_pairs_from_the_mapper_hierarchy()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.StandaloneDispatch_8c2f1a4b.Scenario.Verify();
    }

    [Test]
    public void Explains_when_a_nested_pair_requires_the_application_mapper()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.StandaloneDispatchBoundary_9d7a0106.Scenario.Verify();
    }
}
