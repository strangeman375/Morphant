namespace Morphant.Generator.IntegrationTests.TypeMapperConvertTests;

[TestFixture]
internal sealed class InvalidPlanTests
{
    [Test]
    public void Rejects_captures_duplicates_mixed_plans_and_map_settings()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InvalidPlan_521c7ef8.Scenario.Verify();
    }

    [Test]
    public void Does_not_interpret_declarative_markers_or_run_conventions()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InvalidPlan_66207f7c.Scenario.Verify();
    }
}
