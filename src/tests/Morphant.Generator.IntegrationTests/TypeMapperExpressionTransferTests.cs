namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class TypeMapperExpressionTransferTests
{
    [Test]
    public async Task Preserves_async_runtime_callbacks()
    {
        await global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.AsyncTransfer_a11ce009.Scenario.Verify();
    }

    [Test]
    public void Preserves_unsafe_context_in_structured_and_runtime_callbacks()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.UnsafeTransfer_a11ce00a.Scenario.Verify();
    }

    [Test]
    public void Preserves_local_warning_and_nullable_context()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.LexicalContext_a11ce00b.Scenario.Verify();
    }

    [Test]
    public void Preserves_null_conditional_extension_binding()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionBinding_a11ce00c.Scenario.Verify();
    }

    [Test]
    public void Rejects_untransferable_extension_binding_before_emission()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TransferPreflight_a11ce00d.Scenario.Verify();
    }

    [Test]
    public void Preserves_caller_information_in_all_structured_surfaces()
    {
        global::Morphant.Generator.IntegrationTests.Latest.Scenarios.CallerInfo_a11ce005.Scenario.Verify();
    }

    [Test]
    public void Rejects_file_local_helpers_in_all_structured_surfaces()
    {
        global::Morphant.Generator.IntegrationTests.CSharp11.Scenarios.FileLocal_a11ce006.Scenario.Verify();
    }
}
