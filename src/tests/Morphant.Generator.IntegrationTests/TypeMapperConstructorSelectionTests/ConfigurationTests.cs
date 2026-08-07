namespace Morphant.Generator.IntegrationTests.TypeMapperConstructorSelectionTests;

[TestFixture]
internal sealed class ConfigurationTests
{
    [Test]
    public void Resolves_mapping_mapper_and_library_Default_precedence()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Configuration_51ecb8a3.Scenario.Verify();
    }

    [Test]
    public void Rejects_an_invalid_ConstructorSelection_only_when_construction_is_required()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InvalidConstructorSelection_f71c9a42.Scenario.Verify();
    }

}
