using Microsoft.Build.Framework;

namespace Morphant.Build.Tasks;

public sealed class PublishMorphantGitSnapshot : MorphantBuildTask
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

    public bool Clean { get; set; } = true;

    internal ISnapshotPublicationObserver Observer { get; set; } =
        NullSnapshotPublicationObserver.Instance;

    protected override void ExecuteCore()
    {
        var path = SnapshotPath.Create(
            ProjectFile,
            ProjectDirectory,
            SnapshotRoot,
            TargetFramework,
            TargetFrameworks,
            BaseIntermediateOutputPath,
            IntermediateOutputPath,
            CompilerGeneratedFilesOutputPath,
            EmitCompilerGeneratedFiles);

        SnapshotLifecycle.Publish(path, Clean, Observer);
    }
}
