namespace Morphant.Generator.IntegrationTests.TypeMapperNestedMapTests;

[TestFixture]
internal sealed class ExternalLookupTests
{
    [Test]
    public void Resolves_a_nested_mapper_from_another_assembly()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExternalLookup_1ec1f24c.Scenario.Verify();
    }
}
