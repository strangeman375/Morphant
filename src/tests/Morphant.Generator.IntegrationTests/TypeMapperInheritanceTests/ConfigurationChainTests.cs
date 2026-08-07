namespace Morphant.Generator.IntegrationTests.TypeMapperInheritanceTests;

[TestFixture]
internal sealed class ConfigurationChainTests
{
    [Test]
    public void Does_not_add_base_pair_registrations_to_the_derived_mapper()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConfigurationChain_308ba72c.Scenario.Verify();
    }

    [Test]
    public void Repeated_pair_starts_clean_but_keeps_connected_root_settings()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConfigurationChain_8f7b3926.Scenario.Verify();
    }
}
