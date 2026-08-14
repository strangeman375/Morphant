namespace Morphant.Generator.IntegrationTests.TypeMapperEvaluationTests;

[TestFixture]
internal sealed class AliasingTests
{
    [Test]
    public void Evaluates_an_aliased_source_value_once_without_reordering_assignments()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Aliasing_9cff7b29.Scenario.Verify();
    }
}
