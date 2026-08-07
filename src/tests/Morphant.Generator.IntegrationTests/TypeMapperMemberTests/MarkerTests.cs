namespace Morphant.Generator.IntegrationTests.TypeMapperMemberTests;

[TestFixture]
internal sealed class MarkerTests
{
    [Test]
    public void Keeps_an_unavailable_Auto_rule_as_an_unsupported_path()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Marker_2232b36b.Scenario.Verify();
    }

    [Test]
    public void Applies_typed_and_target_typed_Auto_and_Ignore_markers()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Marker_2b5ad463.Scenario.Verify();
    }
}
