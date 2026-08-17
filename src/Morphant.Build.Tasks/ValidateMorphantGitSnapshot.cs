using Microsoft.Build.Framework;

namespace Morphant.Build.Tasks;

public sealed class ValidateMorphantGitSnapshot : MorphantBuildTask
{
    [Required]
    public string ProjectFile { get; set; } = string.Empty;

    [Required]
    public string ProjectDirectory { get; set; } = string.Empty;

    [Required]
    public string SnapshotRoot { get; set; } = string.Empty;

    public string TargetFramework { get; set; } = string.Empty;

    public string TargetFrameworks { get; set; } = string.Empty;

    [Required]
    public string BaseIntermediateOutputPath { get; set; } = string.Empty;

    [Required]
    public string IntermediateOutputPath { get; set; } = string.Empty;

    public string CompilerGeneratedFilesOutputPath { get; set; } = string.Empty;

    public string EmitCompilerGeneratedFiles { get; set; } = string.Empty;

    public string TargetsTriggeredByCompilation { get; set; } = string.Empty;

    protected override void ExecuteCore()
    {
        if (!TargetsTriggeredByCompilation
                .Split([';'], StringSplitOptions.RemoveEmptyEntries)
                .Select(static target => target.Trim())
                .Contains(
                    "PublishMorphantGitSnapshot",
                    StringComparer.Ordinal))
        {
            throw new SnapshotException(
                "MORPHANTMSB017",
                "MorphantGitSnapshot requires PublishMorphantGitSnapshot in " +
                "TargetsTriggeredByCompilation. Remove the command-line or " +
                "global override that prevents post-compile publication.");
        }

        _ = SnapshotPath.Create(
            ProjectFile,
            ProjectDirectory,
            SnapshotRoot,
            TargetFramework,
            TargetFrameworks,
            BaseIntermediateOutputPath,
            IntermediateOutputPath,
            CompilerGeneratedFilesOutputPath,
            EmitCompilerGeneratedFiles);
    }
}
