using Microsoft.CodeAnalysis;
using Morphant.Generator.TypeMapperGeneration;

namespace Morphant.Generator.UnitTests.CallbackDiagnosticsTests;

[TestFixture]
internal sealed class DescriptorTests
{
    private const string HelpLinkBase =
        "https://github.com/strangeman375/Morphant/blob/main/docs/diagnostics/";

    [Test]
    public void Exposes_the_exact_callback_diagnostic_contract()
    {
        var descriptors = new[]
        {
            CallbackDiagnosticDescriptors.StructuredCallbackMustBeLambda,
            CallbackDiagnosticDescriptors.CallbackCannotBeTransferred,
            CallbackDiagnosticDescriptors.UnsupportedStructuredSyntax,
            CallbackDiagnosticDescriptors.StructuredInputIsReadOnly,
            CallbackDiagnosticDescriptors.InvalidCompileTimeMarkerUse
        };

        Assert.Multiple(() =>
        {
            Assert.That(
                descriptors.Select(static descriptor => descriptor.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0029",
                    "MORPH0030",
                    "MORPH0031",
                    "MORPH0032",
                    "MORPH0033"
                }));
            Assert.That(
                descriptors.Select(static descriptor => descriptor.Title.ToString()),
                Is.EqualTo(new[]
                {
                    "Mapping expression must be an inline lambda",
                    "Mapping expression is unavailable",
                    "Unsupported mapping expression",
                    "Destination input is read-only",
                    "Invalid declarative API use"
                }));
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.MessageFormat.ToString()),
                Is.EqualTo(new[]
                {
                    "{0} for mapping '{1}' must use an inline lambda.",
                    "{0} for mapping '{1}' cannot be used by mapper '{2}': " +
                    "{3}.",
                    "{0} for mapping '{1}' contains unsupported syntax " +
                    "'{2}'.",
                    "'{0}' is read-only in mapping '{1}'.",
                    "'{0}' cannot be used in this position within {1} for " +
                    "mapping '{2}'."
                }));
            Assert.That(
                descriptors.Select(static descriptor => descriptor.Category),
                Is.All.EqualTo("Morphant.Callbacks"));
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.DefaultSeverity),
                Is.All.EqualTo(DiagnosticSeverity.Error));
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.IsEnabledByDefault),
                Is.All.True);
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.Description.ToString()),
                Is.All.Empty);
            Assert.That(
                descriptors.Select(static descriptor => descriptor.HelpLinkUri),
                Is.EqualTo(descriptors.Select(static descriptor =>
                    HelpLinkBase + descriptor.Id + ".md")));
            Assert.That(
                descriptors.SelectMany(static descriptor =>
                    descriptor.CustomTags),
                Is.Empty);
        });
    }
}
