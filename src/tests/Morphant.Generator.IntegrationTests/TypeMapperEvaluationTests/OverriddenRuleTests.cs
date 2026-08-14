namespace Morphant.Generator.IntegrationTests.TypeMapperEvaluationTests;

[TestFixture]
internal sealed class OverriddenRuleTests
{
    [Test]
    public void Does_not_evaluate_a_rule_replaced_by_a_with_expression()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Overlay_9694e323.Scenario.Verify();
    }
}
