namespace Morphant.Generator.IntegrationTests.TypeMapperEvaluationTests;

[TestFixture]
internal sealed class OverloadIdentityTests
{
    [Test]
    public void Keeps_calls_to_different_overloads_independent()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.OverloadIdentity_ecdf4df4.Scenario.Verify();
    }
}
