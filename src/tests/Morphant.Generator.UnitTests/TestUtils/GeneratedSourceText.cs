namespace Morphant.Generator.UnitTests.TestUtils;

internal static class GeneratedSourceText
{
    private const string NewLine = "\r\n";

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
