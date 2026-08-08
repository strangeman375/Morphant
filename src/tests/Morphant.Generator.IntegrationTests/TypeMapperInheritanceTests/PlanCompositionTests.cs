namespace Morphant.Generator.IntegrationTests.TypeMapperInheritanceTests;

[TestFixture]
internal sealed class PlanCompositionTests
{
    [Test]
    public void Composes_same_level_pairs_transitively_regardless_of_order()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.PlanComposition_227dbda4.Scenario.Verify();
    }

    [Test]
    public void Prefers_a_same_level_pair_to_a_connected_base_pair()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.PlanComposition_160b0b46.Scenario.Verify();
    }

    [Test]
    public void Merges_included_Members_by_destination_member_and_rebuilds_dependencies()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.PlanComposition_19641a70.Scenario.Verify();
    }

    [Test]
    public void Accepts_interface_base_pair_assignability()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.PlanComposition_a0a2a071.Scenario.Verify();
    }

    [Test]
    public void Does_not_include_result_policy_and_recomputes_derived_construction()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.PlanComposition_07647072.Scenario.Verify();
    }

    [Test]
    public void Uses_the_nearest_explicit_base_pair_and_composes_transitively()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.PlanComposition_4be20a69.Scenario.Verify();
    }

    [Test]
    public void Does_not_include_Convert_and_local_Convert_replaces_included_Members()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.PlanComposition_241e949d.Scenario.Verify();
    }
}
