using NestedUpdate = Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimePolymorphismNestedUpdate.Scenario;

namespace Morphant.Generator.IntegrationTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class CompositionAndNestedTests
{
    [Test]
    public void Nested_Create_selects_the_derived_branch() => NestedUpdate.CreateSelectsDerivedBranch();

    [Test]
    public void Update_with_null_outer_destination_uses_nested_Create() => NestedUpdate.NullOuterDestinationUsesNestedCreate();

    [Test]
    public void Nested_derived_Update_preserves_both_destinations() => NestedUpdate.UpdatePreservesBothDestinations();

    [Test]
    public void Nested_Update_with_null_destination_applies_the_derived_policy() => NestedUpdate.MissingNestedDestinationAppliesDerivedPolicy();

    [Test]
    public void Nested_Update_reports_the_incompatible_derived_destination() => NestedUpdate.WrongNestedDestinationReportsSelectedBranch();

    [TestCase(false)]
    [TestCase(true)]
    public void Nested_Update_handles_null_source_before_destination(bool hasDestination) => NestedUpdate.NullSourcePreservesNestedDestination(hasDestination);

    [Test]
    public void Nested_Update_uses_the_replacement_destination_member() => NestedUpdate.ReplacementUsesItsOwnNestedDestination();

    [Test]
    public void Nested_Update_with_empty_replacement_does_not_reuse_the_old_member() => NestedUpdate.EmptyReplacementDoesNotFallBackToPreviousMember();

    [Test]
    public void Dispatches_transitively_at_root_and_nested_calls() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismNested_b82d0009.Scenario.Verify();

    [Test]
    public void Runs_derived_configuration_rules() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismDerivedRules_b82d0010.Scenario.Verify();

    [Test]
    public void IncludeBase_does_not_import_dispatch_links() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismIncludeBase_b82d0011.Scenario.Verify();

    [Test]
    public void Exact_IncludeBase_does_not_import_dispatch_links() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismExactIncludeBase_b82d0012.Scenario.Verify();

    [Test]
    public void Supports_generic_mapper_substitution() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismGeneric_b82d0013.Scenario.Verify();
}
