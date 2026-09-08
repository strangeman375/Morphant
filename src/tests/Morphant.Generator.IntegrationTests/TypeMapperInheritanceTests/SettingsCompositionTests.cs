namespace Morphant.Generator.IntegrationTests.TypeMapperInheritanceTests;

[TestFixture]
internal sealed class SettingsCompositionTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void Default_continues_through_included_settings_before_current_roots(bool update) =>
        CSharp9.Scenarios.SettingsInheritance.Scenario.VerifyIncludedSettings(update);

    [Test]
    public void Included_unknown_derived_handling_overrides_current_root() =>
        CSharp9.Scenarios.SettingsInheritance.Scenario.VerifyIncludedUnknownDerivedHandling();

    [Test]
    public void Local_settings_and_Auto_override_inherited_rules() =>
        CSharp9.Scenarios.SettingsInheritance.Scenario.VerifyLocalSettings();

    [TestCase(false)]
    [TestCase(true)]
    public void Local_Ignore_preserves_values_and_Auto_requests_inherited_flattening(bool update) =>
        CSharp9.Scenarios.SettingsInheritance.Scenario.VerifyIgnoreAndAuto(update);

    [TestCase(false)]
    [TestCase(true)]
    public void Final_Default_inherits_root_settings_regardless_of_call_order(bool settingsAfterRegistration) =>
        CSharp9.Scenarios.SettingsInheritance.Scenario.VerifyRootOrder(settingsAfterRegistration);

    [Test]
    public void Resolves_all_included_pair_settings_before_mapper_roots()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.SettingsComposition_dcc2ec2a.Scenario.Verify();
    }
}
