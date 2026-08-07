namespace Morphant.Generator.IntegrationTests.TypeMapperDeclarativeControlFlowTests;

[TestFixture]
internal sealed class ThrowTests
{
    [Test]
    public void Preserves_throw_expression_and_non_exhaustive_switch_fallback()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Throw_8f7cf658.Scenario.Verify();
    }
}
