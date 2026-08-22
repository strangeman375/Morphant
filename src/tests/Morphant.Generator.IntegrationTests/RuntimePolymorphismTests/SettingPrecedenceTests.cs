namespace Morphant.Generator.IntegrationTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class SettingPrecedenceTests
{
    [Test]
    public void Resolves_assembly_mapper_and_pair_precedence() =>
        global::Morphant.Generator.IntegrationTests.AssemblySettings.Scenarios
            .UnknownDerivedTypeHandling.Scenario.Verify();
}
