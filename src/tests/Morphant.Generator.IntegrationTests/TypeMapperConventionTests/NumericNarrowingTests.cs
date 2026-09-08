using Scenario = Morphant.Generator.IntegrationTests.CSharp9.Scenarios.NumericNarrowing.Scenario;

namespace Morphant.Generator.IntegrationTests.TypeMapperConventionTests;

[TestFixture]
internal sealed class NumericNarrowingTests
{
    [TestCase("Create")]
    [TestCase("UpdateNull")]
    [TestCase("UpdateExisting")]
    public void Convention_does_not_apply_an_explicit_numeric_conversion(string operation)
    {
        Scenario.Verify(operation);
    }
}
