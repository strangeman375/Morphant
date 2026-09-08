using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.GenericSourceMemberBinding;

namespace Morphant.Generator.IntegrationTests.TypeMapperInheritanceTests;

[TestFixture]
internal sealed class SourceMemberBindingTests
{
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
}
