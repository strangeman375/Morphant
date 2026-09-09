using System.Collections;
using Microsoft.Build.Framework;
using Morphant.Build.Tasks;

namespace Morphant.Generator.UnitTests.GitSnapshotTests;

[TestFixture]
internal sealed class TaskDiagnosticTests
{
    [TestCase("Operation", "Unknown", "MORPHANTMSB001", "Unknown Morphant Git snapshot operation 'Unknown'.")]
    [TestCase("Emit", "false", "MORPHANTMSB002", "MorphantGitSnapshot requires EmitCompilerGeneratedFiles=true. Remove the command-line or global override that prevents Morphant from enabling it.")]
    [TestCase("Snapshot", "", "MORPHANTMSB006", "MorphantGitSnapshotPath must be one non-empty literal path without wildcards, item separators, or unevaluated MSBuild syntax.")]
    [TestCase("Framework", "../outside", "MORPHANTMSB007", "TargetFramework contains a value that cannot be used as a safe snapshot path component.")]
    [TestCase("Frameworks", "net10.0;CON", "MORPHANTMSB007", "TargetFrameworks contains a value that cannot be used as a safe snapshot path component.")]
    [TestCase("SelectedFrameworks", "CON", "MORPHANTMSB007", "MorphantGitSnapshotTargetFrameworks contains a value that cannot be used as a safe snapshot path component.")]
    [TestCase("Publication", "ForeignTarget", "MORPHANTMSB017", "MorphantGitSnapshot requires PublishMorphantGitSnapshot in TargetsTriggeredByCompilation. Remove the command-line or global override that prevents post-compile publication.")]
    [TestCase("Detail", "Everything", "MORPHANTMSB020", "MorphantGitSnapshotDetail must be Mappers or Full. The effective value is 'Everything'.")]
    [TestCase("SelectedFrameworks", "net9.0", "MORPHANTMSB021", "MorphantGitSnapshotTargetFrameworks contains 'net9.0', which is not declared by TargetFramework or TargetFrameworks. Declared target frameworks: 'net10.0'.")]
    [TestCase("SelectedFrameworks", "; ;", "MORPHANTMSB021", "MorphantGitSnapshotTargetFrameworks must contain at least one target framework when specified.")]
    public void Invalid_configuration_reports_one_actionable_error_before_mutation(
        string property, string value, string code, string message)
    {
        using var workspace = new Workspace();
        var task = workspace.Task;
        switch (property)
        {
            case "Operation": task.Operation = value; break;
            case "Emit": task.EmitCompilerGeneratedFiles = value; break;
            case "Snapshot": task.SnapshotRoot = value; break;
            case "Framework": task.TargetFramework = value; break;
            case "Frameworks": task.TargetFrameworks = value; break;
            case "SelectedFrameworks": task.SnapshotTargetFrameworks = value; break;
            case "Publication": task.TargetsTriggeredByCompilation = value; break;
            case "Detail": task.SnapshotDetail = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(property));
        }

        var before = workspace.Files();
        AssertFailure(workspace, code, message);
        Assert.That(workspace.Files(), Is.EqualTo(before));
    }

    [TestCase("Overlap", "MORPHANTMSB003")]
    [TestCase("Root", "MORPHANTMSB005")]
    [TestCase("File", "MORPHANTMSB015")]
    [TestCase("Directory", "MORPHANTMSB015")]
    public void Unsafe_paths_report_the_responsible_path_and_preserve_files(string kind, string code)
    {
        using var workspace = new Workspace();
        var task = workspace.Task;
        string message;
        switch (kind)
        {
            case "Overlap":
                task.BaseIntermediateOutputPath = task.SnapshotRoot;
                message = "IntermediateOutputPath must remain inside a dedicated BaseIntermediateOutputPath, and neither path may equal the project root or overlap MorphantGitSnapshotPath.";
                break;
            case "Root":
                task.SnapshotRoot = task.ProjectDirectory;
                message = "MorphantGitSnapshotPath must be a dedicated subdirectory inside MSBuildProjectDirectory. The project root, an ancestor, or an external/shared directory is not allowed.";
                break;
            case "File":
                task.SnapshotRoot = Path.Combine(task.ProjectDirectory, "occupied");
                File.WriteAllText(task.SnapshotRoot, "user file");
                message = $"MorphantGitSnapshotPath '{task.SnapshotRoot}' names a file, not a directory.";
                break;
            case "Directory":
                var collision = Path.Combine(task.SnapshotRoot, "net10.0", "Morphant.Generated.TypeMapper.Current.g.cs");
                Directory.CreateDirectory(collision);
                File.WriteAllText(Path.Combine(collision, "user.txt"), "user file");
                message = $"Reserved Morphant snapshot path '{collision}' names a directory, not a generated file.";
                break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }

        var before = workspace.Files();
        AssertFailure(workspace, code, message);
        Assert.That(workspace.Files(), Is.EqualTo(before));
    }

    [TestCase("My Generated", false)]
    [TestCase("Nested/Generators", true)]
    [TestCase("Old/../MyGenerated/.", true)]
    [TestCase("Nested/Generators/", false)]
    public void Custom_compiler_output_is_cleaned_and_published_without_touching_other_output(
        string subdirectory, bool relative)
    {
        using var workspace = new Workspace();
        var task = workspace.Task;
        var configured = Path.Combine(task.IntermediateOutputPath, subdirectory);
        var output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured));
        task.CompilerGeneratedFilesOutputPath = relative
            ? Path.GetRelativePath(task.ProjectDirectory, task.IntermediateOutputPath) + "/" + subdirectory
            : configured;
        Directory.CreateDirectory(output);
        var stale = Path.Combine(output, "Morphant.Generated.TypeMapper.Stale.g.cs");
        var foreign = Path.Combine(output, "Other.Generator.Output.g.cs");
        File.WriteAllText(stale, "// stale\r\n");
        File.WriteAllText(foreign, "// foreign\r\n");
        var afterPreparation = workspace.Files();
        afterPreparation.Remove(Path.GetRelativePath(task.ProjectDirectory, stale));

        task.Operation = "Prepare";
        Assert.That(task.Execute(), Is.True);
        Assert.That(workspace.Files(), Is.EqualTo(afterPreparation),
            "Preparation must preserve the snapshot, foreign files and the old default staging directory.");

        const string currentName = "Morphant.Generated.TypeMapper.Updated.g.cs";
        var current = Path.Combine(output, currentName);
        File.WriteAllText(current, "// updated\r\n");
        var afterPublication = workspace.Files();
        afterPublication.Remove(Path.Combine("Generated", "net10.0", "Morphant.Generated.TypeMapper.Previous.g.cs"));
        afterPublication.Add(Path.Combine("Generated", "net10.0", currentName), File.ReadAllBytes(current));

        task.Operation = "Publish";
        Assert.That(task.Execute(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(workspace.Files(), Is.EqualTo(afterPublication),
                "Publication must use only the effective staging directory and preserve every unrelated file.");
            Assert.That(workspace.Engine.Errors, Is.Empty);
            Assert.That(workspace.Engine.Warnings, Is.Empty);
        });
    }

    [Test]
    public void Compiler_output_must_be_strictly_inside_the_current_intermediate_directory(
        [Values("Prepare", "Publish")] string operation,
        [Values("Intermediate", "NormalizedIntermediate", "Parent", "PrefixSibling", "OtherFramework", "Snapshot")] string kind)
    {
        using var workspace = new Workspace();
        var task = workspace.Task;
        task.Operation = operation;
        task.CompilerGeneratedFilesOutputPath = kind switch
        {
            "Intermediate" => task.IntermediateOutputPath,
            "NormalizedIntermediate" => Path.Combine(task.IntermediateOutputPath, "Nested", ".."),
            "Parent" => task.BaseIntermediateOutputPath,
            "PrefixSibling" => task.IntermediateOutputPath + "-other",
            "OtherFramework" => Path.Combine(task.BaseIntermediateOutputPath, "Release", "net9.0", "Custom"),
            "Snapshot" => task.SnapshotRoot,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var before = workspace.Files();
        var output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(task.CompilerGeneratedFilesOutputPath));

        AssertFailure(workspace, "MORPHANTMSB004",
            $"CompilerGeneratedFilesOutputPath '{output}' must be a dedicated subdirectory inside IntermediateOutputPath '{task.IntermediateOutputPath}'.");
        Assert.That(workspace.Files(), Is.EqualTo(before));
    }

    [Test]
    public void Custom_compiler_output_rejects_linked_directories_before_mutation(
        [Values("Prepare", "Publish")] string operation,
        [Values(false, true)] bool nested)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Creating directory symbolic links is not generally available to Windows test runners.");
        }

        using var workspace = new Workspace();
        var task = workspace.Task;
        task.Operation = operation;
        var linked = Path.Combine(task.IntermediateOutputPath, "CustomLink");
        var target = Path.Combine(task.ProjectDirectory, "linked-output");
        var targetOutput = nested ? Path.Combine(target, "Nested") : target;
        Directory.CreateDirectory(targetOutput);
        File.WriteAllText(Path.Combine(targetOutput, "Morphant.Generated.TypeMapper.Linked.g.cs"), "// linked\r\n");
        Directory.CreateSymbolicLink(linked, target);
        task.CompilerGeneratedFilesOutputPath = nested ? Path.Combine(linked, "Nested") : linked;
        var before = workspace.Files();

        AssertFailure(workspace, "MORPHANTMSB016",
            $"CompilerGeneratedFilesOutputPath traverses symbolic link or reparse point '{linked}'.");
        Assert.That(workspace.Files(), Is.EqualTo(before));
    }

    [Test]
    public void Custom_compiler_output_rejects_a_file_before_mutation(
        [Values("Prepare", "Publish")] string operation)
    {
        using var workspace = new Workspace();
        var task = workspace.Task;
        task.Operation = operation;
        task.CompilerGeneratedFilesOutputPath = Path.Combine(task.IntermediateOutputPath, "occupied");
        File.WriteAllText(task.CompilerGeneratedFilesOutputPath, "user file");
        var before = workspace.Files();

        AssertFailure(workspace, "MORPHANTMSB015",
            $"CompilerGeneratedFilesOutputPath '{task.CompilerGeneratedFilesOutputPath}' names a file, not a directory.");
        Assert.That(workspace.Files(), Is.EqualTo(before));
    }

    [Test]
    public void Duplicate_generated_names_are_rejected_before_publication()
    {
        using var workspace = new Workspace();
        var otherGenerator = Path.Combine(workspace.Task.CompilerGeneratedFilesOutputPath, "OtherGenerator");
        Directory.CreateDirectory(otherGenerator);
        File.WriteAllText(Path.Combine(otherGenerator, "Morphant.Generated.TypeMapper.Current.g.cs"), "// duplicate");
        var before = workspace.Files();

        AssertFailure(workspace, "MORPHANTMSB008",
            "Morphant generated file names must be portable and unique ignoring case. Invalid name: 'Morphant.Generated.TypeMapper.Current.g.cs'.");
        Assert.That(workspace.Files(), Is.EqualTo(before));
    }

    [Test]
    public async Task A_lock_timeout_reports_the_root_and_a_later_publication_recovers()
    {
        using var workspace = new Workspace();
        var task = workspace.Task;
        var context = GitSnapshotContext.Create(task.ProjectDirectory, task.SnapshotRoot, task.SnapshotDetail,
            task.TargetFramework, task.TargetFrameworks, task.SnapshotTargetFrameworks,
            task.BaseIntermediateOutputPath, task.IntermediateOutputPath,
            task.CompilerGeneratedFilesOutputPath, task.EmitCompilerGeneratedFiles);
        var before = workspace.Files();

        // Exercise the real two-minute contention timeout, without replacing the clock or filesystem.
        using (context.AcquireRootLock())
        {
            Assert.That(await System.Threading.Tasks.Task.Run(task.Execute), Is.False);
        }

        var error = workspace.Engine.Errors.Single();
        Assert.Multiple(() =>
        {
            Assert.That(error.Code, Is.EqualTo("MORPHANTMSB019"));
            Assert.That(error.Message, Does.StartWith(
                $"Timed out waiting for another build to release the Morphant snapshot root '{task.SnapshotRoot}': "));
            Assert.That(workspace.Files(), Is.EqualTo(before));
        });
        workspace.Engine.Errors.Clear();
        Assert.That(task.Execute(), Is.True);
        Assert.That(workspace.Engine.Errors, Is.Empty);
        Assert.That(File.ReadAllText(Path.Combine(task.SnapshotRoot, "net10.0", "Morphant.Generated.TypeMapper.Current.g.cs")),
            Is.EqualTo("// current\r\n"));
    }

    [Test]
    public void Unexpected_task_failure_reports_full_details_and_returns_false()
    {
        var engine = new RecordingBuildEngine();
        var task = new FailingTask { BuildEngine = engine };

        Assert.That(task.Execute(), Is.False);
        AssertError(engine, "MORPHANTMSB999",
            "Unexpected Morphant Git snapshot failure: TestFailure: deliberate I/O failure\n   at TestTask.ExecuteCore()",
            "FailingTask");
    }

    private static void AssertFailure(Workspace workspace, string code, string message)
    {
        Assert.That(workspace.Task.Execute(), Is.False);
        AssertError(workspace.Engine, code, message, "ManageMorphantGitSnapshot");
    }

    private static void AssertError(RecordingBuildEngine engine, string code, string message, string sender)
    {
        Assert.That(engine.Errors, Has.Count.EqualTo(1));
        var error = engine.Errors.Single();
        Assert.Multiple(() =>
        {
            Assert.That(error.Code, Is.EqualTo(code));
            Assert.That(error.Message, Is.EqualTo(message));
            Assert.That(error.File, Is.EqualTo("Consumer.csproj"));
            Assert.That(error.Subcategory, Is.EqualTo("MorphantGitSnapshot"));
            Assert.That(error.SenderName, Is.EqualTo(sender));
            Assert.That(new[] { error.LineNumber, error.ColumnNumber, error.EndLineNumber, error.EndColumnNumber },
                Is.EqualTo(new[] { 0, 0, 0, 0 }));
            Assert.That(engine.Warnings, Is.Empty);
        });
    }

    private sealed class Workspace : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), nameof(TaskDiagnosticTests), Guid.NewGuid().ToString("N"));
        public RecordingBuildEngine Engine { get; } = new();
        public ManageMorphantGitSnapshot Task { get; }

        public Workspace()
        {
            var intermediate = Path.Combine(root, "obj", "Release", "net10.0");
            Task = new ManageMorphantGitSnapshot
            {
                BuildEngine = Engine,
                Operation = "Publish",
                ProjectDirectory = root,
                SnapshotRoot = Path.Combine(root, "Generated"),
                SnapshotDetail = "Mappers",
                TargetFramework = "net10.0",
                BaseIntermediateOutputPath = Path.Combine(root, "obj"),
                IntermediateOutputPath = intermediate,
                CompilerGeneratedFilesOutputPath = Path.Combine(intermediate, "Morphant.CompilerGenerated"),
                EmitCompilerGeneratedFiles = "true",
                TargetsTriggeredByCompilation = "PublishMorphantGitSnapshot"
            };
            Directory.CreateDirectory(Task.CompilerGeneratedFilesOutputPath);
            Directory.CreateDirectory(Path.Combine(Task.SnapshotRoot, "net10.0"));
            File.WriteAllText(Path.Combine(Task.CompilerGeneratedFilesOutputPath, "Morphant.Generated.TypeMapper.Current.g.cs"), "// current\r\n");
            File.WriteAllText(Path.Combine(Task.SnapshotRoot, "net10.0", "Morphant.Generated.TypeMapper.Previous.g.cs"), "// previous\r\n");
            File.WriteAllText(Path.Combine(Task.SnapshotRoot, "user.txt"), "user file");
        }

        public Dictionary<string, byte[]> Files() => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.Ordinal);

        public void Dispose() => Directory.Delete(root, recursive: true);
    }

    private sealed class FailingTask : MorphantBuildTask
    {
        protected override void ExecuteCore() => throw new TestFailure();
    }

    private sealed class TestFailure : IOException
    {
        public override string ToString() => "TestFailure: deliberate I/O failure\n   at TestTask.ExecuteCore()";
    }

    private sealed class RecordingBuildEngine : IBuildEngine
    {
        public List<BuildErrorEventArgs> Errors { get; } = [];
        public List<BuildWarningEventArgs> Warnings { get; } = [];
        public bool ContinueOnError => false;
        public int LineNumberOfTaskNode => 12;
        public int ColumnNumberOfTaskNode => 3;
        public string ProjectFileOfTaskNode => "Consumer.csproj";
        public void LogErrorEvent(BuildErrorEventArgs e) => Errors.Add(e);
        public void LogWarningEvent(BuildWarningEventArgs e) => Warnings.Add(e);
        public void LogMessageEvent(BuildMessageEventArgs e) { }
        public void LogCustomEvent(CustomBuildEventArgs e) { }
        public bool BuildProjectFile(string projectFileName, string[] targetNames,
            IDictionary globalProperties, IDictionary targetOutputs) => throw new NotSupportedException();
    }
}
