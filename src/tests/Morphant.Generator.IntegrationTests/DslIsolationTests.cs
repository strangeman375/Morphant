namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class DslIsolationTests
{
    [Test]
    public void Related_families_keep_independent_configuration_with_and_without_base_call()
    {
        CSharp9.Scenarios.DslFamilyIsolation.Scenario.Verify();
    }

    [Test]
    public void Mappers_sharing_a_tuple_pair_execute_their_own_callbacks()
    {
        CSharp9.Scenarios.DslFamilyRecovery.Scenario.Verify();
    }
}
