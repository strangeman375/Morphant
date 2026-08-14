namespace Morphant.Generator.IntegrationTests.TypeMapperEvaluationTests;

[TestFixture]
internal sealed class SharedValuesTests
{
    [Test]
    public void Evaluates_repeated_values_once_across_constructor_and_members()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.SharedValues_e2449755.Scenario.Verify();
    }
}
