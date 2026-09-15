using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.SwitchExhaustiveness;

namespace Morphant.Generator.IntegrationTests.TypeMapperEvaluationTests;

[TestFixture]
internal sealed class SwitchExhaustivenessTests
{
    [Test]
    public void Preserves_switch_evaluation_and_unmatched_values(
        [Values(false, true)] bool resolve,
        [Values("Complete", "Guarded", "Nullable", "Enum")] string mode,
        [Values("Create", "UpdateNull", "Reuse", "Replace")] string operation,
        [Values(0, 1, 2)] int choice,
        [Values(false, true)] bool guard)
    {
        Scenario.Verify(resolve, mode, operation, choice, guard);
    }
}
