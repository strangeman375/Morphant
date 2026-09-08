using Scenario = Morphant.Generator.IntegrationTests.CSharp9.Scenarios.PolymorphismGenericVariance.Scenario;

namespace Morphant.Generator.IntegrationTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class GenericVarianceTests
{
    [TestCase(false, false, false)]
    [TestCase(false, false, true)]
    [TestCase(false, true, false)]
    [TestCase(false, true, true)]
    [TestCase(true, false, false)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    [TestCase(true, true, true)]
    public void Rejects_suppressed_unknown_branch_relationships_and_keeps_independent_pairs(
        bool specificFirst, bool application, bool update) => Scenario.Verify(specificFirst, application, update);

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void Preserves_known_generic_inheritance_closed_variance_and_interface_ambiguity(bool application, bool update) =>
        CSharp9.Scenarios.KnownGenericPolymorphism.Scenario.Verify(application, update);
}
