using Scenario = Morphant.Generator.IntegrationTests.CSharp9.Scenarios.NullableTuplePropertyCapture.Scenario;

namespace Morphant.Generator.IntegrationTests.TupleMappingTests;

[TestFixture]
internal sealed class NullablePropertyCaptureTests
{
    [TestCase("Create")]
    [TestCase("UpdateNull")]
    [TestCase("UpdateExisting")]
    [TestCase("CreateNullSource")]
    [TestCase("UpdateNullSource")]
    public void Preserves_the_property_value_before_constructor_and_member_callbacks(string operation) =>
        Scenario.Verify(operation);
}
