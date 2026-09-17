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

    [Test]
    public void ResolveInterpolation_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.ResolveInterpolation.Scenario.Verify();
    }

    [Test]
    public void MembersInterpolation_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.MembersInterpolation.Scenario.Verify();
    }

    [Test]
    public void Interpolation_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.Interpolation.Scenario.Verify();
    }

    [Test]
    public void VerbatimInterpolation_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.VerbatimInterpolation.Scenario.Verify();
    }

    [Test]
    public void RawInterpolation_preserves_values_on_create_and_update()
    {
        CSharp11.Scenarios.QueryVariableNames.RawInterpolation.Scenario.Verify();
    }

    [Test]
    public void LocalInterpolation_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.LocalInterpolation.Scenario.Verify();
    }

    [Test]
    public void AnonymousParameter_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.AnonymousParameter.Scenario.Verify();
    }

    [Test]
    public void TupleParameter_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.TupleParameter.Scenario.Verify();
    }

    [Test]
    public void AnonymousLocal_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.AnonymousLocal.Scenario.Verify();
    }

    [Test]
    public void TupleLocal_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.TupleLocal.Scenario.Verify();
    }

    [Test]
    public void AnonymousRange_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.AnonymousRange.Scenario.Verify();
    }

    [Test]
    public void TupleRange_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.TupleRange.Scenario.Verify();
    }

    [Test]
    public void RepeatedInto_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.RepeatedInto.Scenario.Verify();
    }

    [Test]
    public void SiblingQueries_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.SiblingQueries.Scenario.Verify();
    }

    [Test]
    public void ExistingSuffix_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.ExistingSuffix.Scenario.Verify();
    }

    [Test]
    public void UnusedSource_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.UnusedSource.Scenario.Verify();
    }

    [Test]
    public void MapperName_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.MapperName.Scenario.Verify();
    }

    [Test]
    public void JoinContinuation_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.JoinContinuation.Scenario.Verify();
    }

    [Test]
    public void ExplicitProjection_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.ExplicitProjection.Scenario.Verify();
    }

    [Test]
    public void NestedInterpolation_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.NestedInterpolation.Scenario.Verify();
    }

    [Test]
    public void QueryComment_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.QueryComment.Scenario.Verify();
    }

    [Test]
    public void ConstructUsingInterpolation_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.ConstructUsingInterpolation.Scenario.Verify();
    }

    [Test]
    public void ResolveUsingInterpolation_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.ResolveUsingInterpolation.Scenario.Verify();
    }

    [Test]
    public void ResolveReplacementInterpolation_preserves_values_on_create_and_update()
    {
        CSharp9.Scenarios.QueryVariableNames.ResolveReplacementInterpolation.Scenario.Verify();
    }
}
