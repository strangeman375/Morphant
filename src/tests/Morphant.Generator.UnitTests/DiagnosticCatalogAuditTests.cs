using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class DiagnosticCatalogAuditTests
{
    private const string HelpLinkBase =
        "https://github.com/strangeman375/Morphant/blob/main/docs/diagnostics/";

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
            Assert.That(
                descriptors.Select(static descriptor =>
                    descriptor.HelpLinkUri),
                Is.EqualTo(descriptors.Select(static descriptor =>
                    HelpLinkBase + descriptor.Id + ".md")));
            Assert.That(actual, Is.EqualTo(ExpectedCatalog));
        });
    }

    [Test]
    public void Every_diagnostic_has_one_documentation_page_and_catalog_link()
    {
        var ids = ExpectedCatalog
            .Select(static entry => entry.Id)
            .ToArray();
        var repositoryRoot = FindRepositoryRoot();
        var documentationDirectory = Path.Combine(
            repositoryRoot,
            "docs",
            "diagnostics");
        var documentedIds = Directory
            .GetFiles(documentationDirectory, "MORPH*.md")
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();
        var catalog = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "docs",
            "diagnostics.md"));

        Assert.Multiple(() =>
        {
            Assert.That(documentedIds, Is.EqualTo(ids));

            foreach (var id in ids)
            {
                var page = File.ReadAllText(Path.Combine(
                    documentationDirectory,
                    id + ".md"));

                Assert.That(
                    page,
                    Does.StartWith("# " + id + ": "),
                    id + " must start with its ID and title.");
                Assert.That(
                    page,
                    Does.Contain("## Cause"),
                    id + " must explain why it is reported.");
                Assert.That(
                    page,
                    Does.Contain("## Fix"),
                    id + " must explain how to fix it.");
                Assert.That(
                    catalog,
                    Does.Contain(
                        "[" + id + "](diagnostics/" + id + ".md)"),
                    id + " must be linked from the catalog.");
            }
        });
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(
                TestContext.CurrentContext.TestDirectory);
            directory is not null;
            directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "docs",
                    "diagnostics.md")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not find the Morphant repository root.");
    }

    private sealed record CatalogEntry(
        string Id,
        string Category,
        DiagnosticSeverity Severity);
}
