namespace Morphant.Generator.IntegrationTests.MapperDispatchTests;

[TestFixture]
internal sealed class RegistrationTests
{
    [Test]
    public void Enforces_exactly_one_registration_for_each_requested_pair()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ApplicationRegistration_9d7a0102.Scenario.Verify();
    }
}
