namespace Morphant.Generator.IntegrationTests.TupleMappingTests;

[TestFixture]
internal sealed class InteractionTests
{
    [Test]
    public void Composes_with_inheritance_runtime_dispatch_and_DI()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .TupleInteractions_a7b10006.Scenario.Verify();
    }
}
