namespace Morphant.Generator.IntegrationTests.TypeMapperDependencyGraphTests;

[TestFixture]
internal sealed class PathSensitivityTests
{
    [Test]
    public void Shares_only_values_required_by_the_selected_path()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.PathSensitivity_de7bb566.Scenario.Verify();
    }
}
