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
    [TestCase(Route.Convention)]
    [TestCase(Route.InitialResult)]
    public void Preserves_constructor_evaluations_and_member_initialization(Route route) => Scenario.Verify(route);

    [TestCase(false)]
    [TestCase(true)]
    public void Readable_complex_arguments_preserve_conversions_branches_and_independent_members(bool preferred) =>
        Scenario.VerifyComplexArguments(preferred);

    [TestCase(false)]
    [TestCase(true)]
    public void Preserves_both_tuple_stages_and_the_initial_result(bool readsResult) => Scenario.VerifyTuple(readsResult);
    [TestCase(false)]
    [TestCase(true)]
    public void Evaluates_initial_tuple_elements_before_member_locals_and_conditions(bool condition) =>
        Scenario.VerifyTupleBlock(condition);

    [TestCase(false)]
    [TestCase(true)]
    public void Preserves_the_original_operation_when_a_member_switch_does_not_match(bool update) =>
        Scenario.VerifyUnmatchedSwitch(update);
}
