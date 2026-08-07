namespace Morphant.Generator.IntegrationTests.TypeMapperConstructorSelectionTests;

[TestFixture]
internal sealed class StrategyTests
{
    [Test]
    public void Parameterless_selects_only_the_parameterless_constructor()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Strategy_f5bbc7e4.Scenario.Verify();
    }

    [Test]
    public void Single_counts_only_accessible_supported_constructors()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Strategy_82b063f0.Scenario.Verify();
    }

    [Test]
    public void Unambiguous_prefers_one_parameterized_constructor_without_fallback()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Strategy_5ce5bec4.Scenario.Verify();
    }

    [Test]
    public void Explicit_disables_automatic_construction_only()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Strategy_77765882.Scenario.Verify();
    }
}
