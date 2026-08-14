namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class TypeMapperConfigurationFailureTests
{
    [Test]
    public void Reports_an_invalid_pair_without_hiding_independent_contracts()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConfigurationFailure_f27b94e1.Scenario.Verify();
    }
}
