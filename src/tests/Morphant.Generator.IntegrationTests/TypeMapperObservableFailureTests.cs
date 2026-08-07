namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class TypeMapperObservableFailureTests
{
    [Test]
    public void Preserves_observable_failures_and_independent_contracts()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ObservableFailures_f27b94e1.Scenario.Verify();
    }
}
