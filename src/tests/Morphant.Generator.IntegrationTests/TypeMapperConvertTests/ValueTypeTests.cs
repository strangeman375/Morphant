namespace Morphant.Generator.IntegrationTests.TypeMapperConvertTests;

[TestFixture]
internal sealed class ValueTypeTests
{
    [Test]
    public void Preserves_nullable_value_source_and_previous_states()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ValueType_9e9960f1.Scenario.Verify();
    }
}
