namespace Morphant.Generator.IntegrationTests.TypeMapperMemberTests;

[TestFixture]
internal sealed class ResultAwareTests
{
    [Test]
    public void Keeps_previous_and_selected_constructor_result_distinct()
    {
        global::Morphant.Generator.IntegrationTests.CSharp11.Scenarios.ResultAware_3aa73f8a.Scenario.Verify();
    }

    [Test]
    public void Uses_the_selected_factory_and_direct_results_and_stops_on_null()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ResultAware_f6b07787.Scenario.Verify();
    }

    [Test]
    public void Provides_the_non_null_value_of_a_nullable_destination_as_result()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ResultAware_968bad04.Scenario.Verify();
    }
}
