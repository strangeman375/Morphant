using Microsoft.Build.Framework;

namespace Morphant.Build.Tasks;

public sealed class PrepareMorphantGitSnapshot : MorphantBuildTask
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

    [Output]
    public string[] SnapshotFiles { get; private set; } = [];

    [Output]
    public string SnapshotManifest { get; private set; } = string.Empty;

    [Output]
    public string ForceCompileStamp { get; private set; } = string.Empty;

    [Output]
    public bool ForcedCompilation { get; private set; }

    protected override void ExecuteCore()
    {
        var path = CreatePath();
        var result = SnapshotLifecycle.Prepare(path, Clean);
        SnapshotFiles = result.SnapshotFiles.ToArray();
        SnapshotManifest = path.SnapshotManifest;
        ForceCompileStamp = path.ForceCompileStamp;
        ForcedCompilation = result.ForcedCompilation;

        if (ForcedCompilation)
        {
            LogMessage(
                "Morphant will compile because the Git snapshot or its " +
                "trusted state is missing, modified, or invalid.");
        }
    }

    internal SnapshotPath CreatePath() => SnapshotPath.Create(
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
