namespace Morphant.Generator.IntegrationTests.TypeMapperCreationResultTests;

[TestFixture]
internal sealed class CaptureTests
{
    [Test]
    public void Rejects_runtime_Configure_locals_for_runtime_callbacks()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Capture_0ca4831f.Scenario.Verify();
    }

    [Test]
    public void Requires_an_explicit_result_policy_only_for_reachable_creation()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Capture_d45adfd5.Scenario.Verify();
    }
}
