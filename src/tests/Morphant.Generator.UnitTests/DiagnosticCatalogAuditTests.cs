using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class DiagnosticCatalogAuditTests
{
    private static readonly CatalogEntry[] ExpectedCatalog =
    {
        new("MORPH0001", "Morphant.Compatibility", DiagnosticSeverity.Error),
        new("MORPH0002", "Morphant.Compatibility", DiagnosticSeverity.Error),
        new("MORPH0003", "Morphant.Compatibility", DiagnosticSeverity.Error),
        new("MORPH0004", "Morphant.Compatibility", DiagnosticSeverity.Error),
        new("MORPH0005", "Morphant.Declaration", DiagnosticSeverity.Error),
        new("MORPH0006", "Morphant.Declaration", DiagnosticSeverity.Error),
        new("MORPH0007", "Morphant.Declaration", DiagnosticSeverity.Error),
        new("MORPH0008", "Morphant.Declaration", DiagnosticSeverity.Error),
        new("MORPH0009", "Morphant.Declaration", DiagnosticSeverity.Error),
        new("MORPH0010", "Morphant.Declaration", DiagnosticSeverity.Error),
        new("MORPH0011", "Morphant.Registration", DiagnosticSeverity.Error),
        new("MORPH0012", "Morphant.Registration", DiagnosticSeverity.Error),
        new("MORPH0013", "Morphant.Registration", DiagnosticSeverity.Error),
        new("MORPH0014", "Morphant.Registration", DiagnosticSeverity.Error),
        new("MORPH0015", "Morphant.Configuration", DiagnosticSeverity.Error),
        new("MORPH0016", "Morphant.Configuration", DiagnosticSeverity.Error),
        new("MORPH0017", "Morphant.Configuration", DiagnosticSeverity.Error),
        new("MORPH0018", "Morphant.Configuration", DiagnosticSeverity.Error),
        new("MORPH0019", "Morphant.Composition", DiagnosticSeverity.Error),
        new("MORPH0020", "Morphant.Composition", DiagnosticSeverity.Error),
        new("MORPH0021", "Morphant.Settings", DiagnosticSeverity.Error),
        new("MORPH0022", "Morphant.Settings", DiagnosticSeverity.Error),
        new("MORPH0023", "Morphant.Settings", DiagnosticSeverity.Error),
        new("MORPH0024", "Morphant.Inheritance", DiagnosticSeverity.Error),
        new("MORPH0025", "Morphant.Inheritance", DiagnosticSeverity.Error),
        new("MORPH0026", "Morphant.Inheritance", DiagnosticSeverity.Error),
        new("MORPH0027", "Morphant.Inheritance", DiagnosticSeverity.Error),
        new("MORPH0028", "Morphant.Inheritance", DiagnosticSeverity.Error),
        new("MORPH0029", "Morphant.Callbacks", DiagnosticSeverity.Error),
        new("MORPH0030", "Morphant.Callbacks", DiagnosticSeverity.Error),
        new("MORPH0031", "Morphant.Callbacks", DiagnosticSeverity.Error),
        new("MORPH0032", "Morphant.Callbacks", DiagnosticSeverity.Error),
        new("MORPH0033", "Morphant.Callbacks", DiagnosticSeverity.Error),
        new("MORPH0034", "Morphant.Declaration", DiagnosticSeverity.Error),
        new("MORPH0035", "Morphant.Construction", DiagnosticSeverity.Error),
        new("MORPH0036", "Morphant.Construction", DiagnosticSeverity.Error),
        new("MORPH0037", "Morphant.Construction", DiagnosticSeverity.Error),
        new("MORPH0038", "Morphant.Construction", DiagnosticSeverity.Error),
        new("MORPH0039", "Morphant.Construction", DiagnosticSeverity.Error),
        new("MORPH0040", "Morphant.Members", DiagnosticSeverity.Error),
        new("MORPH0041", "Morphant.Members", DiagnosticSeverity.Error),
        new("MORPH0042", "Morphant.Members", DiagnosticSeverity.Error),
        new("MORPH0043", "Morphant.Members", DiagnosticSeverity.Error),
        new("MORPH0044", "Morphant.NestedMapping", DiagnosticSeverity.Error),
        new("MORPH0045", "Morphant.NestedMapping", DiagnosticSeverity.Error),
        new("MORPH0046", "Morphant.NestedMapping", DiagnosticSeverity.Error),
        new("MORPH0047", "Morphant.MappingCompleteness", DiagnosticSeverity.Warning),
        new("MORPH0048", "Morphant.MappingCompleteness", DiagnosticSeverity.Warning)
    };

    [Test]
    public void Enumerates_the_exact_project_owned_diagnostic_catalog()
    {
        var descriptors = typeof(MorphantGenerator).Assembly
            .GetTypes()
            .SelectMany(static type => type.GetFields(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static))
            .Where(static field =>
                field.FieldType == typeof(DiagnosticDescriptor))
            .Select(static field =>
                (DiagnosticDescriptor)field.GetValue(null)!)
            .ToArray();
        var actual = descriptors
            .Select(static descriptor => new CatalogEntry(
                descriptor.Id,
                descriptor.Category,
                descriptor.DefaultSeverity))
            .OrderBy(static descriptor => descriptor.Id, StringComparer.Ordinal)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                descriptors.GroupBy(static descriptor => descriptor.Id)
                    .Where(static group => group.Count() != 1)
                    .Select(static group => group.Key),
                Is.Empty,
                "Every diagnostic ID must have exactly one descriptor.");
            Assert.That(
                descriptors.Where(static descriptor =>
                    !Regex.IsMatch(
                        descriptor.Id,
                        "^MORPH[0-9]{4}$",
                        RegexOptions.CultureInvariant))
                    .Select(static descriptor => descriptor.Id),
                Is.Empty);
            Assert.That(
                descriptors.SelectMany(static descriptor =>
                    descriptor.CustomTags),
                Does.Not.Contain(WellKnownDiagnosticTags.NotConfigurable));
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.IsEnabledByDefault),
                Is.All.True);
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.Title.ToString()),
                Is.All.Not.Empty);
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.MessageFormat.ToString()),
                Is.All.Not.Empty);
            Assert.That(actual, Is.EqualTo(ExpectedCatalog));
        });
    }

    private sealed record CatalogEntry(
        string Id,
        string Category,
        DiagnosticSeverity Severity);
}
