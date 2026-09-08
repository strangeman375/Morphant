using Generic = Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TupleGenericPresentation.Scenario;
using Composition = Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TupleCompositionState.Scenario;

namespace Morphant.Generator.IntegrationTests.TupleMappingTests;

[TestFixture]
internal sealed class GenericAndStateTests
{
    [TestCase("Create", false)]
    [TestCase("Create", true)]
    [TestCase("UpdateNull", false)]
    [TestCase("UpdateNull", true)]
    [TestCase("UpdateExisting", false)]
    [TestCase("UpdateExisting", true)]
    public void Preserves_nested_nullable_and_dynamic_elements_through_IncludeBase(string operation, bool nullElement) =>
        Generic.Verify(operation, nullElement);

    [TestCase(false)]
    [TestCase(true)]
    public void Combines_multiple_inputs_outputs_and_explicit_user_state(bool update) => Composition.Verify(update);
}
