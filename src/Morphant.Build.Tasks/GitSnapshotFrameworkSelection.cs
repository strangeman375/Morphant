namespace Morphant.Build.Tasks;

internal sealed class GitSnapshotFrameworkSelection
{
    private GitSnapshotFrameworkSelection(string current, string[] selected, bool usedFallback)
    {
        Current = current;
        Selected = selected;
        UsedFallback = usedFallback;
    }

    public string Current { get; }
    public string[] Selected { get; }
    public bool UsedFallback { get; }
    public bool IncludesCurrent => Selected.Contains(Current, StringComparer.OrdinalIgnoreCase);

    public static GitSnapshotFrameworkSelection Create(string current, string declared, string requested)
    {
        current = string.IsNullOrWhiteSpace(current) ? "_default" : current;
        EnsureSafe(current, "TargetFramework");
        var frameworks = Split(declared);
        if (frameworks.Length == 0)
            frameworks = [current];
        foreach (var framework in frameworks)
            EnsureSafe(framework, "TargetFrameworks");

        if (string.IsNullOrWhiteSpace(requested))
            return new(current, [frameworks[frameworks.Length - 1]], false);

        var requestedFrameworks = Split(requested);
        if (requestedFrameworks.Length == 0)
            throw new SnapshotException("MORPHANTMSB021",
                "MorphantGitSnapshotTargetFrameworks must contain at least one target framework when specified.");
        foreach (var framework in requestedFrameworks)
            EnsureSafe(framework, "MorphantGitSnapshotTargetFrameworks");

        var selected = frameworks.Where(framework =>
            requestedFrameworks.Contains(framework, StringComparer.OrdinalIgnoreCase)).ToArray();
        return selected.Length > 0
            ? new(current, selected, false)
            : new(current, [frameworks[frameworks.Length - 1]], true);
    }

    private static string[] Split(string value) => value.Split(';')
        .Select(static item => item.Trim()).Where(static item => item.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static void EnsureSafe(string value, string property)
    {
        if (!PortablePath.IsSafeComponent(value))
            throw new SnapshotException("MORPHANTMSB007",
                $"{property} contains a value that cannot be used as a safe snapshot path component.");
    }
}
