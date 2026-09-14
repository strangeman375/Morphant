using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.MemberOperationGuards;

namespace Morphant.Generator.IntegrationTests.TypeMapperEvaluationTests;

[TestFixture]
public sealed class MemberOperationGuardTests
{
    [Test]
    public void Selects_members_for_the_public_operation(
        [Values(GuardKind.UpdateOnly, GuardKind.CreateOnly)] GuardKind kind,
        [Values("Create", "UpdateNull", "UpdateExisting")] string operation)
    {
        Scenario.Verify(kind, operation, allowed: false);
    }

    [Test]
    public void Preserves_short_circuiting_and_condition_effects(
        [Values(GuardKind.OperationFirstOr, GuardKind.CallFirstOr,
            GuardKind.OperationFirstAnd, GuardKind.CallFirstAnd)] GuardKind kind,
        [Values("Create", "UpdateNull", "UpdateExisting")] string operation,
        [Values(false, true)] bool allowed)
    {
        Scenario.Verify(kind, operation, allowed);
    }

    [Test]
    public void Preserves_user_defined_logical_operators(
        [Values("Create", "UpdateNull", "UpdateExisting")] string operation)
    {
        Scenario.Verify(GuardKind.UserDefinedAnd, operation, allowed: false);
    }
}
