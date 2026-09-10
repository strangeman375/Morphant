using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConditionalTransfer;

namespace Morphant.Generator.IntegrationTests.TypeMapperDeclarativeControlFlowTests;

[TestFixture]
internal sealed class ConditionalTransferTests
{
    [Test]
    public void Preserves_short_circuiting_and_evaluates_values_once(
        [Values] Surface surface,
        [Values(null, "", "abc")] string? text,
        [Values(false, true)] bool update) =>
        Scenario.VerifySurface(surface, text, update);

    private static IEnumerable<TestCaseData> Expressions()
    {
        var cases = new (ExpressionCase Case, string? Text, string Value, string Trace)[]
        {
            (ExpressionCase.Arithmetic, null, "null", "RA"),
            (ExpressionCase.Arithmetic, "", "null", "RMA"),
            (ExpressionCase.Arithmetic, "abc", "4", "RMA"),
            (ExpressionCase.Chain, null, "fallback", "R"),
            (ExpressionCase.Chain, "abc", "3", "RE"),
            (ExpressionCase.NestedChain, null, "fallback", "R"),
            (ExpressionCase.NestedChain, "", "fallback", "RAM"),
            (ExpressionCase.NestedChain, "abc", "ABC1", "RAME"),
            (ExpressionCase.Indexer, null, "?", "R"),
            (ExpressionCase.Indexer, "abc", "a", "REI"),
            (ExpressionCase.Statement, null, "done", "R"),
            (ExpressionCase.Statement, "", "done", "RAM"),
            (ExpressionCase.Statement, "abc", "done", "RAMATabc11"),
            (ExpressionCase.Deferred, null, "fallback", "DRRR"),
            (ExpressionCase.Deferred, "abc", "abc1", "DRATabc1RATabc1RAM"),
            (ExpressionCase.CoalesceThrow, null, "", "R"),
            (ExpressionCase.CoalesceThrow, "", "", "RAM"),
            (ExpressionCase.CoalesceThrow, "abc", "abc1", "RAM"),
            (ExpressionCase.ExplicitStatic, null, "fallback", "RFE"),
            (ExpressionCase.ExplicitStatic, "abc", "abc", "RE")
        };
        foreach (var item in cases)
        foreach (var update in new[] { false, true })
            yield return new TestCaseData(item.Case, item.Text, update, item.Value, item.Trace);
    }

    [TestCaseSource(nameof(Expressions))]
    public void Preserves_composed_expressions_and_deferred_evaluation(
        ExpressionCase expression, string? text, bool update, string expected, string expectedTrace) =>
        Scenario.VerifyExpression(expression, text, update, expected, expectedTrace);
}
