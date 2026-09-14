namespace Morphant.Generator.IntegrationTests.TypeMapperNestedMapTests;

[TestFixture]
internal sealed class ReadOnlyMemberTests
{
    [Test]
    public void Updates_non_null_read_only_members_and_skips_null_members()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ReadOnlyMember_c82cdb4e.Scenario.Verify();
    }

    [Test]
    public void Preserves_nested_update_evaluation_order_operation_and_destination_identity(
        [Values] CSharp9.Scenarios.SharedNestedUpdate.MappingKind kind,
        [Values("Create", "UpdateNull", "UpdateExisting")] string operation,
        [Values(false, true)] bool hasChild) =>
        CSharp9.Scenarios.SharedNestedUpdate.Scenario.Verify(kind, operation, hasChild);

    [TestCase(false)]
    [TestCase(true)]
    public void Skips_nested_updates_after_a_null_factory_result(bool update) =>
        CSharp9.Scenarios.SharedNestedUpdate.Scenario.VerifyNullFactory(update);
}
