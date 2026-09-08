using Values = Morphant.Generator.IntegrationTests.CSharp11.Scenarios.ValueTypeConstruction.Scenario;
using Boxed = Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InterfaceDestination.Scenario;
using Defaults = Morphant.Generator.IntegrationTests.CSharp9.Scenarios.OptionalConstructorDefaults.Scenario;

namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class TypeMapperValueConstructionTests
{
    [TestCase("Create")]
    [TestCase("CreateNull")]
    [TestCase("UpdateDefault")]
    [TestCase("UpdateNullableNull")]
    public void Invokes_an_explicit_struct_constructor_only_when_constructing(string operation) =>
        Values.VerifyExplicitConstructor(operation);

    [TestCase(false)]
    [TestCase(true)]
    public void Preserves_readonly_record_struct_construction_and_update(bool update) =>
        Values.VerifyReadonlyRecord(update);

    [TestCase("Create")]
    [TestCase("UpdateNull")]
    [TestCase("UpdateExisting")]
    public void Maps_a_boxed_struct_through_its_interface(string operation) => Boxed.Verify(operation);

    [TestCase(false)]
    [TestCase(true)]
    public void Preserves_decimal_enum_and_params_constructor_defaults(bool update) => Defaults.Verify(update);
}
