namespace Morphant.Generator.IntegrationTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class BasePlanReachabilityTests
{
    [Test]
    public void Uses_strict_dispatch_without_an_interface_base_plan() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismUnreachableBase_b82d0006.Scenario.Verify();
}
