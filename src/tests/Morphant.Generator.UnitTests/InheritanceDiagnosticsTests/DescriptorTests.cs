using Microsoft.CodeAnalysis;
using Morphant.Generator.PairConfiguration;

namespace Morphant.Generator.UnitTests.InheritanceDiagnosticsTests;

[TestFixture]
internal sealed class DescriptorTests
{
    private const string HelpLinkBase =
        "https://github.com/strangeman375/Morphant/blob/main/docs/diagnostics/";

    [Test]
    public void Defines_the_complete_inheritance_descriptor_contract()
    {
        var descriptors = new[]
        {
            InheritanceDiagnosticDescriptors.DuplicateBaseConfiguration,
            InheritanceDiagnosticDescriptors.DuplicateIncludeBase,
            InheritanceDiagnosticDescriptors.IncludedPairNotFound,
            InheritanceDiagnosticDescriptors.IncompatibleIncludedType,
            InheritanceDiagnosticDescriptors.InaccessibleInheritedCallback
        };

        Assert.Multiple(() =>
        {
            Assert.That(
                descriptors.Select(static descriptor => descriptor.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0024",
                    "MORPH0025",
                    "MORPH0026",
                    "MORPH0027",
                    "MORPH0028"
                }));
            Assert.That(
                descriptors.Select(static descriptor => descriptor.Title
                    .ToString()),
                Is.EqualTo(new[]
                {
                    "Duplicate base configuration call",
                    "Duplicate IncludeBase call",
                    "Included mapping pair not found",
                    "Included mapping type is incompatible",
                    "Inherited mapping expression is inaccessible"
                }));
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.MessageFormat.ToString()),
                Is.EqualTo(new[]
                {
                    "Base configuration is included more than once in " +
                    "Configure of mapper '{0}'.",
                    "IncludeBase is configured more than once for mapping " +
                    "'{0}' in mapper '{1}'.",
                    "Included mapping '{0}' was not found for mapping " +
                    "'{1}' in mapper '{2}'.",
                    "The {0} type '{1}' is not compatible with included " +
                    "{0} type '{2}' for mapping '{3}' in mapper '{4}'.",
                    "The inherited {0} expression for mapping '{1}' is " +
                    "inaccessible from mapper '{2}'."
                }));
            Assert.That(
                descriptors.Select(static descriptor => descriptor.Category),
                Has.All.EqualTo("Morphant.Inheritance"));
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.DefaultSeverity),
                Has.All.EqualTo(DiagnosticSeverity.Error));
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.IsEnabledByDefault),
                Has.All.True);
            Assert.That(
                descriptors.SelectMany(static descriptor =>
                    descriptor.CustomTags),
                Is.Empty);
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.Description.ToString()),
                Has.All.Empty);
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.HelpLinkUri),
                Is.EqualTo(descriptors.Select(static descriptor =>
                    HelpLinkBase + descriptor.Id + ".md")));
            Assert.That(
                descriptors.SelectMany(static descriptor =>
                    descriptor.CustomTags),
                Does.Not.Contain(WellKnownDiagnosticTags.NotConfigurable));
        });
    }
}
