namespace Morphant.Generator.IntegrationTests.TypeMapperInheritanceTests;

[TestFixture]
internal sealed class GenericAndAccessibilityTests
{
    [Test]
    public void Provides_the_open_configuration_surface_for_a_closed_base()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.GenericAndAccessibility_f2272e41.Scenario.Verify();
    }

    [Test]
    public void Substitutes_closed_type_arguments_in_an_included_nested_map()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.GenericAndAccessibility_9abffc73.Scenario.Verify();
    }

    [Test]
    public void Instantiates_generic_IncludeBase_types_for_a_nested_mapper()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.GenericAndAccessibility_a15526de.Scenario.Verify();
    }

    [Test]
    public void Treats_inaccessible_inherited_expressions_as_unsupported_plans()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.GenericAndAccessibility_242822d4.Scenario.Verify();
    }

    [Test]
    public void Removes_an_overridden_inaccessible_member_rule_before_emission()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.GenericAndAccessibility_33e35b5a.Scenario.Verify();
    }

    [Test]
    public void Does_not_transfer_an_inaccessible_Construct_plan()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.GenericAndAccessibility_d13d9d85.Scenario.Verify();
    }
}
