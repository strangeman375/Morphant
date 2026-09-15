namespace Morphant.Generator.IntegrationTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class ValueTypeTests
{
    [Test]
    public void Supports_boxed_sources_destinations_and_nullable_values() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismValues_b82d0008.Scenario.Verify();

    [TestCase(false, "Create")]
    [TestCase(false, "UpdateExisting")]
    [TestCase(true, "Create")]
    [TestCase(true, "UpdateExisting")]
    [TestCase(true, "UpdateNull")]
    public void Preserves_strongly_typed_value_destination_branches(bool nullable, string operation) =>
        CSharp9.Scenarios.ValueDestinationPolymorphism.Scenario.Verify(nullable, operation);
}
