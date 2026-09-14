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

    [Test]
    public void Preserves_mutations_of_struct_sources_and_user_locals(
        [Values("Create", "UpdateNull", "UpdateExisting")] string operation,
        [Values(false, true)] bool local) =>
        CSharp9.Scenarios.SharedNestedUpdate.Scenario.VerifyMutableState(operation, local);

    [Test]
    public void Evaluates_the_source_between_the_null_guard_and_context_access(
        [Values(false, true)] bool capture,
        [Values(false, true)] bool hasChild) =>
        CSharp9.Scenarios.SharedNestedUpdate.Scenario.VerifyContextAccess(capture, hasChild);

    [TestCase("Create")]
    [TestCase("UpdateNull")]
    [TestCase("UpdateExisting")]
    public void Preserves_constant_conversions_in_source_selectors(string operation) =>
        CSharp9.Scenarios.SharedNestedUpdate.Scenario.VerifyConstant(operation);

    [Test]
    public void Preserves_checked_update_order_failures_mutations_and_identity(
        [Values(false, true)] bool capture,
        [Values("Create", "UpdateNull", "UpdateExisting")] string operation,
        [Values(0, 1, 2)] int destinationKind,
        [Values(false, true)] bool throwSource) =>
        CSharp9.Scenarios.CheckedNestedUpdate.Scenario.Verify(capture, operation, destinationKind, throwSource);

    [Test]
    public void Checks_wide_destinations_before_accessing_the_mapper(
        [Values(false, true)] bool capture,
        [Values(0, 1, 2)] int destinationKind) =>
        CSharp9.Scenarios.CheckedNestedUpdate.Scenario.VerifyContextAccess(capture, destinationKind);
}
