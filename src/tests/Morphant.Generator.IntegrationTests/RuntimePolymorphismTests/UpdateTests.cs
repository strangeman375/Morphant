namespace Morphant.Generator.IntegrationTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class UpdateTests
{
    [Test]
    public void Preserves_derived_identity_policy_and_replacement() =>
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .RuntimePolymorphismUpdate_b82d0007.Scenario.Verify();
}
