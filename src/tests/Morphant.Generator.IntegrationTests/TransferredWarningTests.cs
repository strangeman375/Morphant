namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class TransferredWarningTests
{
    [Test]
    public void Preserves_verbatim_interpolated_strings(
        [Values("Construct", "Resolve", "Members", "ConstructUsing", "ResolveUsing", "Convert")] string callback,
        [Values("Create", "Reuse", "Replace", "NullUpdate")] string operation) =>
        CSharp9.Scenarios.TransferredWarnings.Scenario.Verify(callback, operation);

    [TestCase(false)]
    [TestCase(true)]
    public void Preserves_raw_interpolated_strings(bool update) =>
        CSharp11.Scenarios.TransferredWarnings.Scenario.Verify(update);
}
