using Microsoft.CodeAnalysis;
using Morphant.Generator.TypeMapperGeneration;

namespace Morphant.Generator.UnitTests.CallbackDiagnosticsTests;

[TestFixture]
internal sealed class DescriptorTests
{
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
                    "Structured callback must be a lambda",
                    "Callback cannot be transferred",
                    "Unsupported structured callback syntax",
                    "Structured destination input is read-only",
                    "Invalid compile-time marker use"
                }));
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.MessageFormat.ToString()),
                Is.EqualTo(new[]
                {
                    "Structured {0} callback for contract '{1}' must be " +
                    "an inline lambda.",
                    "{0} callback for contract '{1}' cannot be transferred " +
                    "to generated mapper '{2}': {3}.",
                    "Structured {0} callback for contract '{1}' contains " +
                    "unsupported syntax '{2}'.",
                    "Structured destination input '{0}' for contract '{1}' " +
                    "is read-only and cannot be mutated.",
                    "Compile-time marker '{0}' cannot be used as a runtime " +
                    "value or outside a supported terminal DSL position in " +
                    "{1} callback for contract '{2}'."
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
                Is.All.Empty);
            Assert.That(
                descriptors.SelectMany(static descriptor =>
                    descriptor.CustomTags),
                Is.Empty);
        });
    }
}
