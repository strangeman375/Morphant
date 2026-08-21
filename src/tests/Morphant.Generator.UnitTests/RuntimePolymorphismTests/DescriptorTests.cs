using Microsoft.CodeAnalysis;
using Morphant.Generator.PairConfiguration;

namespace Morphant.Generator.UnitTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class DescriptorTests
{
    private const string HelpLinkBase =
        "https://github.com/strangeman375/Morphant/blob/main/docs/diagnostics/";

    [Test]
    public void Exposes_the_exact_polymorphism_diagnostic_contract()
    {
        var descriptors = new[]
        {
            PolymorphismDiagnosticDescriptors.SelfLink,
            PolymorphismDiagnosticDescriptors.DuplicateSource,
            PolymorphismDiagnosticDescriptors.IncompatibleType,
            PolymorphismDiagnosticDescriptors.InaccessibleType
        };

        Assert.Multiple(() =>
        {
            Assert.That(
                descriptors.Select(static descriptor => descriptor.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0052",
                    "MORPH0053",
                    "MORPH0054",
                    "MORPH0055"
                }));
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.Title.ToString()),
                Is.EqualTo(new[]
                {
                    "Polymorphic mapping cannot link to itself",
                    "Polymorphic source branch is duplicated",
                    "Polymorphic branch type is incompatible",
                    "Polymorphic branch type is inaccessible"
                }));
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.MessageFormat.ToString()),
                Is.EqualTo(new[]
                {
                    "ForDerived source type '{0}' is the exact source type " +
                    "of mapping '{1}'.",
                    "ForDerived source type '{0}' is configured more than " +
                    "once for mapping '{1}'.",
                    "ForDerived {0} type '{1}' is not assignable to base " +
                    "{0} type '{2}' for mapping '{3}'.",
                    "ForDerived {0} type '{1}' is inaccessible from " +
                    "generated mapper '{2}'."
                }));
            Assert.That(
                descriptors.Select(static descriptor => descriptor.Category),
                Is.All.EqualTo("Morphant.Polymorphism"));
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
