namespace Morphant.Generator.IntegrationTests.TypeMapperEvaluationTests;

[TestFixture]
internal sealed class NameCollisionTests
{
    [Test]
    public void Accepts_user_pattern_names_that_match_generated_temporaries()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.NameCollision_fd601948.Scenario.Verify();
    }

    [Test]
    public void Accepts_out_variables_in_all_structured_callbacks()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.BoundNames_a11ce002.Scenario.Verify();
    }
}
