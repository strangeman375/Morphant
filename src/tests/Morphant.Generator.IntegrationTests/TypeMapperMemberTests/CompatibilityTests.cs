namespace Morphant.Generator.IntegrationTests.TypeMapperMemberTests;

[TestFixture]
internal sealed class CompatibilityTests
{
    [Test]
    public void Uses_warning_free_implicit_conversions_for_explicit_and_automatic_values()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Compatibility_e6b8eb30.Scenario.Verify();
    }
}
