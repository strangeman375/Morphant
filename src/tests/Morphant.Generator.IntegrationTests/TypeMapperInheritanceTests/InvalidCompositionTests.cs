namespace Morphant.Generator.IntegrationTests.TypeMapperInheritanceTests;

[TestFixture]
internal sealed class InvalidCompositionTests
{
    [Test]
    public void Preserves_invalid_IncludeBase_forms_as_unsupported_states()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InvalidComposition_c2abd125.Scenario.Verify();
    }

    [Test]
    public void Rejects_duplicate_base_Configure_calls_for_the_mapper()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InvalidComposition_c945271c.Scenario.Verify();
    }

    [Test]
    public void Keeps_included_declarative_settings_inactive_for_local_Convert()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InvalidComposition_1cbf6d17.Scenario.Verify();
    }
}
