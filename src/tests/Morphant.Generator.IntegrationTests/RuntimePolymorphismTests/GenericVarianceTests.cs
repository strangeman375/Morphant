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
    public void Selects_the_most_specific_branch_after_closing_generic_arguments(
        bool specificFirst, bool application, bool update) => Scenario.Verify(specificFirst, application, update);
}
