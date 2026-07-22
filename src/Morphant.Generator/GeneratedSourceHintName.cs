namespace Morphant.Generator;

internal static class GeneratedSourceHintName
{
    public static string Create(
        string artifactKind,
        string stableIdentity)
    {
        return
            "Morphant.Generated." +
            artifactKind +
            "." +
            stableIdentity +
            ".g.cs";
    }
}
