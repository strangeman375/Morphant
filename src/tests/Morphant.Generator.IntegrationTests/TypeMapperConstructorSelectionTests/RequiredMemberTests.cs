using Morphant.Generator.IntegrationTests.CSharp11.Scenarios.RequiredConstructorMembers_s0501;

namespace Morphant.Generator.IntegrationTests.TypeMapperConstructorSelectionTests;

[TestFixture]
internal sealed class RequiredMemberTests
{
    [TestCase(ConstructionRoute.Automatic)]
    [TestCase(ConstructionRoute.ByConvention)]
    [TestCase(ConstructionRoute.Explicit)]
    public void Keeps_explicit_required_init(ConstructionRoute route) => Scenario.VerifyRequiredInit(route);

    [TestCase(ConstructionRoute.Automatic)]
    [TestCase(ConstructionRoute.ByConvention)]
    [TestCase(ConstructionRoute.Explicit)]
    public void Keeps_explicit_required_setter(ConstructionRoute route) => Scenario.VerifyRequiredSet(route);

    [TestCase(ConstructionRoute.Automatic)]
    [TestCase(ConstructionRoute.ByConvention)]
    [TestCase(ConstructionRoute.Explicit)]
    public void Keeps_explicit_init_after_SetsRequiredMembers(ConstructionRoute route) => Scenario.VerifyAttributedInit(route);

    [TestCase(ConstructionRoute.Automatic)]
    [TestCase(ConstructionRoute.ByConvention)]
    [TestCase(ConstructionRoute.Explicit)]
    public void Keeps_explicit_setter_after_SetsRequiredMembers(ConstructionRoute route) => Scenario.VerifyAttributedSet(route);

    [Test]
    public void Preserves_automatic_required_member_evaluation() => Scenario.VerifyAutomaticControls();
}
