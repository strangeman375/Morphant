namespace Morphant.Generator.IntegrationTests.TypeMapperConstructorSelectionTests;

[TestFixture]
internal sealed class CompatibilityTests
{
    [Test]
    public void Keeps_candidate_nullability_separate_and_rebinds_optional_arguments() =>
        CSharp9.Scenarios.ConstructorCompatibility.Scenario.Verify();
}
