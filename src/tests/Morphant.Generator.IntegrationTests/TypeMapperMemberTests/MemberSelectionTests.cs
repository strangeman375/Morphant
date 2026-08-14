namespace Morphant.Generator.IntegrationTests.TypeMapperMemberTests;

[TestFixture]
internal sealed class MemberSelectionTests
{
    [Test]
    public void Resolves_mapper_and_pair_MemberSelection_with_Default_inheritance()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.MemberSelection_ac87953f.Scenario.Verify();
    }

    [Test]
    public void Preserves_an_invalid_effective_MemberSelection_as_a_complete_stub()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InvalidMemberSelection_5d3e2b8a.Scenario.Verify();
    }

    [Test]
    public void Resolves_included_current_and_connected_mapper_precedence()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.MemberSelectionHierarchy_9d7a0310.Scenario.Verify();
    }

    [Test]
    public void Uses_the_MSBuild_assembly_default_and_pair_override()
    {
        global::Morphant.Generator.IntegrationTests.AssemblySettings.Scenarios.MemberSelection.Scenario.Verify();
    }
}
