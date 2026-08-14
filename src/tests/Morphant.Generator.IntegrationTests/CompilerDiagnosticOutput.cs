namespace Morphant.Generator.IntegrationTests;

internal static class CompilerDiagnosticOutput
{
    public static string[] Read(string output)
    {
        return output
            .Split('\n')
            .TakeWhile(static line =>
                !line.Contains("Build FAILED.", StringComparison.Ordinal) &&
                !line.Contains("Build succeeded.", StringComparison.Ordinal))
            .Where(static line =>
                line.Contains(": error ", StringComparison.Ordinal) ||
                line.Contains(": warning ", StringComparison.Ordinal))
            .Select(static line => line.Trim())
            .ToArray();
    }

    public static string GetId(string diagnostic)
    {
        var marker = diagnostic.Contains(": error ", StringComparison.Ordinal)
            ? ": error "
            : ": warning ";
        var start = diagnostic.IndexOf(marker, StringComparison.Ordinal) +
                    marker.Length;
        var end = diagnostic.IndexOf(':', start);

        return diagnostic[start..end];
    }
}
