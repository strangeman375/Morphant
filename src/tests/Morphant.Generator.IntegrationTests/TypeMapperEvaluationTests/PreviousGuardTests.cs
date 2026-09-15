using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.PreviousGuard;

namespace Morphant.Generator.IntegrationTests.TypeMapperEvaluationTests;

[TestFixture]
internal sealed class PreviousGuardTests
{
    [Test]
    public void Preserves_out_bindings_short_circuiting_and_exceptions(
        [Values("And", "Negated", "Existing", "Stored", "MembersConstruct", "MembersResolve")] string form,
        [Values("Create", "UpdateNull", "UpdateExisting")] string operation,
        [Values(false, true)] bool reuse,
        [Values(false, true)] bool throwProbe) => Scenario.Verify(form, operation, reuse, throwProbe, gate: true);

    [Test]
    public void Keeps_conditional_and_user_defined_TryGetValue_evaluations(
        [Values("Conditional", "Arbitrary")] string form,
        [Values("Create", "UpdateNull", "UpdateExisting")] string operation,
        [Values(false, true)] bool reuse,
        [Values(false, true)] bool throwProbe,
        [Values(false, true)] bool gate) => Scenario.Verify(form, operation, reuse, throwProbe, gate);
}
