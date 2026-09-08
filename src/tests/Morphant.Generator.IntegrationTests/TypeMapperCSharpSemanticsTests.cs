namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class TypeMapperCSharpSemanticsTests
{
    [TestCase("ConstructUsing", false)]
    [TestCase("ConstructUsing", true)]
    [TestCase("ResolveUsing", false)]
    [TestCase("ResolveUsing", true)]
    [TestCase("Convert", false)]
    [TestCase("Convert", true)]
    [TestCase("Constructor", false)]
    [TestCase("Constructor", true)]
    public void Preserves_caller_information_in_runtime_callbacks_and_target_typed_constructors(string callback, bool update)
    {
        CSharp9.Scenarios.RuntimeCallerInfo.Scenario.Verify(callback, update);
    }

    [Test]
    public void Reads_a_delegate_property_once_per_mapping_call()
    {
        CSharp9.Scenarios.CallbackEvaluation.Scenario.VerifyDelegateProperty();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Executes_finally_when_a_runtime_callback_returns_early_for_null(bool update)
    {
        CSharp9.Scenarios.CallbackEvaluation.Scenario.VerifyFinallyOnNullReturn(update);
    }

    [Test]
    public void Defers_source_reads_and_skips_unused_structured_locals()
    {
        CSharp9.Scenarios.CallbackEvaluation.Scenario.VerifyDeferredSourceCapture();
    }

    [Test]
    public void Preserves_nameof_aliases_and_optional_extension_arguments()
    {
        CSharp9.Scenarios.ExpressionContext.Scenario.VerifyNames();
    }

    [Test]
    public void Preserves_overloads_selected_by_source_argument_types()
    {
        CSharp9.Scenarios.ExpressionContext.Scenario.VerifyOverloads();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Preserves_checked_and_unchecked_expression_context(bool checkedExpression)
    {
        CSharp9.Scenarios.ExpressionContext.Scenario.VerifyOverflow(checkedExpression);
    }

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
