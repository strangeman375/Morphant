using Microsoft.Build.Framework;

namespace Morphant.Build.Tasks;

public sealed class ManageMorphantGitSnapshot : MorphantBuildTask
{
    [Required]
    public string Operation { get; set; } = string.Empty;

    [Required]
    public string ProjectDirectory { get; set; } = string.Empty;

    [Required]
    public string SnapshotRoot { get; set; } = string.Empty;

    [Required]
    public string SnapshotDetail { get; set; } = string.Empty;

    public string TargetFramework { get; set; } = string.Empty;

    public string CompilerGeneratedFilesOutputPath { get; set; } = string.Empty;

    public string EmitCompilerGeneratedFiles { get; set; } = string.Empty;

    public string ProjectFile { get; set; } = string.Empty;

    [Output]
    public string SnapshotDirectory { get; set; } = string.Empty;

    protected override string FailureContext =>
        $"Morphant Git snapshot operation '{Operation}' for '{SnapshotRoot}' " +
        $"(compiler output '{CompilerGeneratedFilesOutputPath}')";

    protected override void ExecuteCore()
    {
        if (Operation is not ("Prepare" or "Publish"))
            throw new SnapshotException("MORPHANTMSB001",
                $"Unknown Morphant Git snapshot operation '{Operation}'.");

        var context = GitSnapshotContext.Create(
            ProjectDirectory,
            SnapshotRoot,
            SnapshotDetail,
            TargetFramework,
            CompilerGeneratedFilesOutputPath,
            EmitCompilerGeneratedFiles, ProjectFile);
        SnapshotDirectory = context.SliceDirectory;

        switch (Operation)
        {
            case "Prepare":
                GitSnapshotLifecycle.Prepare(context, CancellationToken);
                break;
            case "Publish":
                var result = GitSnapshotLifecycle.Publish(context, CancellationToken);
                LogMessage($"Morphant Git snapshot '{SnapshotDirectory}': " +
                    $"{result.Updated} updated, {result.Removed} removed, {result.Unchanged} unchanged.",
                    MessageImportance.High);
                break;
        }
    }
}
