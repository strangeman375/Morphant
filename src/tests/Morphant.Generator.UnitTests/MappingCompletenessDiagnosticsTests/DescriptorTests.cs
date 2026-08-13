using Microsoft.CodeAnalysis;
using Morphant.Generator.TypeMapperGeneration;

namespace Morphant.Generator.UnitTests.MappingCompletenessDiagnosticsTests;

[TestFixture]
internal sealed class DescriptorTests
{
    [Test]
    public void Exposes_the_exact_mapping_completeness_contract()
    {
        var descriptors = new[]
        {
            MappingCompletenessDiagnosticDescriptors.SourceMemberUnused,
            MappingCompletenessDiagnosticDescriptors.DestinationMemberUnmapped
        };

        Assert.Multiple(() =>
        {
            Assert.That(
                descriptors.Select(static descriptor => descriptor.Id),
                Is.EqualTo(new[] { "MORPH0047", "MORPH0048" }));
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.Title.ToString()),
                Is.EqualTo(new[]
                {
                    "Source member is not used",
                    "Destination member is not mapped"
                }));
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.MessageFormat.ToString()),
                Is.EqualTo(new[]
                {
                    "Source member '{0}' is not used by mapping '{1}'.",
                    "Destination member '{0}' is not mapped by mapping " +
                    "'{1}'."
                }));
            Assert.That(
                descriptors.Select(static descriptor => descriptor.Category),
                Is.All.EqualTo("Morphant.MappingCompleteness"));
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.DefaultSeverity),
                Is.All.EqualTo(DiagnosticSeverity.Warning));
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
