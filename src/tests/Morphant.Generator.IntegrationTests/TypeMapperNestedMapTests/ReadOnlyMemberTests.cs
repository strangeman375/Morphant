namespace Morphant.Generator.IntegrationTests.TypeMapperNestedMapTests;

[TestFixture]
internal sealed class ReadOnlyMemberTests
{
    [Test]
    public void Updates_non_null_read_only_members_and_skips_null_members()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ReadOnlyMember_c82cdb4e.Scenario.Verify();
    }
}
