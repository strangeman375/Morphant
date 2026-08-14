using Microsoft.CodeAnalysis;
using Morphant.Generator.TypeMapperGeneration;

namespace Morphant.Generator.UnitTests.NestedMappingDiagnosticsTests;

[TestFixture]
internal sealed class DescriptorTests
{
    private const string HelpLinkBase =
        "https://github.com/strangeman375/Morphant/blob/main/docs/diagnostics/";

    [Test]
    public void Exposes_the_exact_nested_mapping_diagnostic_contract()
    {
        var descriptors = new[]
        {
            NestedMappingDiagnosticDescriptors.PairUnknown,
            NestedMappingDiagnosticDescriptors.ResultIncompatible,
            NestedMappingDiagnosticDescriptors.UpdateDestinationInvalid
        };

        Assert.Multiple(() =>
        {
            Assert.That(
                descriptors.Select(static descriptor => descriptor.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0044",
                    "MORPH0045",
                    "MORPH0046"
                }));
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.Title.ToString()),
                Is.EqualTo(new[]
                {
                    "Nested mapping types cannot be determined",
                    "Nested mapping result is incompatible",
                    "Nested Update destination is invalid"
                }));
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.MessageFormat.ToString()),
                Is.EqualTo(new[]
                {
                    "Cannot determine source or destination type for '{0}' " +
                    "in mapping '{1}': {2}. Affected cases: {3}.",
                    "Nested mapping result type '{0}' cannot be assigned to " +
                    "'{2}' in mapping '{1}'. Affected cases: {3}.",
                    "Destination for nested '{0}' is invalid in mapping " +
                    "'{1}': {2}. Affected cases: {3}."
                }));
            Assert.That(
                descriptors.Select(static descriptor => descriptor.Category),
                Is.All.EqualTo("Morphant.NestedMapping"));
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
                descriptors.Select(static descriptor =>
                    descriptor.HelpLinkUri),
                Is.EqualTo(descriptors.Select(static descriptor =>
                    HelpLinkBase + descriptor.Id + ".md")));
            Assert.That(
                descriptors.SelectMany(static descriptor =>
                    descriptor.CustomTags),
                Is.Empty);
        });
    }
}
