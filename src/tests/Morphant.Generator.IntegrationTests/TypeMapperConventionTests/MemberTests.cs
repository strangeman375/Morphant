namespace Morphant.Generator.IntegrationTests.TypeMapperConventionTests;

[TestFixture]
internal sealed class MemberTests
{
    [Test]
    public void Rejects_creation_when_a_required_member_has_no_convention_value()
    {
        global::Morphant.Generator.IntegrationTests.CSharp11.Scenarios.Member_0047f37c.Scenario.Verify();
    }

    [Test]
    public void Does_not_use_mapper_lexical_access_to_private_destination_members()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Member_a76b168f.Scenario.Verify();
    }

    [Test]
    public void Maps_the_supported_member_matrix_in_destination_order()
    {
        global::Morphant.Generator.IntegrationTests.CSharp11.Scenarios.Member_36f4c993.Scenario.Verify();
    }

    [Test]
    public void Matches_members_by_exact_case_sensitive_name_only()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.MemberNames_9d7a0302.Scenario.Verify();
    }
}
