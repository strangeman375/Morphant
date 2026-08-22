namespace Morphant.Generator.IntegrationTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class ValueTypeTests
{
    [Test]
    public void Supports_boxed_sources_destinations_and_nullable_values() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismValues_b82d0008.Scenario.Verify();
}
