namespace Morphant.Generator.IntegrationTests.TypeMapperCreationResultTests;

[TestFixture]
internal sealed class DirectConstructTests
{
    [Test]
    public void Executes_expression_method_group_and_full_block_forms()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.DirectConstruct_b13efdce.Scenario.Verify();
    }

    [Test]
    public void Keeps_source_only_Construct_inactive_for_existing_destination()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.DirectConstruct_f6bde8db.Scenario.Verify();
    }
}
