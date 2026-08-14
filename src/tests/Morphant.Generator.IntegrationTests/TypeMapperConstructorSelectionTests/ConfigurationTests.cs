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
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .InvalidConstructorSelection_f71c9a42.Scenario.Verify();
    }

    [Test]
    public void Resolves_included_current_and_connected_mapper_precedence()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .ConstructorSelectionHierarchy_9d7a0309.Scenario.Verify();
    }

    [Test]
    public void Uses_the_MSBuild_assembly_default_and_pair_override()
    {
        global::Morphant.Generator.IntegrationTests.AssemblySettings.Scenarios.ConstructorSelection.Scenario.Verify();
    }
}
