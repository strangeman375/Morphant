using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.OperationDispatch;

namespace Morphant.Generator.IntegrationTests.TypeMapperEvaluationTests;

[TestFixture]
public sealed class OperationDispatchTests
{
    [Test]
    public void Preserves_user_condition_effects_and_mapping_lifecycle(
        [Values(false, true)] bool resolve,
        [Values("Create", "UpdateNull", "Reuse", "Replace")] string operation,
        [Values(false, true)] bool conditionResult,
        [Values(false, true)] bool throwCondition)
    {
        Scenario.Verify(resolve, operation, conditionResult, throwCondition);
    }
}
