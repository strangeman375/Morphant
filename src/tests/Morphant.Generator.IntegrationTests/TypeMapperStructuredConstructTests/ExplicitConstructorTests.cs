namespace Morphant.Generator.IntegrationTests.TypeMapperStructuredConstructTests;

[TestFixture]
internal sealed class ExplicitConstructorTests
{
    [Test]
    public void Suppresses_member_convention_only_for_a_provided_constructor_parameter()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExplicitConstructor_8b09d558.Scenario.Verify();
    }

    [Test]
    public void Keeps_a_required_corresponding_member_and_reuses_its_automatic_value()
    {
        global::Morphant.Generator.IntegrationTests.CSharp11.Scenarios.ExplicitConstructor_5d0a3c54.Scenario.Verify();
    }

    [Test]
    public void Executes_source_only_constructor_for_Create_and_null_Update()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExplicitConstructor_7f173aff.Scenario.Verify();
    }

    [Test]
    public void Preserves_overload_argument_order_casts_and_omission()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExplicitConstructor_3e4adc35.Scenario.Verify();
    }
}
