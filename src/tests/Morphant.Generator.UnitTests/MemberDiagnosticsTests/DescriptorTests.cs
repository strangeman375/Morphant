using Microsoft.CodeAnalysis;
using Morphant.Generator.TypeMapperGeneration;

namespace Morphant.Generator.UnitTests.MemberDiagnosticsTests;

[TestFixture]
internal sealed class DescriptorTests
{
    private const string HelpLinkBase =
        "https://github.com/strangeman375/Morphant/blob/main/docs/diagnostics/";

    [Test]
    public void Exposes_the_exact_member_diagnostic_contract()
    {
        var descriptors = new[]
        {
            MemberDiagnosticDescriptors.InvalidRule,
            MemberDiagnosticDescriptors.RequiredMember,
            MemberDiagnosticDescriptors.UnavailableLifecycle,
            MemberDiagnosticDescriptors.NullMembersPlan
        };

        Assert.Multiple(() =>
        {
            Assert.That(
                descriptors.Select(static descriptor => descriptor.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0040",
                    "MORPH0041",
                    "MORPH0042",
                    "MORPH0043"
                }));
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.Title.ToString()),
                Is.EqualTo(new[]
                {
                    "Member rule is invalid",
                    "Required destination member is not initialized",
                    "Member rule cannot be applied",
                    "Members returned no rules"
                }));
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.MessageFormat.ToString()),
                Is.EqualTo(new[]
                {
                    "Rule for destination member '{0}' is invalid in " +
                    "mapping '{1}': {2}.",
                    "Required destination member '{0}' is not initialized " +
                    "in mapping '{1}'. Affected cases: {2}.",
                    "Rule for destination member '{0}' cannot be applied in " +
                    "mapping '{1}': {2}. Affected cases: {3}.",
                    "Members returned null or default for mapping '{0}'. " +
                    "Affected cases: {1}."
                }));
            Assert.That(
                descriptors.Select(static descriptor => descriptor.Category),
                Is.All.EqualTo("Morphant.Members"));
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
