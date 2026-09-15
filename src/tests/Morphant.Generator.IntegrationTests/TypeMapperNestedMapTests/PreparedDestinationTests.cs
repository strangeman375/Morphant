namespace Morphant.Generator.IntegrationTests.TypeMapperNestedMapTests;

[TestFixture]
internal sealed class PreparedDestinationTests
{
    [Test]
    public void Uses_prepared_children_and_retains_nested_replacements(
        [Values("Object", "Factory", "ResolveFactory", "SystemTuple", "ConditionalTuple")] string kind,
        [Values] bool updateNull, [Values] bool nullChild, [Values] bool replace)
    {
        CSharp9.Scenarios.PreparedNestedDestination.Scenario.Verify(kind, updateNull, nullChild, replace);
    }

    [Test]
    public void Uses_prepared_value_tuple_elements([Values] bool nullChild, [Values] bool replace)
    {
        CSharp9.Scenarios.PreparedNestedDestination.Scenario.Verify("ValueTuple", false, nullChild, replace);
    }

    [Test]
    public void Keeps_explicit_create_independent_of_the_prepared_child([Values] bool updateNull)
    {
        CSharp9.Scenarios.PreparedNestedDestination.Scenario.Verify("ConditionalTuple", updateNull, false, false, true);
    }

    [Test]
    public void Preserves_evaluation_order_around_init_only_members(
        [Values("InitBefore", "InitAfter")] string kind, [Values] bool updateNull)
    {
        CSharp9.Scenarios.PreparedNestedDestination.Scenario.Verify(kind, updateNull, false, false);
    }
}
