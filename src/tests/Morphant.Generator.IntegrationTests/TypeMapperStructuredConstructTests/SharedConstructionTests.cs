using Scenario = Morphant.Generator.IntegrationTests.CSharp9.Scenarios.SharedConstruction.Scenario;

namespace Morphant.Generator.IntegrationTests.TypeMapperStructuredConstructTests;

[TestFixture]
internal sealed class SharedConstructionTests
{
    [Test]
    public void Shared_fallback_preserves_out_values_and_short_circuit_order(
        [Values(0, 1, 2)] int operation,
        [Values] bool before,
        [Values] bool after,
        [Values] bool last) =>
        Scenario.VerifyOutGuard(operation, before, after, last);

    [Test]
    public void Shared_fallback_preserves_branch_locals_and_terminating_reuse(
        [Values(0, 1, 2)] int operation,
        [Values] bool before,
        [Values] bool after,
        [Values(-1, 42)] int id) =>
        Scenario.VerifyScopedGuard(operation, before, after, id);

    [TestCase(false, 0, false)]
    [TestCase(false, 1, false)]
    [TestCase(false, 2, false)]
    [TestCase(false, 2, true)]
    [TestCase(true, 0, false)]
    [TestCase(true, 1, false)]
    [TestCase(true, 2, false)]
    [TestCase(true, 2, true)]
    public void Calls_the_mapper_method_when_a_user_delegate_has_the_same_name(bool construct, int operation, bool reuse) =>
        Scenario.VerifyShadowing(construct, operation, reuse);

    [TestCase(0, false, false, 0)]
    [TestCase(0, false, true, 0)]
    [TestCase(0, true, false, 0)]
    [TestCase(0, true, true, 0)]
    [TestCase(1, false, false, 0)]
    [TestCase(1, false, true, 0)]
    [TestCase(1, true, false, 0)]
    [TestCase(1, true, true, 0)]
    [TestCase(2, false, false, 0)]
    [TestCase(2, false, true, 0)]
    [TestCase(2, true, false, 0)]
    [TestCase(2, true, true, 0)]
    [TestCase(0, true, true, 3)]
    [TestCase(1, true, true, 3)]
    [TestCase(2, true, false, 3)]
    [TestCase(2, true, true, 3)]
    public void Preserves_selection_argument_constructor_and_initializer_order(int operation, bool before, bool after, int throwAt) =>
        Scenario.VerifyOrder(operation, before, after, throwAt);

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(2, true)]
    public void Preserves_captured_variable_storage(int operation, bool reuse) =>
        Scenario.VerifyCapturedStorage(operation, reuse);

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(2, true)]
    public void Preserves_previous_dependent_locals(int operation, bool reuse) =>
        Scenario.VerifyPrevious(operation, reuse);

    [TestCase(0, null)]
    [TestCase(1, null)]
    [TestCase(2, null)]
    [TestCase(0, "mapped")]
    [TestCase(1, "mapped")]
    [TestCase(2, "mapped")]
    [TestCase(2, "previous")]
    public void Preserves_nullable_guards_and_narrowed_local_values(int operation, string? name) =>
        Scenario.VerifyNullable(operation, name);
}
