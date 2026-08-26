namespace Morphant.Generator.IntegrationTests.TupleMappingTests;

[TestFixture]
internal sealed class ConventionTests
{
    [Test]
    public void Maps_tuple_elements_by_name_without_physical_identity_shortcuts()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .TupleConvention_a7b10001.Scenario.Verify();
    }
}
