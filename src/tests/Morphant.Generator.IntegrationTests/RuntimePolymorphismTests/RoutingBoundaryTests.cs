namespace Morphant.Generator.IntegrationTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class RoutingBoundaryTests
{
    [Test]
    public void Uses_application_lookup_across_mappers_but_keeps_standalone_exact() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismMapperBoundary_b82d0014.Scenario.Verify();

    [Test]
    public void Enforces_base_mode_before_dispatch_and_derived_mode_after_match() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismModes_b82d0015.Scenario.Verify();

    [Test]
    public void Preserves_zero_one_or_multiple_lookup_for_a_matched_pair() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismLookupLaw_b82d0016.Scenario.Verify();

    [Test]
    public void Resolves_the_exact_base_pair_before_running_its_dispatcher() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismBaseLookupLaw_b82d0017.Scenario.Verify();
}
