using Microsoft.CodeAnalysis;
using Morphant.Generator.PairConfiguration;

namespace Morphant.Generator.UnitTests.MapperConfigurationTests;

[TestFixture]
internal sealed class DescriptorTests
{
    [TestCaseSource(nameof(Cases))]
    public void Descriptor_matches_the_public_contract(DescriptorCase value)
    {
        var descriptor = value.Descriptor;

        Assert.Multiple(() =>
        {
            Assert.That(descriptor.Id, Is.EqualTo(value.Id));
            Assert.That(descriptor.Title.ToString(), Is.EqualTo(value.Title));
            Assert.That(
                descriptor.MessageFormat.ToString(),
                Is.EqualTo(value.Message));
            Assert.That(descriptor.Category,
                Is.EqualTo("Morphant.Configuration"));
            Assert.That(descriptor.DefaultSeverity,
                Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(descriptor.IsEnabledByDefault, Is.True);
            Assert.That(descriptor.Description.ToString(), Is.Empty);
            Assert.That(descriptor.HelpLinkUri, Is.Empty);
            Assert.That(descriptor.CustomTags, Is.Empty);
            Assert.That(
                descriptor.CustomTags,
                Does.Not.Contain(WellKnownDiagnosticTags.NotConfigurable));
        });
    }

    private static IEnumerable<DescriptorCase> Cases()
    {
        yield return new DescriptorCase(
            ConfigurationFlowDiagnosticDescriptors.MissingConfigure,
            "MORPH0015",
            "Mapper must declare Configure",
            "Mapper '{0}' must override 'Configure(Morphant.MapperBuilder)' " +
            "with a readable method body.");
        yield return new DescriptorCase(
            ConfigurationFlowDiagnosticDescriptors.UnavailableBaseConfigure,
            "MORPH0016",
            "Base mapper configuration is unavailable",
            "Morphant cannot read Configure for base mapper '{0}' while " +
            "analyzing mapper '{1}'.");
        yield return new DescriptorCase(
            ConfigurationFlowDiagnosticDescriptors.UnsupportedMapperFlow,
            "MORPH0017",
            "Configure cannot be analyzed",
            "Morphant cannot analyze Configure in mapper '{0}'.");
        yield return new DescriptorCase(
            ConfigurationFlowDiagnosticDescriptors.UnsupportedMappingFlow,
            "MORPH0018",
            "Mapping configuration cannot be analyzed",
            "Morphant cannot analyze configuration for mapping '{0}' in " +
            "mapper '{1}'.");
    }

    internal sealed record DescriptorCase(
        DiagnosticDescriptor Descriptor,
        string Id,
        string Title,
        string Message);
}
