using Scenario = Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TupleLiteralComposition.Scenario;

namespace Morphant.Generator.IntegrationTests.TupleMappingTests;

[TestFixture]
internal sealed class LiteralCompositionTests
{
    [Test]
    public void Preserves_reads_conversions_and_member_branch_order(
        [Values(false, true)] bool referenceTuple,
        [Values(false, true)] bool update,
        [Values(false, true)] bool branch) => Scenario.Verify(referenceTuple, update, branch);

    [TestCase(false)]
    [TestCase(true)]
    public void Creates_reference_tuple_for_a_null_update_destination(bool branch) =>
        Scenario.Verify(referenceTuple: true, update: true, branch, nullDestination: true);
}
