namespace Morphant.Generator.IntegrationTests.TypeMapperStructuredConstructTests;

[TestFixture]
internal sealed class LifecycleTests
{
    [Test]
    public void Specializes_previous_availability_without_skipping_condition_effects()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Lifecycle_88242046.Scenario.Verify();
    }

    [Test]
    public void Keeps_an_unguarded_previous_selection_unsupported_for_Create()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Lifecycle_d46efb66.Scenario.Verify();
    }

    [Test]
    public void Selects_previous_or_replacement_without_evaluating_other_branches()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Lifecycle_ee1a3190.Scenario.Verify();
    }

    [Test]
    public void Keeps_unsupported_constructor_branch_path_sensitive()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Lifecycle_16cb8056.Scenario.Verify();
    }
}
