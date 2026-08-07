namespace Morphant.Generator.IntegrationTests.TypeMapperDependencyGraphTests;

[TestFixture]
internal sealed class WrapperTests
{
    [Test]
    public void Ignores_transparent_nullability_and_parenthesis_wrappers()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Wrapper_7b8f2fe2.Scenario.Verify();
    }
}
