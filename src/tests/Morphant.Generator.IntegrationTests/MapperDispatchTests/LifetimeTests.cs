using Scenario = Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ApplicationMapperLifetimes.Scenario;

namespace Morphant.Generator.IntegrationTests.MapperDispatchTests;

[TestFixture]
internal sealed class LifetimeTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void Respects_DI_lifetimes_during_repeated_nested_calls_and_disposal(bool transient) => Scenario.Verify(transient);
}
