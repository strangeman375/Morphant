namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class FlatteningTests
{
    [Test]
    public void Flattens_supported_source_paths_with_CSharp9() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .Flattening_9a31c001.Scenario.Verify();

    [Test]
    public void Uses_the_MSBuild_default_and_pair_override() =>
        global::Morphant.Generator.IntegrationTests.AssemblySettings.Scenarios
            .Flattening.Scenario.Verify();
}
