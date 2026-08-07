namespace Morphant.Generator.IntegrationTests.TypeMapperNestedMapTests;

[TestFixture]
internal sealed class UnsupportedTests
{
    [Test]
    public void Rejects_ambiguous_or_incompatible_maps_without_implicit_auto_dispatch()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Unsupported_4217c0f0.Scenario.Verify();
    }
}
