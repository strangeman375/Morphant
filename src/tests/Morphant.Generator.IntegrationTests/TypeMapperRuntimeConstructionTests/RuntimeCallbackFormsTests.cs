namespace Morphant.Generator.IntegrationTests.TypeMapperRuntimeConstructionTests;

[TestFixture]
internal sealed class RuntimeCallbackFormsTests
{
    [Test]
    public void Executes_lambda_block_method_group_and_delegate_forms_once()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimeCallback_baa540b5.Scenario.Verify();
    }

    [Test]
    public void Supports_previous_aware_replacement_and_terminal_null()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimeCallback_aac81ef9.Scenario.Verify();
    }
}
