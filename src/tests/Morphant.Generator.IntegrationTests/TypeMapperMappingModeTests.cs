namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class TypeMapperMappingModeTests
{
    [Test]
    public void Gates_Create_and_Update_without_changing_the_shared_contract()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.MappingMode_a9e37137.Scenario.Verify();
    }

    [Test]
    public void Resolves_pair_included_mapper_base_and_library_precedence()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.MappingModePrecedence_9d7a0305.Scenario.Verify();
    }

    [Test]
    public void Keeps_invalid_effective_modes_local_to_unoverridden_pairs()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.MappingModeInvalid_9d7a0306.Scenario.Verify();
    }

    [Test]
    public void Uses_the_MSBuild_assembly_default_and_pair_override()
    {
        global::Morphant.Generator.IntegrationTests.AssemblySettings.Scenarios.MappingMode.Scenario.Verify();
    }
}
