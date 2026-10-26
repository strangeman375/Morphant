using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.UnitTests.TestUtils;

internal static class GeneratedSourceText
{
    private const string NewLine = "\r\n";
    private static readonly SourceHashAlgorithm WorkspaceChecksumAlgorithm =
        GetWorkspaceChecksumAlgorithm();

    public static SourceText CreateForWorkspace(string source)
    {
        return SourceText.From(Normalize(source), Encoding.UTF8,
            WorkspaceChecksumAlgorithm);
    }

    private static SourceHashAlgorithm GetWorkspaceChecksumAlgorithm()
    {
        // Roslyn Testing materializes generated syntax as workspace documents.
        // Their default checksum algorithm differs between supported hosts.
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject("Checksum", LanguageNames.CSharp)
            .AddDocument("Checksum.cs", "");
        return document.GetTextAsync().GetAwaiter().GetResult().ChecksumAlgorithm;
    }

    public static string Normalize(string source)
    {
        var normalized = source
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Replace("\n", NewLine);

        return normalized.EndsWith(NewLine, StringComparison.Ordinal)
            ? normalized
            : normalized + NewLine;
    }
}
