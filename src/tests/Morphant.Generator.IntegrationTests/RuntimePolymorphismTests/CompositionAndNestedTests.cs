namespace Morphant.Generator.IntegrationTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class CompositionAndNestedTests
{
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
