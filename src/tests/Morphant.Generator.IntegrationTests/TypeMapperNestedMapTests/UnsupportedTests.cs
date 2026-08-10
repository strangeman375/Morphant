namespace Morphant.Generator.IntegrationTests.TypeMapperNestedMapTests;

[TestFixture]
internal sealed class UnsupportedTests
{
    [Test]
    public void Rejects_ambiguous_or_incompatible_maps_without_implicit_auto_dispatch()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Unsupported_4217c0f0.Scenario.Verify();
    }

    [Test]
    public void Rejects_non_terminal_nested_markers_in_all_structured_surfaces()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TerminalMarker_a11ce001.Scenario.Verify();
    }
}
