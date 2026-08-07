namespace Morphant.Generator.IntegrationTests.TypeMapperDependencyGraphTests;

[TestFixture]
internal sealed class EvaluationOrderTests
{
    [Test]
    public void Preserves_explicit_constructor_argument_order_while_sharing()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.EvaluationOrder_fce49890.Scenario.Verify();
    }
}
