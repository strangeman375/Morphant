using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConstructorResultDependencies;

namespace Morphant.Generator.IntegrationTests.TypeMapperConstructorSelectionTests;

[TestFixture]
internal sealed class ResultDependencyTests
{
    [TestCase(Route.Unguarded)]
    [TestCase(Route.Explicit)]
    [TestCase(Route.Operation)]
    [TestCase(Route.Previous)]
    [TestCase(Route.Local)]
    [TestCase(Route.Deferred)]
    [TestCase(Route.Numeric)]
    [TestCase(Route.Resolve)]
    [TestCase(Route.PreviousValue)]
    [TestCase(Route.Factory)]
    [TestCase(Route.ResolveFactory)]
    [TestCase(Route.Unrelated)]
    public void Reads_the_initial_result_after_independent_construction(Route route)
    {
        Scenario.Verify(route, update: false, hasPrevious: false);
        Scenario.Verify(route, update: true, hasPrevious: false);
        Scenario.Verify(route, update: true, hasPrevious: true);
    }

    [TestCase(Route.Resolve)]
    [TestCase(Route.PreviousValue)]
    [TestCase(Route.ResolveFactory)]
    public void Distinguishes_replacement_from_reuse(Route route) =>
        Scenario.Verify(route, update: true, hasPrevious: true, replace: true);
}
