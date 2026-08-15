using Microsoft.CodeAnalysis;
using Morphant.Generator.Settings;

namespace Morphant.Generator.UnitTests.MappingSettingsDiagnosticsTests;

[TestFixture]
internal sealed class DescriptorTests
{
    private const string HelpLinkBase =
        "https://github.com/strangeman375/Morphant/blob/main/docs/diagnostics/";

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
            Assert.That(descriptor.Category, Is.EqualTo("Morphant.Settings"));
            Assert.That(
                descriptor.DefaultSeverity,
                Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(descriptor.IsEnabledByDefault, Is.True);
            Assert.That(descriptor.Description.ToString(), Is.Empty);
            Assert.That(
                descriptor.HelpLinkUri,
                Is.EqualTo(HelpLinkBase + value.Id + ".md"));
            Assert.That(descriptor.CustomTags, Is.Empty);
            Assert.That(
                descriptor.CustomTags,
                Does.Not.Contain(WellKnownDiagnosticTags.NotConfigurable));
        });
    }

    private static IEnumerable<DescriptorCase> Cases()
    {
        yield return new DescriptorCase(
            MappingSettingsDiagnosticDescriptors.InvalidSettingValue,
            "MORPH0021",
            "Invalid mapping setting value",
            "Setting '{0}' must be a supported compile-time constant.");
        yield return new DescriptorCase(
            MappingSettingsDiagnosticDescriptors.InvalidMsBuildSettingValue,
            "MORPH0022",
            "Invalid MSBuild mapping setting value",
            "MSBuild property '{0}' has unsupported value '{1}'.");
        yield return new DescriptorCase(
            MappingSettingsDiagnosticDescriptors.InapplicableSetting,
            "MORPH0023",
            "Mapping setting is not applicable",
            "Setting '{0}' does not apply to {1} for mapping '{2}' in " +
            "mapper '{3}'.");
    }

    internal sealed record DescriptorCase(
        DiagnosticDescriptor Descriptor,
        string Id,
        string Title,
        string Message);
}
