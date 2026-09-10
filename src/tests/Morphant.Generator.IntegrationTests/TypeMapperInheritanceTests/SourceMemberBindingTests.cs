using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.GenericSourceMemberBinding;

namespace Morphant.Generator.IntegrationTests.TypeMapperInheritanceTests;

[TestFixture]
internal sealed class SourceMemberBindingTests
{
    [Test]
    public void Conditional_access_preserves_hidden_members_and_evaluates_each_getter_once(
        [Values(false, true)] bool hasProfile,
        [Values(Operation.Create, Operation.UpdateWithoutDestination, Operation.UpdateExisting)] Operation operation) =>
        BoundaryScenario.VerifyConditionalMember(hasProfile, operation);

    [TestCase(Callback.Members, Operation.Create)]
    [TestCase(Callback.Members, Operation.UpdateWithoutDestination)]
    [TestCase(Callback.Members, Operation.UpdateExisting)]
    [TestCase(Callback.IncludeMembers, Operation.Create)]
    [TestCase(Callback.IncludeMembers, Operation.UpdateWithoutDestination)]
    [TestCase(Callback.IncludeMembers, Operation.UpdateExisting)]
    public void Preserves_the_source_member_selected_by_the_generic_constraint(Callback callback, Operation operation) =>
        Scenario.Verify(callback, operation);

    [Test]
    public void Preserves_generic_CSharp_and_cross_pair_binding() => Scenario.VerifyOriginalBinding();

    [TestCase(Callback.Members, Operation.Create)]
    [TestCase(Callback.Members, Operation.UpdateWithoutDestination)]
    [TestCase(Callback.Members, Operation.UpdateExisting)]
    [TestCase(Callback.IncludeMembers, Operation.Create)]
    [TestCase(Callback.IncludeMembers, Operation.UpdateWithoutDestination)]
    [TestCase(Callback.IncludeMembers, Operation.UpdateExisting)]
    public void Preserves_hidden_fields_and_virtual_dispatch(Callback callback, Operation operation) =>
        BoundaryScenario.VerifyFieldsAndVirtualDispatch(callback, operation);

    [TestCase(Callback.Members, false, false, false)]
    [TestCase(Callback.Members, true, false, false)]
    [TestCase(Callback.Members, true, true, false)]
    [TestCase(Callback.Members, false, false, true)]
    [TestCase(Callback.Members, true, false, true)]
    [TestCase(Callback.Members, true, true, true)]
    [TestCase(Callback.IncludeMembers, false, false, false)]
    [TestCase(Callback.IncludeMembers, true, false, false)]
    [TestCase(Callback.IncludeMembers, true, true, false)]
    [TestCase(Callback.IncludeMembers, false, false, true)]
    [TestCase(Callback.IncludeMembers, true, false, true)]
    [TestCase(Callback.IncludeMembers, true, true, true)]
    public void Preserves_null_paths_and_evaluates_each_getter_once(
        Callback callback, bool hasPayload, bool hasProfile, bool suppressNull) =>
        BoundaryScenario.VerifyNullablePath(callback, hasPayload, hasProfile, suppressNull);

    [TestCase(Callback.Members, false)]
    [TestCase(Callback.Members, true)]
    [TestCase(Callback.IncludeMembers, false)]
    [TestCase(Callback.IncludeMembers, true)]
    public void Preserves_interface_dispatch_and_mutable_struct_receivers(Callback callback, bool valueType) =>
        BoundaryScenario.VerifyInterfaceDispatch(callback, valueType);
}
