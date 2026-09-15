using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.CustomPreviousGuard;

namespace Morphant.Generator.IntegrationTests.TypeMapperEvaluationTests;

[TestFixture]
internal sealed class CustomPreviousGuardTests
{
    [Test]
    public void Preserves_custom_logical_operators_and_boolean_conversions(
        [Values("Resolve", "ConstructMembers", "ResolveMembers")] string policy,
        [Values(false, true)] bool before,
        [Values(0, 1, 2)] int operation) => Scenario.Verify(policy, before, operation);
}
