namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class IncludeMembersTests
{
    [Test]
    public void Includes_nested_source_scopes_in_all_supported_forms() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .IncludeMembers_7e2b0901.Scenario.Verify();

    [Test]
    public void Preserves_invalid_pair_recovery_and_independent_mappings() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .IncludeMembersDiagnosticsRecovery_7e2b0902.Scenario.Verify();
}
