namespace Morphant.Generator.IntegrationTests.TypeMapperConstructorSelectionTests;

[TestFixture]
internal sealed class GreediestAndLargestTests
{
    [Test]
    public void Greediest_selects_the_unique_plan_with_most_emitted_arguments()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.GreediestAndLargest_acf29cda.Scenario.Verify();
    }

    [Test]
    public void Greediest_requires_an_explicit_choice_when_best_scores_tie()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.GreediestAndLargest_d960be8d.Scenario.Verify();
    }

    [Test]
    public void Greediest_excludes_nullable_warning_and_required_member_failures()
    {
        global::Morphant.Generator.IntegrationTests.CSharp11.Scenarios.GreediestAndLargest_22a7d3d6.Scenario.Verify();
    }

    [Test]
    public void Largest_uses_declared_size_and_never_falls_back()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.GreediestAndLargest_2a460e2b.Scenario.Verify();
    }
}
