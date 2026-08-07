namespace Morphant.Generator.IntegrationTests.TypeMapperMemberTests;

[TestFixture]
internal sealed class ImmutableUpdateTests
{
    [Test]
    public void Allows_no_op_Update_without_explicit_construct_intent()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ImmutableUpdate_40bffe22.Scenario.Verify();
    }
}
