using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConstructorMemberRules_s0501;

namespace Morphant.Generator.IntegrationTests.TypeMapperConstructorSelectionTests;

[TestFixture]
internal sealed class ExplicitMemberTests
{
    [TestCase(ConstructionRoute.Automatic)]
    [TestCase(ConstructionRoute.ByConvention)]
    [TestCase(ConstructionRoute.Explicit)]
    [TestCase(ConstructionRoute.Resolve)]
    public void Applies_explicit_setter_after_construction(ConstructionRoute route) =>
        Scenario.VerifyMutable(route);

    [TestCase(ConstructionRoute.Automatic)]
    [TestCase(ConstructionRoute.ByConvention)]
    [TestCase(ConstructionRoute.Explicit)]
    [TestCase(ConstructionRoute.Resolve)]
    public void Applies_explicit_init_only_during_creation(ConstructionRoute route) =>
        Scenario.VerifyInit(route);

    [TestCase(ConstructionRoute.Automatic)]
    [TestCase(ConstructionRoute.ByConvention)]
    [TestCase(ConstructionRoute.Explicit)]
    [TestCase(ConstructionRoute.Resolve)]
    public void Evaluates_only_the_selected_member_dependencies(ConstructionRoute route) =>
        Scenario.VerifyBranches(route);

    [TestCase(ConstructionRoute.Automatic)]
    [TestCase(ConstructionRoute.ByConvention)]
    [TestCase(ConstructionRoute.Explicit)]
    [TestCase(ConstructionRoute.Resolve)]
    public void Omits_only_redundant_automatic_assignments(ConstructionRoute route) =>
        Scenario.VerifyAutomaticRule(route);
}
