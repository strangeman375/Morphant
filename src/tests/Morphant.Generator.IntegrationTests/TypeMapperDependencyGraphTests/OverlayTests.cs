namespace Morphant.Generator.IntegrationTests.TypeMapperDependencyGraphTests;

[TestFixture]
internal sealed class OverlayTests
{
    [Test]
    public void Removes_overridden_dependencies_before_sharing()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Overlay_9694e323.Scenario.Verify();
    }
}
