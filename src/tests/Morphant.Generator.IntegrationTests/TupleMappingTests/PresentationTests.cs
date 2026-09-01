namespace Morphant.Generator.IntegrationTests.TupleMappingTests;

[TestFixture]
internal sealed class PresentationTests
{
    [Test]
    public void Shares_one_tuple_presentation_across_independent_mappers()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .TupleSharedPresentation_a7b10005.Scenario.Verify();
    }
}
