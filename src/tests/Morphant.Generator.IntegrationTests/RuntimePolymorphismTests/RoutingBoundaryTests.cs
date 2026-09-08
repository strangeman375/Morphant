namespace Morphant.Generator.IntegrationTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class RoutingBoundaryTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void Uses_application_lookup_across_mappers_but_keeps_standalone_exact(bool update) =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismMapperBoundary_b82d0014.Scenario.Verify(update);

    [Test]
    public void Enforces_base_mode_before_dispatch_and_derived_mode_after_match() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismModes_b82d0015.Scenario.Verify();

    [TestCase(false)]
    [TestCase(true)]
    public void Preserves_zero_one_or_multiple_lookup_for_a_matched_pair(bool update) =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismLookupLaw_b82d0016.Scenario.Verify(update);

    [TestCase(false)]
    [TestCase(true)]
    public void Resolves_the_exact_base_pair_before_running_its_dispatcher(bool update) =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismBaseLookupLaw_b82d0017.Scenario.Verify(update);
}
