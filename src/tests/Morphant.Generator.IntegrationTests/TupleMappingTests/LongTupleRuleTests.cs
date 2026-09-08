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

    [TestCase(8, false)]
    [TestCase(8, true)]
    [TestCase(15, false)]
    [TestCase(15, true)]
    public void A_tail_value_does_not_override_Ignore_for_an_earlier_Item1(int arity, bool update) =>
        Scenario.VerifyIgnoredHead(arity, update);

    [TestCase(false)]
    [TestCase(true)]
    public void Constructs_and_updates_fields_across_two_Rest_boundaries(bool update) => Scenario.VerifyValueTuple(update);

    [TestCase(false)]
    [TestCase(true)]
    public void Distinguishes_a_null_long_tuple_from_a_present_default_value(bool present) => Scenario.VerifyNullableValueTuple(present);

    [TestCase(false)]
    [TestCase(true)]
    public void Reads_source_fields_across_two_Rest_boundaries(bool update) => Scenario.VerifyPhysicalSourcePaths(update);
}
