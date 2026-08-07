namespace Morphant.Generator.IntegrationTests.TypeMapperDependencyGraphTests;

[TestFixture]
internal sealed class OpaquePlanTests
{
    [Test]
    public void Keeps_factory_and_direct_bodies_outside_cross_plan_sharing()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.OpaquePlan_116969a6.Scenario.Verify();
    }
}
