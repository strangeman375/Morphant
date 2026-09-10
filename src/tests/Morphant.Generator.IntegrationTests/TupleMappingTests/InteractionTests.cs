namespace Morphant.Generator.IntegrationTests.TupleMappingTests;

[TestFixture]
internal sealed class InteractionTests
{
    [Test]
    public void Preserves_tuple_construction_and_nullable_member_overrides_through_generic_IncludeBase() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .TupleGenericInheritance.Scenario.Verify();

    [Test]
    public void Composes_with_inheritance_runtime_dispatch_and_DI()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .TupleInteractions_a7b10006.Scenario.Verify();
    }
}
