namespace Morphant.Generator.IntegrationTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class SelectionTests
{
    [Test]
    public void Selects_the_unique_most_specific_branch_and_proxy_ancestor() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismSelection_b82d0001.Scenario.Verify();

    [Test]
    public void Reports_all_maximal_incomparable_interface_branches() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismAmbiguity_b82d0002.Scenario.Verify();

    [Test]
    public void Applies_throw_only_to_non_null_unknown_derived_sources() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismUnknown_b82d0003.Scenario.Verify();

    [Test]
    public void Throw_rejects_unknown_runtime_types_even_without_links() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismEmptyStrict_b82d0004.Scenario.Verify();

    [Test]
    public void Null_sources_follow_the_base_pair_policy_before_dispatch() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismNulls_b82d0018.Scenario.Verify();
}
