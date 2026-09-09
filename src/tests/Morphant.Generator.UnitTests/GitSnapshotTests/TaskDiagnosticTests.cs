using System.Collections;
using Microsoft.Build.Framework;
using Morphant.Build.Tasks;

namespace Morphant.Generator.UnitTests.GitSnapshotTests;

[TestFixture]
internal sealed class TaskDiagnosticTests
{
    [TestCase("Operation", "Unknown", "MORPHANTMSB001", "Unknown Morphant Git snapshot operation 'Unknown'.")]
    [TestCase("Emit", "false", "MORPHANTMSB002", "MorphantGitSnapshot requires EmitCompilerGeneratedFiles=true. Remove the command-line or global override that prevents Morphant from enabling it.")]
    [TestCase("Snapshot", "", "MORPHANTMSB006", "MorphantGitSnapshotPath must name one valid directory path.")]
    [TestCase("Framework", "../outside", "MORPHANTMSB007", "TargetFramework value '../outside' cannot be used as a safe snapshot directory name.")]
    [TestCase("Detail", "Everything", "MORPHANTMSB020", "MorphantGitSnapshotDetail must be Mappers or Full. The effective value is 'Everything'.")]
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
                task.CompilerGeneratedFilesOutputPath = task.SnapshotRoot;
                message = $"CompilerGeneratedFilesOutputPath '{task.CompilerGeneratedFilesOutputPath}' and MorphantGitSnapshotPath '{task.SnapshotRoot}' must not overlap. Use separate directories.";
                break;
            case "Root":
                task.SnapshotRoot = task.ProjectDirectory;
                message = $"MorphantGitSnapshotPath '{task.SnapshotRoot}' must be a dedicated directory, not the project root '{task.ProjectDirectory}' or an ancestor.";
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
    [TestCase("Build [CI]", false)]
    [TestCase("Nested/Generators", true)]
    [TestCase("Old/../MyGenerated/.", true)]
    [TestCase("Nested/Generators/", false)]
    public void Custom_compiler_output_is_cleaned_and_published_without_touching_other_output(
        string subdirectory, bool relative)
    {
        using var workspace = new Workspace();
        var task = workspace.Task;
        var configured = Path.Combine(workspace.Intermediate, subdirectory);
        var output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured));
        task.CompilerGeneratedFilesOutputPath = relative
            ? Path.GetRelativePath(task.ProjectDirectory, workspace.Intermediate) + "/" + subdirectory
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
    public void Compiler_output_accepts_independent_directories(
        [Values("Prepare", "Publish")] string operation,
        [Values("Intermediate", "NormalizedIntermediate", "Parent", "PrefixSibling", "OtherFramework")] string kind)
    {
        using var workspace = new Workspace();
        var task = workspace.Task;
        task.Operation = operation;
        task.CompilerGeneratedFilesOutputPath = kind switch
        {
            "Intermediate" => workspace.Intermediate,
            "NormalizedIntermediate" => Path.Combine(workspace.Intermediate, "Nested", ".."),
            "Parent" => workspace.BaseIntermediate,
            "PrefixSibling" => workspace.Intermediate + "-other",
            "OtherFramework" => Path.Combine(workspace.BaseIntermediate, "Release", "net9.0", "Custom"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        Assert.That(task.Execute(), Is.True);
        Assert.That(workspace.Engine.Errors, Is.Empty);
        Assert.That(File.ReadAllText(Path.Combine(task.SnapshotRoot, "user.txt")), Is.EqualTo("user file"));
    }

    [Test]
    public void Custom_compiler_output_accepts_linked_roots(
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
        var linked = Path.Combine(workspace.Intermediate, "CustomLink");
        var target = Path.Combine(task.ProjectDirectory, "linked-output");
        var targetOutput = nested ? Path.Combine(target, "Nested") : target;
        Directory.CreateDirectory(targetOutput);
        File.WriteAllText(Path.Combine(targetOutput, "Morphant.Generated.TypeMapper.Linked.g.cs"), "// linked\r\n");
        Directory.CreateSymbolicLink(linked, target);
        task.CompilerGeneratedFilesOutputPath = nested ? Path.Combine(linked, "Nested") : linked;
        Assert.That(task.Execute(), Is.True);
        Assert.That(workspace.Engine.Errors, Is.Empty);
        var name = "Morphant.Generated.TypeMapper.Linked.g.cs";
        if (operation == "Publish")
            Assert.That(File.ReadAllText(Path.Combine(task.SnapshotRoot, "net10.0", name)), Is.EqualTo("// linked\r\n"));
        else
            Assert.That(File.Exists(Path.Combine(targetOutput, name)), Is.False);
    }

    [Test]
    public void Custom_compiler_output_rejects_a_file_before_mutation(
        [Values("Prepare", "Publish")] string operation)
    {
        using var workspace = new Workspace();
        var task = workspace.Task;
        task.Operation = operation;
        task.CompilerGeneratedFilesOutputPath = Path.Combine(workspace.Intermediate, "occupied");
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
    public void Unexpected_task_failure_reports_full_details_and_returns_false()
    {
        var engine = new RecordingBuildEngine();
        var task = new FailingTask { BuildEngine = engine };

        Assert.That(task.Execute(), Is.False);
        AssertError(engine, "MORPHANTMSB999",
            "Unexpected Morphant Git snapshot failure: TestFailure: deliberate failure\n   at TestTask.ExecuteCore()",
            "FailingTask");
    }

    private static void AssertFailure(Workspace workspace, string code, string message)
    {
        Assert.That(workspace.Task.Execute(), Is.False);
        AssertError(workspace.Engine, code, message, "ManageMorphantGitSnapshot");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void File_system_failures_report_recovery_and_keep_full_details_in_verbose_output(bool accessDenied)
    {
        var engine = new RecordingBuildEngine();
        Exception failure = accessDenied
            ? new UnauthorizedAccessException("Cannot write '/snapshot/mapper.g.cs'.")
            : new IOException("Cannot write '/snapshot/mapper.g.cs'.");
        var task = new FileSystemFailureTask(failure) { BuildEngine = engine };
        Assert.That(task.Execute(), Is.False);
        AssertError(engine, "MORPHANTMSB999",
            "Cannot complete Morphant Git snapshot: Cannot write '/snapshot/mapper.g.cs'. " +
            "Check that the output directories are accessible and writable. After correcting the cause, " +
            "run a rebuild to refresh the snapshot.", "FileSystemFailureTask");
        Assert.That(engine.Messages.Single().Importance, Is.EqualTo(MessageImportance.Low));
        Assert.That(engine.Messages.Single().Message, Is.EqualTo(failure.ToString()));
    }

    [Test]
    public void Publication_reports_the_exact_path_and_changed_file_counts()
    {
        using var workspace = new Workspace();
        var task = workspace.Task;
        Assert.That(task.Execute(), Is.True);
        Assert.That(task.Execute(), Is.True);
        File.Delete(Path.Combine(task.CompilerGeneratedFilesOutputPath, "Morphant.Generated.TypeMapper.Current.g.cs"));
        Assert.That(task.Execute(), Is.True);
        var directory = Path.Combine(task.SnapshotRoot, "net10.0");
        Assert.That(task.SnapshotDirectory, Is.EqualTo(directory));
        Assert.That(workspace.Engine.Messages.Select(message => message.Message), Is.EqualTo(new[]
        {
            $"Morphant Git snapshot '{directory}': 1 updated, 1 removed, 0 unchanged.",
            $"Morphant Git snapshot '{directory}': 0 updated, 0 removed, 1 unchanged.",
            $"Morphant Git snapshot '{directory}': 0 updated, 1 removed, 0 unchanged."
        }));
        Assert.That(workspace.Engine.Messages.All(message => message.Importance == MessageImportance.High), Is.True);
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
        private readonly string root = Path.Combine(TemporaryDirectory(), nameof(TaskDiagnosticTests), Guid.NewGuid().ToString("N"));
        public RecordingBuildEngine Engine { get; } = new();
        public ManageMorphantGitSnapshot Task { get; }
        public string BaseIntermediate => Path.Combine(root, "obj");
        public string Intermediate => Path.Combine(BaseIntermediate, "Release", "net10.0");

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
                CompilerGeneratedFilesOutputPath = Path.Combine(intermediate, "Morphant.CompilerGenerated"),
                EmitCompilerGeneratedFiles = "true"
            };
            Directory.CreateDirectory(Task.CompilerGeneratedFilesOutputPath);
            Directory.CreateDirectory(Path.Combine(Task.SnapshotRoot, "net10.0"));
            File.WriteAllText(Path.Combine(Task.CompilerGeneratedFilesOutputPath, "Morphant.Generated.TypeMapper.Current.g.cs"), "// current\r\n");
            File.WriteAllText(Path.Combine(Task.SnapshotRoot, "net10.0", "Morphant.Generated.TypeMapper.Previous.g.cs"), "// previous\r\n");
            File.WriteAllText(Path.Combine(Task.SnapshotRoot, "user.txt"), "user file");
        }

        public Dictionary<string, byte[]> Files() => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => path != Path.Combine(Task.SnapshotRoot, ".morphant", "owner"))
            .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.Ordinal);

        public void Dispose() => Directory.Delete(root, recursive: true);

        private static string TemporaryDirectory()
        {
            // macOS may expose TEMP through /var -> /private/var. Use the BCL to
            // create the fixture at its physical path before asserting exact diagnostics.
            var directory = new DirectoryInfo(Path.GetTempPath());
            var parts = new Stack<string>();
            for (var current = directory; current.Parent is not null; current = current.Parent)
                parts.Push(current.Name);
            var path = directory.Root.FullName;
            foreach (var part in parts)
            {
                var child = new DirectoryInfo(Path.Combine(path, part));
                path = child.ResolveLinkTarget(true)?.FullName ?? child.FullName;
            }
            return path;
        }
    }

    private sealed class FailingTask : MorphantBuildTask
    {
        protected override void ExecuteCore() => throw new TestFailure();
    }

    private sealed class FileSystemFailureTask(Exception failure) : MorphantBuildTask
    {
        protected override void ExecuteCore() => throw failure;
    }

    private sealed class TestFailure : InvalidOperationException
    {
        public override string ToString() => "TestFailure: deliberate failure\n   at TestTask.ExecuteCore()";
    }

    private sealed class RecordingBuildEngine : IBuildEngine
    {
        public List<BuildErrorEventArgs> Errors { get; } = [];
        public List<BuildWarningEventArgs> Warnings { get; } = [];
        public List<BuildMessageEventArgs> Messages { get; } = [];
        public bool ContinueOnError => false;
        public int LineNumberOfTaskNode => 12;
        public int ColumnNumberOfTaskNode => 3;
        public string ProjectFileOfTaskNode => "Consumer.csproj";
        public void LogErrorEvent(BuildErrorEventArgs e) => Errors.Add(e);
        public void LogWarningEvent(BuildWarningEventArgs e) => Warnings.Add(e);
        public void LogMessageEvent(BuildMessageEventArgs e) => Messages.Add(e);
        public void LogCustomEvent(CustomBuildEventArgs e) { }
        public bool BuildProjectFile(string projectFileName, string[] targetNames,
            IDictionary globalProperties, IDictionary targetOutputs) => throw new NotSupportedException();
    }
}
