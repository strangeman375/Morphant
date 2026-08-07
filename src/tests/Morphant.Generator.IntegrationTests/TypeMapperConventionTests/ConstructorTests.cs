namespace Morphant.Generator.IntegrationTests.TypeMapperConventionTests;

[TestFixture]
internal sealed class ConstructorTests
{
    [Test]
    public void Omits_an_unmatched_optional_constructor_parameter()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Constructor_12b1639c.Scenario.Verify();
    }

    [Test]
    public void Does_not_use_mapper_lexical_access_to_a_private_constructor()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Constructor_01ff2834.Scenario.Verify();
    }

    [Test]
    public void Evaluates_a_shared_constructor_and_required_member_value_once()
    {
        global::Morphant.Generator.IntegrationTests.CSharp11.Scenarios.Constructor_861b2632.Scenario.Verify();
    }

    [Test]
    public void Honors_SetsRequiredMembers_without_synthesizing_a_required_assignment()
    {
        global::Morphant.Generator.IntegrationTests.CSharp11.Scenarios.Constructor_9cc0ca16.Scenario.Verify();
    }

    [Test]
    public void Selects_the_only_parameterized_constructor_and_maps_its_arguments()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Constructor_4542a26b.Scenario.Verify();
    }

    [Test]
    public void Does_not_fallback_from_an_ambiguous_constructor_selection()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Constructor_dd34c318.Scenario.Verify();
    }
}
