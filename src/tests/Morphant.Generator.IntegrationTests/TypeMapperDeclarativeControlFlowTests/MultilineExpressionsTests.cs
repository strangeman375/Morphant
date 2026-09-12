using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.MultilineExpressions;

namespace Morphant.Generator.IntegrationTests.TypeMapperDeclarativeControlFlowTests;

[TestFixture]
internal sealed class MultilineExpressionsTests
{
    [TestCase(false, false, "allowed")]
    [TestCase(false, true, "allowed")]
    [TestCase(false, false, "42")]
    [TestCase(false, true, "42")]
    [TestCase(false, false, "invalid")]
    [TestCase(false, true, "invalid")]
    [TestCase(true, false, "allowed")]
    [TestCase(true, true, "allowed")]
    public void Preserves_values_evaluation_order_and_selected_branches(
        bool factory, bool update, string name) => Scenario.Verify(factory, update, name);
}
