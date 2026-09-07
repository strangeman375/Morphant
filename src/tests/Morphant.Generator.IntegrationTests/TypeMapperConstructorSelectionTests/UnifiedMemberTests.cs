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
    public void Constructs_from_the_effective_value_and_evaluates_it_once(Route route) => Scenario.Verify(route);

    [Test]
    public void Member_auto_replaces_an_explicit_constructor_rule() => Scenario.VerifyMemberAuto();

    [Test]
    public void Keeps_the_explicit_constructor_overload_when_member_type_is_narrower() => Scenario.VerifyOverload();
}
