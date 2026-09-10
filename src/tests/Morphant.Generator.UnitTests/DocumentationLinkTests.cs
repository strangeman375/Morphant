using System.Text.RegularExpressions;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class DocumentationLinkTests
{
    private const string RepositoryUrl = "https://github.com/strangeman375/Morphant/blob/main/";

    [Test]
    public void Public_Markdown_and_XML_links_resolve_to_existing_files_and_sections()
    {
        var root = FindRepositoryRoot();
        var documents = Directory.GetFiles(Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar).Contains("internal"))
            .Concat([Path.Combine(root, "README.md"), Path.Combine(root, "CHANGELOG.md")]);
        var failures = new List<string>();

        foreach (var document in documents)
        {
            var text = WithoutCodeBlocks(File.ReadAllText(document));
            var links = Regex.Matches(text, @"\]\(([^\s)]+)\)")
                .Select(match => match.Groups[1].Value)
                .Concat(Regex.Matches(text, @"^\[[^\]]+\]:\s*(\S+)", RegexOptions.Multiline)
                    .Select(match => match.Groups[1].Value))
                .Concat(Regex.Matches(text, "(?:href|src)=\"([^\"]+)\"")
                    .Select(match => match.Groups[1].Value));
            foreach (var link in links) CheckLink(document, link);
        }

        foreach (var source in Directory.GetFiles(Path.Combine(root, "src", "Morphant"), "*.cs",
                     SearchOption.AllDirectories)
                     .Where(path => !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar)
                         .Any(part => part is "bin" or "obj")))
        {
            foreach (Match link in Regex.Matches(File.ReadAllText(source), "///.*?href=\"([^\"]+)\""))
                CheckLink(source, link.Groups[1].Value);
        }

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
        return;

        void CheckLink(string source, string link)
        {
            string target;
            if (link.StartsWith(RepositoryUrl, StringComparison.Ordinal))
                target = Path.Combine(root, link[RepositoryUrl.Length..]);
            else if (Uri.TryCreate(link, UriKind.Absolute, out _))
                return;
            else
                target = Path.Combine(Path.GetDirectoryName(source)!, link);

            var parts = target.Split('#', 2);
            var path = Uri.UnescapeDataString(parts[0]);
            // A fragment-only link refers to the current document.
            if (link.StartsWith('#')) path = source;
            var label = Path.GetRelativePath(root, source) + " -> " + link;
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                failures.Add(label + " (missing path)");
                return;
            }

            if (parts.Length == 2 && parts[1].Length != 0 && Path.GetExtension(path) == ".md")
            {
                var anchors = GetAnchors(WithoutCodeBlocks(File.ReadAllText(path)));
                if (!anchors.Contains(Uri.UnescapeDataString(parts[1])))
                    failures.Add(label + " (missing section)");
            }
        }
    }

    private static HashSet<string> GetAnchors(string markdown)
    {
        var anchors = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match heading in Regex.Matches(markdown, @"^#{1,6} (.+)$", RegexOptions.Multiline))
        {
            var title = Regex.Replace(heading.Groups[1].Value.Trim(), @"\[([^\]]+)\]\([^)]*\)", "$1");
            var slug = Regex.Replace(title.ToLowerInvariant(), @"[^\p{L}\p{N}\p{M}\-_ ]", "")
                .Replace(' ', '-');
            var anchor = slug;
            for (var duplicate = 1; !anchors.Add(anchor); duplicate++) anchor = slug + "-" + duplicate;
        }

        foreach (Match anchor in Regex.Matches(markdown, "<(?:a|h[1-6])[^>]*(?:id|name)=\"([^\"]+)\""))
            anchors.Add(anchor.Groups[1].Value);
        return anchors;
    }

    private static string WithoutCodeBlocks(string text) =>
        Regex.Replace(text.ReplaceLineEndings("\n"), @"^```[^\n]*\n.*?^```[^\n]*(?:\n|$)", "",
            RegexOptions.Multiline | RegexOptions.Singleline);

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
             directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "src", "Morphant.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Could not find the Morphant repository root.");
    }
}
