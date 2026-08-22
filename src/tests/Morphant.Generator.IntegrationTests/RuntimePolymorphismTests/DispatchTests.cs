namespace Morphant.Generator.IntegrationTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class DispatchTests
{
    [Test]
    public void Routes_create_and_strict_update_through_the_exact_pair() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismDispatch_b82d0005.Scenario.Verify();
}
