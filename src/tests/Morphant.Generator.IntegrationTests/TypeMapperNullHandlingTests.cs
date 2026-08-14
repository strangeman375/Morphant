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

    [Test]
    public void Resolves_pair_included_mapper_base_and_library_precedence()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.NullHandlingPrecedence_9d7a0307.Scenario.Verify();
    }

    [Test]
    public void Preserves_invalid_policies_independently_and_allows_overrides()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.NullHandlingInvalid_9d7a0308.Scenario.Verify();
    }

    [Test]
    public void Uses_MSBuild_assembly_defaults_and_pair_overrides()
    {
        global::Morphant.Generator.IntegrationTests.AssemblySettings.Scenarios.NullHandling.Scenario.Verify();
    }
}
