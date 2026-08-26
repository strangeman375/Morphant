namespace Morphant.Generator.IntegrationTests.TupleMappingTests;

[TestFixture]
internal sealed class CompositionTests
{
    [Test]
    public void Preserves_tuple_lifecycle_fusion_and_observable_evaluation()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .TupleComposition_a7b10002.Scenario.Verify();
    }
}
