namespace Morphant.Build.Tasks;

internal sealed class GitSnapshotContext
{
    private readonly string snapshotOwner;
    private readonly string compilerOwner;

    private GitSnapshotContext(string snapshotRoot, GitSnapshotDetail detail,
        GitSnapshotFrameworkSelection selection, string compilerDirectory,
        string projectFile, string configuration, string runtimeIdentifier)
    {
        SnapshotRoot = snapshotRoot;
        SnapshotDetail = detail;
        SelectedTargetFrameworks = selection.Selected;
        IsSelectedTargetFramework = selection.IncludesCurrent;
        CompilerGeneratedDirectory = compilerDirectory;
        SliceDirectory = Path.Combine(snapshotRoot, selection.Current);
        snapshotOwner = "Morphant Git snapshot 1\r\n" + PhysicalDirectory.Relative(snapshotRoot, projectFile) + "\r\n";
        compilerOwner = "Morphant compiler output 1\r\n" + PhysicalDirectory.Relative(compilerDirectory, projectFile) +
            $"\r\n{configuration}\r\n{selection.Current}\r\n{runtimeIdentifier}\r\n";
    }

    public string SnapshotRoot { get; }
    public GitSnapshotDetail SnapshotDetail { get; }
    public IReadOnlyCollection<string> SelectedTargetFrameworks { get; }
    public bool IsSelectedTargetFramework { get; }
    public string CompilerGeneratedDirectory { get; }
    public string SliceDirectory { get; }

    public static GitSnapshotContext Create(string projectDirectory, string snapshotRoot, string snapshotDetail,
        string targetFramework, string targetFrameworks, string snapshotTargetFrameworks,
        string baseIntermediateOutputPath, string intermediateOutputPath,
        string compilerGeneratedFilesOutputPath, string emitCompilerGeneratedFiles,
        string projectFile = "", string configuration = "", string runtimeIdentifier = "")
    {
        var detail = ParseSnapshotDetail(snapshotDetail);
        if (!bool.TryParse(emitCompilerGeneratedFiles, out var emit) || !emit)
            throw new SnapshotException("MORPHANTMSB002",
                "MorphantGitSnapshot requires EmitCompilerGeneratedFiles=true. Remove the command-line " +
                "or global override that prevents Morphant from enabling it.");

        var project = PhysicalDirectory.Resolve(projectDirectory, projectDirectory, "MSBuildProjectDirectory");
        var snapshot = PhysicalDirectory.Resolve(snapshotRoot, project, "MorphantGitSnapshotPath");
        if (PhysicalDirectory.Equal(snapshot, project) || PhysicalDirectory.Inside(project, snapshot))
            throw new SnapshotException("MORPHANTMSB005",
                "MorphantGitSnapshotPath must be a dedicated directory, not the project root or an ancestor.");

        var compiler = PhysicalDirectory.Resolve(compilerGeneratedFilesOutputPath, project, "CompilerGeneratedFilesOutputPath");
        if (PhysicalDirectory.Equal(compiler, project) || PhysicalDirectory.Inside(project, compiler))
            throw new SnapshotException("MORPHANTMSB004",
                "CompilerGeneratedFilesOutputPath must be a dedicated compilation directory, not the project root or an ancestor.");
        if (PhysicalDirectory.Overlap(snapshot, compiler))
            throw new SnapshotException("MORPHANTMSB003",
                "CompilerGeneratedFilesOutputPath and MorphantGitSnapshotPath must not overlap.");

        var selection = GitSnapshotFrameworkSelection.Create(targetFramework, targetFrameworks, snapshotTargetFrameworks);
        return new GitSnapshotContext(snapshot, detail, selection, compiler,
            Path.Combine(project, string.IsNullOrEmpty(projectFile) ? "Morphant.csproj" : Path.GetFileName(projectFile)),
            configuration, runtimeIdentifier);
    }

    private static GitSnapshotDetail ParseSnapshotDetail(string value)
    {
        if (string.Equals(value, "Mappers", StringComparison.OrdinalIgnoreCase))
            return GitSnapshotDetail.Mappers;
        if (string.Equals(value, "Full", StringComparison.OrdinalIgnoreCase))
            return GitSnapshotDetail.Full;
        throw new SnapshotException("MORPHANTMSB020",
            "MorphantGitSnapshotDetail must be Mappers or Full. " + $"The effective value is '{value}'.");
    }

    public IDisposable AcquireRootLock(CancellationToken cancellationToken = default, Action<string>? waiting = null) =>
        GitSnapshotStorage.Acquire(SnapshotRoot, snapshotOwner, "MORPHANTMSB005", cancellationToken, waiting);

    public IDisposable AcquireCompilerLock(CancellationToken cancellationToken = default, Action<string>? waiting = null) =>
        GitSnapshotStorage.Acquire(CompilerGeneratedDirectory, compilerOwner, "MORPHANTMSB004", cancellationToken, waiting);

    public void CheckSnapshotOwner(CancellationToken cancellationToken, Action<string>? waiting)
    {
        var path = Path.Combine(SnapshotRoot, GitSnapshotStorage.OwnerFileName);
        if (File.Exists(path) || Directory.Exists(path) || PhysicalDirectory.IsLink(path))
        {
            using var snapshotLock = AcquireRootLock(cancellationToken, waiting);
        }
    }

    public bool IsSelectedTargetFrameworkSlice(string value) => SelectedTargetFrameworks.Contains(value, StringComparer.OrdinalIgnoreCase);
    public void EnsureSafeCompilerOutput() => PhysicalDirectory.EnsureNoLinks(
        CompilerGeneratedDirectory, CompilerGeneratedDirectory, "CompilerGeneratedFilesOutputPath");
    public void EnsureSafeSnapshotPath(string path, string description) =>
        PhysicalDirectory.EnsureNoLinks(SnapshotRoot, path, description);
}
