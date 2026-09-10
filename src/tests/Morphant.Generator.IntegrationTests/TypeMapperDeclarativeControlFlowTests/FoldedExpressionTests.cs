using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.FoldedExpressions;

namespace Morphant.Generator.IntegrationTests.TypeMapperDeclarativeControlFlowTests;

[TestFixture]
internal sealed class FoldedExpressionTests
{
    [Test]
    public void Preserves_grouping_numeric_promotion_and_overloads(
        [Values] Surface surface,
        [Values(false, true)] bool update) => Scenario.Verify(surface, update);
}
