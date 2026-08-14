namespace Morphant.Generator.IntegrationTests.TypeMapperEvaluationTests;

[TestFixture]
internal sealed class RuntimeCallbackTests
{
    [Test]
    public void Evaluates_runtime_callbacks_independently_from_declarative_rules()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.OpaquePlan_116969a6.Scenario.Verify();
    }
}
