namespace Morphant.Generator.IntegrationTests.TypeMapperStructuredConstructTests;

[TestFixture]
internal sealed class ByConventionTests
{
    [Test]
    public void Selects_the_unambiguous_constructor_without_overrides()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ByConvention_2e3a2b6b.Scenario.Verify();
    }

    [Test]
    public void Applies_written_rules_before_remaining_automatic_arguments()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ByConvention_064618fb.Scenario.Verify();
    }
}
