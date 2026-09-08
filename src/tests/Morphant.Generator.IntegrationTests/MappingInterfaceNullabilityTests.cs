using Scenario = Morphant.Generator.IntegrationTests.CSharp9.Scenarios.NullEntryPoints.Scenario;

namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class MappingInterfaceNullabilityTests
{
    [TestCase("CreateWithContext")]
    [TestCase("UpdateWithContext")]
    [TestCase("Create")]
    [TestCase("Update")]
    [TestCase("MapCreate")]
    [TestCase("MapUpdate")]
    public void Default_null_policy_returns_null_even_with_a_non_nullable_destination_contract(string entryPoint)
    {
        Scenario.Verify(entryPoint);
    }
}
