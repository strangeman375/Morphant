using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.StrictRuntimeTypes;

namespace Morphant.Generator.IntegrationTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class ExactTypesTests
{
    [Test]
    public void Preserves_exact_source_and_null_policies(
        [Values(false, true)] bool reference,
        [Values(false, true)] bool nullSource,
        [Values("Create", "UpdateNull", "Reuse")] string operation) => Scenario.VerifyExact(reference, nullSource, operation);

    [Test]
    public void Retains_covariant_array_dispatch(
        [Values("Exact", "Derived", "Null")] string kind,
        [Values(false, true)] bool update) => Scenario.VerifyArray(kind, update);
}
