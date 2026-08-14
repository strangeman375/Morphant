namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class TypeMapperDeclarativeValueTests
{
    [Test]
    public void Executes_exact_member_values_and_warning_free_nested_results()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .DeclarativeValueMembers_4d90a101.Scenario.Verify();
    }

    [Test]
    public void Preserves_value_constructor_binding_and_evaluation_order()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .DeclarativeValueConstructors_4d90a102.Scenario.Verify();
    }

    [Test]
    public void Rejects_mismatched_and_runtime_intrinsics_fail_closed()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .DeclarativeValueFailures_4d90a103.Scenario.Verify();
    }

    [Test]
    public void Compiles_and_executes_latest_target_typed_values()
    {
        global::Morphant.Generator.IntegrationTests.Latest.DeclarativeValueSurface.Scenario.Verify();
    }

    [Test]
    public void Preserves_collection_expressions_in_all_structured_surfaces()
    {
        global::Morphant.Generator.IntegrationTests.Latest
            .DeclarativeValueSurface.CollectionExpressionScenario.Verify();
    }
}
