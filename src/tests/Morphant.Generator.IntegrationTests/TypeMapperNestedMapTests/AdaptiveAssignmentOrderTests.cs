using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.AdaptiveAssignmentOrder;

namespace Morphant.Generator.IntegrationTests.TypeMapperNestedMapTests;

[TestFixture]
internal sealed class AdaptiveAssignmentOrderTests
{
    [Test]
    public void Preserves_setter_effects_and_explicit_result_reads(
        [Values("Construct", "Resolve", "ConstructUsing", "ResolveUsing")] string form,
        [Values("Create", "UpdateNull", "Reuse", "Replace")] string operation,
        [Values("Compatible", "Null", "Incompatible")] string mode,
        [Values(false, true)] bool readsResult,
        [Values(false, true)] bool throwSource)
    {
        Scenario.Verify(form, operation, mode, readsResult, throwSource);
    }
}
