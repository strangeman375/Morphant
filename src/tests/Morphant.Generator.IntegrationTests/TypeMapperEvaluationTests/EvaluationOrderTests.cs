namespace Morphant.Generator.IntegrationTests.TypeMapperEvaluationTests;

[TestFixture]
internal sealed class EvaluationOrderTests
{
    [Test]
    public void Preserves_constructor_then_member_expression_order()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.EvaluationOrder_fce49890.Scenario.Verify();
    }
}
