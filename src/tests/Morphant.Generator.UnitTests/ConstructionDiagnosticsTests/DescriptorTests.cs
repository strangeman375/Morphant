using Microsoft.CodeAnalysis;
using Morphant.Generator.TypeMapperGeneration;

namespace Morphant.Generator.UnitTests.ConstructionDiagnosticsTests;

[TestFixture]
internal sealed class DescriptorTests
{
    [Test]
    public void Exposes_the_exact_construction_diagnostic_contract()
    {
        var descriptors = new[]
        {
            ConstructionDiagnosticDescriptors.MissingConstructionPolicy,
            ConstructionDiagnosticDescriptors.ConventionUnavailable,
            ConstructionDiagnosticDescriptors.InvalidParameterRule,
            ConstructionDiagnosticDescriptors.PreviousUnavailable,
            ConstructionDiagnosticDescriptors.NullConstructionPlan
        };

        Assert.Multiple(() =>
        {
            Assert.That(
                descriptors.Select(static descriptor => descriptor.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0035",
                    "MORPH0036",
                    "MORPH0037",
                    "MORPH0038",
                    "MORPH0039"
                }));
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.Title.ToString()),
                Is.EqualTo(new[]
                {
                    "Destination construction is not configured",
                    "Convention construction is unavailable",
                    "Constructor parameter rule is invalid",
                    "Previous destination is unavailable",
                    "Structured construction plan is null"
                }));
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.MessageFormat.ToString()),
                Is.EqualTo(new[]
                {
                    "Destination construction for contract '{0}' is not " +
                    "configured for reachable paths: {1}.",
                    "Convention construction for contract '{0}' is " +
                    "unavailable with ConstructorSelection.{1}: {2}.",
                    "Constructor parameter rule for '{0}' in contract '{1}' " +
                    "is invalid: {2}.",
                    "Previous destination is unavailable for contract '{0}' " +
                    "on reachable paths: {1}.",
                    "Structured construction plan for contract '{0}' cannot " +
                    "be null on reachable paths: {1}."
                }));
            Assert.That(
                descriptors.Select(static descriptor => descriptor.Category),
                Is.All.EqualTo("Morphant.Construction"));
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
