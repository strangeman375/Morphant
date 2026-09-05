namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class DslIsolationTests
{
    [Test]
    public void Related_families_keep_independent_configuration_with_and_without_base_call()
    {
        CSharp9.Scenarios.DslFamilyIsolation.Scenario.Verify();
    }

    [Test]
    public void Invalid_family_callback_uses_typed_recovery_without_affecting_other_pairs()
    {
        CSharp9.Scenarios.DslFamilyRecovery.Scenario.Verify();
    }
}
