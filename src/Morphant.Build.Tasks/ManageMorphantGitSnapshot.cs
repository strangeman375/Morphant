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

    public string TargetFrameworks { get; set; } = string.Empty;

    public string SnapshotTargetFrameworks { get; set; } = string.Empty;

    public string BaseIntermediateOutputPath { get; set; } = string.Empty;

    public string IntermediateOutputPath { get; set; } = string.Empty;

    public string CompilerGeneratedFilesOutputPath { get; set; } = string.Empty;

    public string EmitCompilerGeneratedFiles { get; set; } = string.Empty;

    public string ProjectFile { get; set; } = string.Empty;
    public string Configuration { get; set; } = string.Empty;
    public string RuntimeIdentifier { get; set; } = string.Empty;

    protected override void ExecuteCore()
    {
        var selection = GitSnapshotFrameworkSelection.Create(
            TargetFramework, TargetFrameworks, SnapshotTargetFrameworks);
        if (selection.UsedFallback && Operation == "Prepare" && selection.IncludesCurrent)
            LogMessage($"Morphant Git snapshot selection '{SnapshotTargetFrameworks}' does not match " +
                $"this project; using its default target framework '{selection.Selected[0]}'.");
        if (!selection.IncludesCurrent && Operation is "Prepare" or "Publish")
            return;

        var context = GitSnapshotContext.Create(
            ProjectDirectory,
            SnapshotRoot,
            SnapshotDetail,
            TargetFramework,
            TargetFrameworks,
            SnapshotTargetFrameworks,
            BaseIntermediateOutputPath,
            IntermediateOutputPath,
            CompilerGeneratedFilesOutputPath,
            EmitCompilerGeneratedFiles, ProjectFile, Configuration, RuntimeIdentifier);

        switch (Operation)
        {
            case "Prepare":
                GitSnapshotLifecycle.Prepare(context, CancellationToken, path => LogMessage("Waiting for Morphant snapshot storage: " + path));
                break;
            case "Publish":
                GitSnapshotLifecycle.Publish(context, CancellationToken, path => LogMessage("Waiting for Morphant snapshot storage: " + path));
                break;
            default:
                throw new SnapshotException(
                    "MORPHANTMSB001",
                    $"Unknown Morphant Git snapshot operation '{Operation}'.");
        }
    }
}
