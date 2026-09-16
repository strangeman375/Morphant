using Morphant.Generator.IntegrationTests.Latest.Scenarios.NamespaceStyle;

namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class NamespaceStyleTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void Preserves_query_binding_and_multiline_string_values(bool update, bool existing)
    {
        var (result, reused) = Scenario.Run(update, existing);
        var expected = Scenario.ExpectedStrings();
        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Is.EqualTo(existing ? "existing" : "2,3"));
            Assert.That(result.Verbatim, Is.EqualTo(expected.Verbatim));
            Assert.That(result.Interpolated, Is.EqualTo(expected.Interpolated));
            Assert.That(result.Raw, Is.EqualTo(expected.Raw));
            Assert.That(reused, Is.EqualTo(existing));
        });
    }
}
