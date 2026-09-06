namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class ResolveTryGetValueTests
{
    [Test]
    public void Preserves_guards_out_variables_and_evaluation_counts()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ResolveTryGetValue.Scenario.Verify();
    }
}
