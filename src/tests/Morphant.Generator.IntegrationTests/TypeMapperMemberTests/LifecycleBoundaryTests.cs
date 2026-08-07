namespace Morphant.Generator.IntegrationTests.TypeMapperMemberTests;

[TestFixture]
internal sealed class LifecycleBoundaryTests
{
    [Test]
    public void Keeps_impossible_creation_time_rules_as_unsupported_paths()
    {
        global::Morphant.Generator.IntegrationTests.CSharp11.Scenarios.LifecycleBoundary_b4fd61d5.Scenario.Verify();
    }
}
