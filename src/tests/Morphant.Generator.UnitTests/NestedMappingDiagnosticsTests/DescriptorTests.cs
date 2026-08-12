using Microsoft.CodeAnalysis;
using Morphant.Generator.TypeMapperGeneration;

namespace Morphant.Generator.UnitTests.NestedMappingDiagnosticsTests;

[TestFixture]
internal sealed class DescriptorTests
{
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
                    "Nested mapping pair cannot be determined",
                    "Nested mapping result is incompatible",
                    "Nested Update destination is invalid"
                }));
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.MessageFormat.ToString()),
                Is.EqualTo(new[]
                {
                    "Nested mapping pair for marker '{0}' in contract '{1}' " +
                    "cannot be determined: {2}. Reachable paths: {3}.",
                    "Nested mapping result type '{0}' in contract '{1}' " +
                    "cannot be converted warning-free to target type '{2}'. " +
                    "Reachable paths: {3}.",
                    "Nested Update destination for marker '{0}' in contract " +
                    "'{1}' is invalid: {2}. Reachable paths: {3}."
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
                Is.All.Empty);
            Assert.That(
                descriptors.SelectMany(static descriptor =>
                    descriptor.CustomTags),
                Is.Empty);
        });
    }
}
