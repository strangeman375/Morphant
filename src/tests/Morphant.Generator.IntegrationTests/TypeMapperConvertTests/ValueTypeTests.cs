namespace Morphant.Generator.IntegrationTests.TypeMapperConvertTests;

[TestFixture]
internal sealed class ValueTypeTests
{
    [Test]
    public void Preserves_nullable_value_source_and_previous_states()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ValueType_9e9960f1.Scenario.Verify();
    }

    [Test]
    public void Reports_an_empty_previous_value_when_Create_reads_it()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.MissingPreviousValue_9d7a0105.Scenario.Verify();
    }
}
