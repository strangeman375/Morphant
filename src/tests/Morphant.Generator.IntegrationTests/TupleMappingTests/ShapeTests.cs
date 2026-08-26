namespace Morphant.Generator.IntegrationTests.TupleMappingTests;

[TestFixture]
internal sealed class ShapeTests
{
    [Test]
    public void Supports_bcl_shapes_nullable_roots_and_static_ituple_boundaries()
    {
        global::Morphant.Generator.IntegrationTests.CSharp9.Scenarios
            .TupleShapes_a7b10004.Scenario.Verify();
    }
}
