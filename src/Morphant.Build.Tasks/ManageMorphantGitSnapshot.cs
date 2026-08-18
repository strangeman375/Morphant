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

    [Required]
    public string BaseIntermediateOutputPath { get; set; } = string.Empty;

    [Required]
    public string IntermediateOutputPath { get; set; } = string.Empty;

    public string CompilerGeneratedFilesOutputPath { get; set; } = string.Empty;

    public string EmitCompilerGeneratedFiles { get; set; } = string.Empty;

    public string TargetsTriggeredByCompilation { get; set; } = string.Empty;

    protected override void ExecuteCore()
    {
        EnsurePublicationTargetIsRegistered();

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
            EmitCompilerGeneratedFiles);

        switch (Operation)
        {
            case "Prepare":
                GitSnapshotLifecycle.Prepare(context);
                break;
            case "Publish":
                GitSnapshotLifecycle.Publish(context);
                break;
            default:
                throw new SnapshotException(
                    "MORPHANTMSB001",
                    $"Unknown Morphant Git snapshot operation '{Operation}'.");
        }
    }

    private void EnsurePublicationTargetIsRegistered()
    {
        if (!TargetsTriggeredByCompilation
                .Split([';'], StringSplitOptions.RemoveEmptyEntries)
                .Select(static target => target.Trim())
                .Contains(
                    "PublishMorphantGitSnapshot",
                    StringComparer.OrdinalIgnoreCase))
        {
            throw new SnapshotException(
                "MORPHANTMSB017",
                "MorphantGitSnapshot requires PublishMorphantGitSnapshot in " +
                "TargetsTriggeredByCompilation. Remove the command-line or " +
                "global override that prevents post-compile publication.");
        }
    }
}
