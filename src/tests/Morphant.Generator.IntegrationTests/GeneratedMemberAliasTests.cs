namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class GeneratedMemberAliasTests
{
    [Test]
    public void Preserves_original_members_in_conventions_overlays_and_nested_updates()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.MemberAliases.Scenario.Verify();
    }
}
