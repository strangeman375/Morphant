namespace Morphant.Generator.IntegrationTests.TypeMapperConstructorSelectionTests;

[TestFixture]
internal sealed class ByConventionTests
{
    [Test]
    public void Applies_selection_to_ByConvention_but_not_explicit_Construct()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ByConvention_7ef0668e.Scenario.Verify();
    }

    [Test]
    public void Greediest_counts_written_ByConvention_rules_and_omissions()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ByConvention_5e51e8dc.Scenario.Verify();
    }

    [Test]
    public void Rejects_warning_producing_automatic_ByConvention_arguments()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ByConvention_1e7f4785.Scenario.Verify();
    }
}
