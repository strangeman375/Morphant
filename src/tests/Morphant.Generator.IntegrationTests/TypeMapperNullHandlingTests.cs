namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class TypeMapperNullHandlingTests
{
    [Test]
    public void Applies_null_source_policy_before_destination_policy()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.NullHandling_8381d92c.Scenario.Verify();
    }

    [Test]
    public void Normalizes_nullable_values_and_omits_impossible_checks()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.NullHandling_f3d15fd6.Scenario.Verify();
    }
}
