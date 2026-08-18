using Microsoft.CodeAnalysis;
using Morphant.Generator.PairConfiguration;

namespace Morphant.Generator.UnitTests.MappingCompositionTests;

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
            Assert.That(descriptor.Category,
                Is.EqualTo("Morphant.Composition"));
            Assert.That(descriptor.DefaultSeverity,
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
            MappingCompositionDiagnosticDescriptors.DuplicatePlanSlot,
            "MORPH0019",
            "Mapping part is configured more than once",
            "'{0}' is configured more than once for mapping '{1}' in " +
            "mapper '{2}'.");
        yield return new DescriptorCase(
            MappingCompositionDiagnosticDescriptors
                .MixedConvertAndDeclarative,
            "MORPH0020",
            "Convert cannot be combined with other mapping rules",
            "Convert cannot be combined with Construct, Resolve, Members, " +
            "or IncludeMembers " +
            "for mapping '{0}' in mapper '{1}'.");
    }

    internal sealed record DescriptorCase(
        DiagnosticDescriptor Descriptor,
        string Id,
        string Title,
        string Message);
}
