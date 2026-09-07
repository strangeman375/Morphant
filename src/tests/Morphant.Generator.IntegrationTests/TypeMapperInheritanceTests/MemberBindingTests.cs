using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InheritedCallbackBinding_s0502;

namespace Morphant.Generator.IntegrationTests.TypeMapperInheritanceTests;

[TestFixture]
internal sealed class MemberBindingTests
{
    [Test, Combinatorial]
    public void Preserves_the_declaring_mapper_binding(
        [Values] CallbackKind callback, [Values] bool explicitThis) =>
        Scenario.VerifyFamily(callback, explicitThis);

    [Test]
    public void Preserves_fields_properties_overloads_and_virtual_slots() => Scenario.VerifyOtherMembers();

    [Test]
    public void Preserves_runtime_method_groups_and_delegate_getters() => Scenario.VerifyMethodGroups();

    [Test]
    public void Preserves_binding_inside_deferred_callbacks() => Scenario.VerifyDeferredCapture();

    [Test]
    public void Preserves_overload_selection_after_covariant_overrides() => Scenario.VerifyCovariantResultTypes();

    [Test]
    public void Preserves_the_static_type_of_standalone_this() => Scenario.VerifyThisType();

    [Test]
    public void Preserves_inferred_generic_method_selection_when_closing_a_family() => Scenario.VerifyInferredGeneric();
}
