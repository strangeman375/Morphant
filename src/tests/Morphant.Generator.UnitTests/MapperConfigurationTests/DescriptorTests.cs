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
            "Mapper '{0}' must declare a source-bodied override of " +
            "'Configure(Morphant.MapperBuilder)'.");
        yield return new DescriptorCase(
            ConfigurationFlowDiagnosticDescriptors.UnavailableBaseConfigure,
            "MORPH0016",
            "Base mapper configuration is unavailable",
            "The Configure body for base mapper '{0}' is unavailable while " +
            "analyzing mapper '{1}'.");
        yield return new DescriptorCase(
            ConfigurationFlowDiagnosticDescriptors.UnsupportedMapperFlow,
            "MORPH0017",
            "Unsupported mapper builder flow",
            "Mapper builder flow in Configure of mapper '{0}' cannot be " +
            "analyzed by Morphant.");
        yield return new DescriptorCase(
            ConfigurationFlowDiagnosticDescriptors.UnsupportedMappingFlow,
            "MORPH0018",
            "Unsupported mapping builder flow",
            "Mapping builder flow for contract '{0}' in mapper '{1}' " +
            "cannot be analyzed by Morphant.");
    }

    internal sealed record DescriptorCase(
        DiagnosticDescriptor Descriptor,
        string Id,
        string Title,
        string Message);
}
