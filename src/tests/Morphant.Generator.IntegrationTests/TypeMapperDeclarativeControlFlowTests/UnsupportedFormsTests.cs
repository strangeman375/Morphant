namespace Morphant.Generator.IntegrationTests.TypeMapperDeclarativeControlFlowTests;

[TestFixture]
internal sealed class UnsupportedFormsTests
{
    private static IEnumerable<TestCaseData> Cases()
    {
        yield return Case(
            "uninitialized local and assignment",
            global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.UnsupportedForms_b9b3ba81.Scenario.Verify);
        yield return Case(
            "increment",
            global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.UnsupportedForms_3dd67861.Scenario.Verify);
        yield return Case(
            "loop",
            global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.UnsupportedForms_d84180d1.Scenario.Verify);
        yield return Case(
            "standalone side effect",
            global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.UnsupportedForms_ad6b4b67.Scenario.Verify);
        yield return Case(
            "local function",
            global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.UnsupportedForms_22c950b1.Scenario.Verify);
        yield return Case(
            "try catch",
            global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.UnsupportedForms_422c4e40.Scenario.Verify);
        yield return Case(
            "using local",
            global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.UnsupportedForms_2aa973a5.Scenario.Verify);
        yield return Case(
            "lock",
            global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.UnsupportedForms_fd1fb6ef.Scenario.Verify);
        yield return Case(
            "label and goto",
            global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios.UnsupportedForms_ffd70b1f.Scenario.Verify);
    }

    [TestCaseSource(nameof(Cases))]
    public void Keeps_mutation_oriented_statement_form_as_invalid(
        Action verify) =>
        verify();

    private static TestCaseData Case(string name, Action verify) =>
        new TestCaseData(verify).SetName(
            "Keeps_" + name.Replace(' ', '_') + "_as_invalid");
}
