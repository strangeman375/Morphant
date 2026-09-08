using Scenario = Morphant.Generator.IntegrationTests.CSharp9.Scenarios.LongTupleRules.Scenario;

namespace Morphant.Generator.IntegrationTests.TupleMappingTests;

[TestFixture]
internal sealed class LongTupleRuleTests
{
    [TestCase(8, false)]
    [TestCase(8, true)]
    [TestCase(15, false)]
    [TestCase(15, true)]
    public void Ignoring_a_tail_element_does_not_ignore_an_earlier_Item1(int arity, bool update) =>
        Scenario.VerifyIgnoredTail(arity, update);
}
