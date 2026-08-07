namespace Morphant.Generator.IntegrationTests.TypeMapperCreationResultTests;

[TestFixture]
internal sealed class CaptureTests
{
    [Test]
    public void Rejects_runtime_Configure_locals_for_direct_and_factory_code()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Capture_0ca4831f.Scenario.Verify();
    }

    [Test]
    public void Requires_direct_Construct_only_for_reachable_creation()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Capture_d45adfd5.Scenario.Verify();
    }
}
