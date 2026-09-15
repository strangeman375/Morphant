using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.SupportingValues;

namespace Morphant.Generator.IntegrationTests.TypeMapperEvaluationTests;

[TestFixture]
internal sealed class SupportingValuesTests
{
    [Test]
    public void Preserves_automatic_value_conversions_and_constructor_effects(
        [Values(false, true)] bool resolve,
        [Values("Create", "UpdateNull", "Reuse", "Replace")] string operation,
        [Values(false, true)] bool throwMember) => Scenario.VerifyAutomatic(resolve, operation, throwMember);

    [Test]
    public void Preserves_tuple_getter_order_and_ignored_input_replacement(
        [Values(false, true)] bool resolve,
        [Values(false, true)] bool reference,
        [Values("Create", "Reuse", "Replace")] string operation,
        [Values(false, true)] bool throwMember) => Scenario.VerifyTuple(resolve, reference, operation, throwMember);
}
