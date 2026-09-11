namespace Morphant.Generator.IntegrationTests.TypeMapperEvaluationTests;

[TestFixture]
internal sealed class AliasingTests
{
    [Test]
    public void Evaluates_each_alias_read_before_assignments()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Aliasing_9cff7b29.Scenario.Verify();
    }
}
