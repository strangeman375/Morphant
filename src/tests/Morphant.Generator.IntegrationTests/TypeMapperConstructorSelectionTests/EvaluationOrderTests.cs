using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConstructionAndMembersEvaluation;

namespace Morphant.Generator.IntegrationTests.TypeMapperConstructorSelectionTests;

[TestFixture]
internal sealed class EvaluationOrderTests
{
    [TestCase(Route.Expressions)]
    [TestCase(Route.Local)]
    [TestCase(Route.Condition)]
    [TestCase(Route.Factory)]
    [TestCase(Route.Swap)]
    public void Preserves_constructor_evaluations_then_member_values_then_assignments(Route route) => Scenario.Verify(route);

    [TestCase(false)]
    [TestCase(true)]
    public void Preserves_both_tuple_stages_and_the_initial_result(bool readsResult) => Scenario.VerifyTuple(readsResult);
}
