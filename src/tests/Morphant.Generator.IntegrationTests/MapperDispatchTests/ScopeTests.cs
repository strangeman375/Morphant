namespace Morphant.Generator.IntegrationTests.MapperDispatchTests;

[TestFixture]
internal sealed class ScopeTests
{
    [Test]
    public void Keeps_nested_calls_in_scope_and_completes_it_after_success_or_failure()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ApplicationScope_9d7a0103.Scenario.Verify();
    }

    [Test]
    public async Task Creates_an_independent_scope_for_each_parallel_root_call()
    {
        await global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ApplicationParallelScope_9d7a0104.Scenario.Verify();
    }
}
