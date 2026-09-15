using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.SupportingLocals;

namespace Morphant.Generator.IntegrationTests.TypeMapperEvaluationTests;

[TestFixture]
internal sealed class SupportingLocalTests
{
    [Test]
    public void Preserves_user_locals_captured_storage_conversions_and_boxing(
        [Values("Stable", "Captured", "Converted", "Boxed")] string form,
        [Values(false, true)] bool resolve,
        [Values(0, 1, 2)] int operation,
        [Values(false, true)] bool throwWrite) => Scenario.Verify(form, resolve, operation, throwWrite);
}
