using Microsoft.CodeAnalysis;
using Morphant.Generator.Diagnostics;

namespace Morphant.Generator.UnitTests.GeneratorFailureDiagnosticsTests;

[TestFixture]
internal sealed class DescriptorTests
{
    [Test]
    public void Unexpected_failure_descriptor_matches_the_public_contract()
    {
        var descriptor =
            GeneratorFailureDiagnosticDescriptors.UnexpectedFailure;

        Assert.Multiple(() =>
        {
            Assert.That(descriptor.Id, Is.EqualTo("MORPH0057"));
            Assert.That(
                descriptor.Title.ToString(),
                Is.EqualTo("Morphant generator failed unexpectedly"));
            Assert.That(
                descriptor.MessageFormat.ToString(),
                Is.EqualTo(
                    "Morphant generator {0} failed unexpectedly in stage " +
                    "'{1}': {2}: {3}. Full exception details are " +
                    "available in generated file '{4}'."));
            Assert.That(
                descriptor.Category,
                Is.EqualTo("Morphant.Generator"));
            Assert.That(
                descriptor.DefaultSeverity,
                Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(descriptor.IsEnabledByDefault, Is.True);
            Assert.That(descriptor.Description.ToString(), Is.Empty);
            Assert.That(
                descriptor.HelpLinkUri,
                Is.EqualTo(
                    "https://github.com/strangeman375/Morphant/blob/main/" +
                    "docs/diagnostics/MORPH0057.md"));
            Assert.That(descriptor.CustomTags, Is.Empty);
            Assert.That(
                descriptor.CustomTags,
                Does.Not.Contain(WellKnownDiagnosticTags.NotConfigurable));
        });
    }
}
