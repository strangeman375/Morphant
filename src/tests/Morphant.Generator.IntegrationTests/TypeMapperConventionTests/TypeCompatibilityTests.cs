namespace Morphant.Generator.IntegrationTests.TypeMapperConventionTests;

[TestFixture]
internal sealed class TypeCompatibilityTests
{
    [Test]
    public void Uses_only_warning_free_implicit_conversions()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TypeCompatibility_43514246.Scenario.Verify();
    }
}
