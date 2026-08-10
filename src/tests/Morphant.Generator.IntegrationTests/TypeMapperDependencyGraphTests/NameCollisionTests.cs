namespace Morphant.Generator.IntegrationTests.TypeMapperDependencyGraphTests;

[TestFixture]
internal sealed class NameCollisionTests
{
    [Test]
    public void Avoids_pattern_variable_names_for_dependency_locals()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.NameCollision_fd601948.Scenario.Verify();
    }

    [Test]
    public void Renames_out_declarations_in_all_structured_surfaces()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.BoundNames_a11ce002.Scenario.Verify();
    }
}
