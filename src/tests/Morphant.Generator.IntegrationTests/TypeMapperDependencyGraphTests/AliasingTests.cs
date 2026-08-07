namespace Morphant.Generator.IntegrationTests.TypeMapperDependencyGraphTests;

[TestFixture]
internal sealed class AliasingTests
{
    [Test]
    public void Shares_an_alias_read_without_contracting_assignment_order()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Aliasing_9cff7b29.Scenario.Verify();
    }
}
