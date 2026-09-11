using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.UnifiedConstructorValues;

namespace Morphant.Generator.IntegrationTests.TypeMapperConstructorSelectionTests;

[TestFixture]
internal sealed class UnifiedMemberTests
{
    [TestCase(Route.Automatic)]
    [TestCase(Route.ByConvention)]
    [TestCase(Route.Auto)]
    [TestCase(Route.Value)]
    [TestCase(Route.ByConventionAuto)]
    [TestCase(Route.ByConventionValue)]
    [TestCase(Route.Resolve)]
    [TestCase(Route.Omitted)]
    public void Preserves_explicit_values_and_shares_automatic_constructor_rules(Route route) => Scenario.Verify(route);

    [Test]
    public void Member_auto_runs_after_an_explicit_constructor_rule() => Scenario.VerifyMemberAuto();

    [Test]
    public void Keeps_the_explicit_constructor_overload_when_member_type_is_narrower() => Scenario.VerifyOverload();
}
