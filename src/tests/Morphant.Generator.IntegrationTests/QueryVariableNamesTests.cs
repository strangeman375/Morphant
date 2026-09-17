namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class QueryVariableNamesTests
{
    [Test]
    public void Clauses_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.Clauses.Scenario.Verify();
    }

    [Test]
    public void Collisions_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.Collisions.Scenario.Verify();
    }

    [Test]
    public void Escaped_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.Escaped.Scenario.Verify();
    }

    [Test]
    public void Nested_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.Nested.Scenario.Verify();
    }

    [Test]
    public void Inferred_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.Inferred.Scenario.Verify();
    }

    [Test]
    public void TupleInference_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.TupleInference.Scenario.Verify();
    }

    [Test]
    public void ConstructUsing_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.ConstructUsing.Scenario.Verify();
    }

    [Test]
    public void ResolveUsing_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.ResolveUsing.Scenario.Verify();
    }
}
