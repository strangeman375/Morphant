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
                    "Constructor cannot be selected",
                    "Constructor parameter rule is invalid",
                    "Previous destination is unavailable",
                    "Construct or Resolve returned no destination"
                }));
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.MessageFormat.ToString()),
                Is.EqualTo(new[]
                {
                    "Mapping '{0}' cannot create a destination. Affected " +
                    "cases: {1}.",
                    "ConstructorSelection.{1} cannot select a constructor " +
                    "for mapping '{0}': {2}.",
                    "Rule for constructor parameter '{0}' is invalid in " +
                    "mapping '{1}': {2}.",
                    "'previous' is unavailable in mapping '{0}'. Affected " +
                    "cases: {1}.",
                    "Construct or Resolve returned null or default for " +
                    "mapping '{0}'. Affected cases: {1}."
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
