namespace Morphant.Generator.IntegrationTests.TypeMapperDependencyGraphTests;

[TestFixture]
internal sealed class OverloadIdentityTests
{
    [Test]
    public void Does_not_share_same_text_bound_to_different_overloads()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.OverloadIdentity_ecdf4df4.Scenario.Verify();
    }
}
