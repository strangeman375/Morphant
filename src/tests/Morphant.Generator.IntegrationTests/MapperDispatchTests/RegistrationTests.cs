namespace Morphant.Generator.IntegrationTests.MapperDispatchTests;

[TestFixture]
internal sealed class RegistrationTests
{
    [Test]
    public void Enforces_exactly_one_registration_for_each_requested_pair()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .ApplicationRegistration_9d7a0102.Scenario.Verify();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Preserves_DI_activation_exceptions_and_retries_on_the_next_root_call(bool update) =>
        CSharp9.Scenarios.ApplicationActivationFailure.Scenario.Verify(update);
}
