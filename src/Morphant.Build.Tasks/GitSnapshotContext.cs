namespace Morphant.Build.Tasks;

internal sealed class GitSnapshotContext
{
    private readonly string snapshotOwner;

    private GitSnapshotContext(string snapshotRoot, GitSnapshotDetail detail,
        string targetFramework, string compilerDirectory, string projectFile)
    {
        SnapshotRoot = snapshotRoot;
        SnapshotDetail = detail;
        CompilerGeneratedDirectory = compilerDirectory;
        SliceDirectory = Path.Combine(snapshotRoot, targetFramework);
        snapshotOwner = "Morphant Git snapshot 1\r\n" + PhysicalDirectory.Relative(snapshotRoot, projectFile) + "\r\n";
    }

    public string SnapshotRoot { get; }
    public GitSnapshotDetail SnapshotDetail { get; }
    public string CompilerGeneratedDirectory { get; }
    public string SliceDirectory { get; }

    public static GitSnapshotContext Create(string projectDirectory, string snapshotRoot, string snapshotDetail,
        string targetFramework,
        string compilerGeneratedFilesOutputPath, string emitCompilerGeneratedFiles,
        string projectFile = "")
    {
        var detail = ParseSnapshotDetail(snapshotDetail);
        targetFramework = string.IsNullOrWhiteSpace(targetFramework) ? "_default" : targetFramework;
        if (!PortablePath.IsSafeComponent(targetFramework))
            throw new SnapshotException("MORPHANTMSB007",
                $"TargetFramework value '{targetFramework}' cannot be used as a safe snapshot directory name.");
        if (!bool.TryParse(emitCompilerGeneratedFiles, out var emit) || !emit)
            throw new SnapshotException("MORPHANTMSB002",
                "MorphantGitSnapshot requires EmitCompilerGeneratedFiles=true. Remove the command-line " +
                "or global override that prevents Morphant from enabling it.");

        var project = PhysicalDirectory.Resolve(projectDirectory, projectDirectory, "MSBuildProjectDirectory");
        var snapshot = PhysicalDirectory.Resolve(snapshotRoot, project, "MorphantGitSnapshotPath");
        if (PhysicalDirectory.Equal(snapshot, project) || PhysicalDirectory.Inside(project, snapshot))
            throw new SnapshotException("MORPHANTMSB005",
                $"MorphantGitSnapshotPath '{snapshot}' must be a dedicated directory, not the project root '{project}' or an ancestor.");

        var compiler = PhysicalDirectory.Resolve(compilerGeneratedFilesOutputPath, project, "CompilerGeneratedFilesOutputPath");
        if (PhysicalDirectory.Equal(compiler, project) || PhysicalDirectory.Inside(project, compiler))
            throw new SnapshotException("MORPHANTMSB004",
                $"CompilerGeneratedFilesOutputPath '{compiler}' must be a dedicated compilation directory, not the project root '{project}' or an ancestor.");
        if (PhysicalDirectory.Overlap(snapshot, compiler))
            throw new SnapshotException("MORPHANTMSB003",
                $"CompilerGeneratedFilesOutputPath '{compiler}' and MorphantGitSnapshotPath '{snapshot}' must not overlap. Use separate directories.");

        return new GitSnapshotContext(snapshot, detail, targetFramework, compiler,
            Path.Combine(project, string.IsNullOrEmpty(projectFile) ? "Morphant.csproj" : Path.GetFileName(projectFile)));
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

    public void CheckSnapshotOwner(CancellationToken cancellationToken, bool create = false) =>
        GitSnapshotStorage.CheckOwner(SnapshotRoot, snapshotOwner, create, cancellationToken);
    public void EnsureSafeCompilerOutput() => PhysicalDirectory.EnsureNoLinks(
        CompilerGeneratedDirectory, CompilerGeneratedDirectory, "CompilerGeneratedFilesOutputPath");
    public void EnsureSafeSnapshotPath(string path, string description) =>
        PhysicalDirectory.EnsureNoLinks(SnapshotRoot, path, description);
}
