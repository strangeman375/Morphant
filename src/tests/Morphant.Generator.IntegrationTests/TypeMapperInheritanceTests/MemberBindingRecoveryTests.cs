using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InheritedBindingRecovery_s0502;

namespace Morphant.Generator.IntegrationTests.TypeMapperInheritanceTests;

[TestFixture]
internal sealed class MemberBindingRecoveryTests
{
    [Test]
    public void Throws_typed_configuration_failures_for_every_blocked_family_and_keeps_independent_pairs() =>
        Scenario.Verify();
}
