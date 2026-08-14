namespace Morphant.Generator.IntegrationTests.MapperDispatchTests;

[TestFixture]
internal sealed class SuccessfulMappingTests
{
    [Test]
    public void Dispatches_create_and_update_to_the_exact_generated_pair()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ApplicationMapping_9d7a0101.Scenario.Verify();
    }

    [Test]
    public void Uses_scoped_dependencies_for_closed_generic_and_nullable_pairs()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ApplicationServices_9d7a0304.Scenario.Verify();
    }
}
