namespace Morphant.Generator.IntegrationTests.TypeMapperConventionTests;

[TestFixture]
internal sealed class DestinationKindTests
{
    [Test]
    public void Supports_record_and_constructed_generic_destinations()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.DestinationKind_76894b16.Scenario.Verify();
    }

    [Test]
    public void Supports_value_and_nullable_value_destination_lifecycles()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.DestinationKind_4c9e5873.Scenario.Verify();
    }

    [Test]
    public void Updates_direct_abstract_and_interface_destinations_without_a_create_fallback()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.DestinationKind_88f809a3.Scenario.Verify();
    }
}
