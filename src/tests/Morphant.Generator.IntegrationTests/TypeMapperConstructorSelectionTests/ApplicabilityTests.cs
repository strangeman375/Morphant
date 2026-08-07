namespace Morphant.Generator.IntegrationTests.TypeMapperConstructorSelectionTests;

[TestFixture]
internal sealed class ApplicabilityTests
{
    [Test]
    public void Ignores_inherited_values_for_direct_and_manual_mappings()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Applicability_b627d758.Scenario.Verify();
    }

    [Test]
    public void Preserves_explicit_map_level_values_as_invalid_state()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Applicability_1c7ecb32.Scenario.Verify();
    }
}
