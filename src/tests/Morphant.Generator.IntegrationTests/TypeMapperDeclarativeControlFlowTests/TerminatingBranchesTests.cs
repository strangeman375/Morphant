using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TerminatingBranches;

namespace Morphant.Generator.IntegrationTests.TypeMapperDeclarativeControlFlowTests;

[TestFixture]
internal sealed class TerminatingBranchesTests
{
    [TestCase(Route.Construct, -1, "throw:fallback", "member,set")]
    [TestCase(Route.Construct, 0, "constructor,construct,member,set", "member,set")]
    [TestCase(Route.Construct, 1, "constructor,construct,member,set", "member,set")]
    [TestCase(Route.Resolve, -1, "throw:fallback", "throw:fallback")]
    [TestCase(Route.Resolve, 0, "constructor,construct,member,set", "member,set")]
    [TestCase(Route.Resolve, 1, "constructor,construct,member,set", "constructor,construct,member,set")]
    [TestCase(Route.Members, -1, "constructor,construct,throw:fallback", "throw:fallback")]
    [TestCase(Route.Members, 0, "constructor,construct,member,set", "member,set")]
    [TestCase(Route.Members, 1, "constructor,construct,nested,set", "nested,set")]
    [TestCase(Route.Members, 2, "constructor,construct,throw:inner", "throw:inner")]
    public void Evaluates_only_the_selected_path_on_create_and_update(
        Route route, int mode, string createEvents, string updateEvents) =>
        Scenario.Verify(route, mode, createEvents, updateEvents);
}
