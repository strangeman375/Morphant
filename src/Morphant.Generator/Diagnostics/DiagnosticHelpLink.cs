namespace Morphant.Generator.Diagnostics;

internal static class DiagnosticHelpLink
{
    private const string BaseUri =
        "https://github.com/strangeman375/Morphant/blob/main/docs/diagnostics/";

    public static string For(string id) => BaseUri + id + ".md";
}
