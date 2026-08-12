using Microsoft.CodeAnalysis;
using Morphant.Generator.TypeMapperGeneration;

namespace Morphant.Generator.UnitTests.MemberDiagnosticsTests;

[TestFixture]
internal sealed class DescriptorTests
{
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
                    "Structured member plan is null"
                }));
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.MessageFormat.ToString()),
                Is.EqualTo(new[]
                {
                    "Member rule for '{0}' in contract '{1}' is invalid: {2}.",
                    "Required destination member '{0}' in contract '{1}' is " +
                    "not initialized on reachable paths: {2}.",
                    "Member rule for '{0}' in contract '{1}' cannot be " +
                    "applied: {2}. Reachable paths: {3}.",
                    "Structured member plan for contract '{0}' cannot be " +
                    "null on reachable paths: {1}."
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
                Is.All.Empty);
            Assert.That(
                descriptors.SelectMany(static descriptor =>
                    descriptor.CustomTags),
                Is.Empty);
        });
    }
}
