namespace Morphant.Generator.IntegrationTests.TupleMappingTests;

[TestFixture]
internal sealed class InteractionTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void Generic_inheritance_preserves_a_nested_nullable_element_of_a_long_tuple(bool hasTail)
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .TupleGenericInheritance.Scenario.VerifyLongTuple(hasTail);
    }

    [Test]
    public void Generic_inheritance_preserves_tuple_construction_and_member_overrides()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .TupleGenericInheritance.Scenario.Verify();
    }

    [Test]
    public void Generic_inheritance_preserves_tuple_fields_in_Convert()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .TupleGenericInheritance.Scenario.VerifyConversion();
    }

    [Test]
    public void Composes_with_inheritance_runtime_dispatch_and_DI()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .TupleInteractions_a7b10006.Scenario.Verify();
    }
}
