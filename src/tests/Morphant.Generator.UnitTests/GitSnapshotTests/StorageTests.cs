using System.Collections;
using Microsoft.Build.Framework;
using Morphant.Build.Tasks;

namespace Morphant.Generator.UnitTests.GitSnapshotTests;

[TestFixture]
internal sealed class StorageTests
{
    private const string Generated = "Morphant.Generated.TypeMapper.Current.g.cs";

    [TestCase("external")]
    [TestCase("obj-siblings")]
    [TestCase("Agent #1 %20 [build]")]
    [TestCase("Agent;run")]
    [TestCase("Agent$(literal)")]
    public void Independent_literal_paths_preserve_foreign_files(string layout)
    {
        using var workspace = new Workspace();
        var task = workspace.CreateTask();
        var parent = layout == "obj-siblings" ? task.IntermediateOutputPath : Path.Combine(workspace.Root, layout);
        task.CompilerGeneratedFilesOutputPath = Path.Combine(parent, "compiler");
        task.SnapshotRoot = Path.Combine(parent, "snapshot");
        // These SDK paths impose no containment requirements on snapshot storage.
        task.BaseIntermediateOutputPath = task.ProjectDirectory;
        Directory.CreateDirectory(task.CompilerGeneratedFilesOutputPath);
        var foreign = Path.Combine(task.CompilerGeneratedFilesOutputPath, "Other.Generated.g.cs");
        File.WriteAllText(foreign, "// foreign\r\n");
        var stale = Path.Combine(task.CompilerGeneratedFilesOutputPath, Generated);
        File.WriteAllText(stale, "// stale\r\n");
        task.Operation = "Prepare";
        AssertSucceeded(task);
        Assert.That(File.Exists(stale), Is.False);
        File.WriteAllText(stale, "// current\r\n");
        task.Operation = "Publish";
        AssertSucceeded(task);
        Assert.That(Sources(task.SnapshotRoot), Is.EqualTo(new Dictionary<string, string>
        {
            [Path.Combine("net10.0", Generated)] = "// current\r\n"
        }));
        Assert.That(File.ReadAllText(foreign), Is.EqualTo("// foreign\r\n"));
        Assert.That(File.ReadAllText(Path.Combine(task.SnapshotRoot, ".morphant")),
            Is.EqualTo("Morphant Git snapshot 1\r\n" + Path.GetRelativePath(task.SnapshotRoot, task.ProjectFile).Replace('\\', '/') + "\r\n"));
    }

    [TestCase("Prepare")]
    [TestCase("Publish")]
    public void Another_project_cannot_claim_or_clean_an_existing_snapshot(string operation)
    {
        using var workspace = new Workspace();
        var first = workspace.CreateTask();
        WriteOutput(first, "// first\r\n");
        AssertSucceeded(first);
        var before = Sources(first.SnapshotRoot);
        var owner = File.ReadAllBytes(Path.Combine(first.SnapshotRoot, ".morphant"));
        var second = workspace.CreateTask("Second");
        second.SnapshotRoot = first.SnapshotRoot;
        second.Operation = operation;
        WriteOutput(second, "// second\r\n");
        AssertRejected(second, "MORPHANTMSB005");
        Assert.That(Sources(first.SnapshotRoot), Is.EqualTo(before));
        Assert.That(File.ReadAllBytes(Path.Combine(first.SnapshotRoot, ".morphant")), Is.EqualTo(owner));
        Assert.That(File.ReadAllText(Path.Combine(second.CompilerGeneratedFilesOutputPath, Generated)), Is.EqualTo("// second\r\n"));
    }

    [TestCase("Project")]
    [TestCase("Configuration")]
    [TestCase("Framework")]
    [TestCase("Runtime")]
    public void Compiler_output_cannot_be_shared_by_different_compilations(string difference)
    {
        using var workspace = new Workspace();
        var first = workspace.CreateTask();
        WriteOutput(first, "// first\r\n");
        AssertSucceeded(first);
        var second = workspace.CreateTask(difference == "Project" ? "Second" : "First");
        second.CompilerGeneratedFilesOutputPath = first.CompilerGeneratedFilesOutputPath;
        second.SnapshotRoot = Path.Combine(workspace.Root, "other-snapshot");
        second.Operation = "Prepare";
        if (difference == "Configuration") second.Configuration = "Debug";
        if (difference == "Framework") second.TargetFramework = "netstandard2.0";
        if (difference == "Runtime") second.RuntimeIdentifier = "linux-x64";
        var before = Sources(first.CompilerGeneratedFilesOutputPath);
        AssertRejected(second, "MORPHANTMSB004");
        Assert.That(Sources(first.CompilerGeneratedFilesOutputPath), Is.EqualTo(before));
        Assert.That(Directory.Exists(second.SnapshotRoot), Is.False);
    }

    [Test]
    public void Distinct_unicode_project_paths_do_not_share_snapshot_ownership()
    {
        if (OperatingSystem.IsMacOS())
            Assert.Ignore("The default macOS filesystem treats canonical Unicode spellings as the same filename.");
        using var workspace = new Workspace();
        var first = workspace.CreateTask("Caf\u00e9");
        var second = workspace.CreateTask("Cafe\u0301");
        WriteOutput(first, "// first\r\n");
        AssertSucceeded(first);
        second.SnapshotRoot = first.SnapshotRoot;
        WriteOutput(second, "// second\r\n");
        AssertRejected(second, "MORPHANTMSB005");
        Assert.That(File.ReadAllText(Path.Combine(first.SnapshotRoot, "net10.0", Generated)), Is.EqualTo("// first\r\n"));
    }

    [Test]
    public void Moving_the_checkout_and_its_artifacts_preserves_ownership()
    {
        using var workspace = new Workspace();
        var task = workspace.CreateTask();
        WriteOutput(task, "// current\r\n");
        AssertSucceeded(task);
        var owner = File.ReadAllBytes(Path.Combine(task.SnapshotRoot, ".morphant"));
        var moved = workspace.Root + "-moved";
        Directory.Move(workspace.Root, moved);
        try
        {
            task.ProjectDirectory = task.ProjectDirectory.Replace(workspace.Root, moved);
            task.ProjectFile = task.ProjectFile.Replace(workspace.Root, moved);
            task.SnapshotRoot = task.SnapshotRoot.Replace(workspace.Root, moved);
            task.CompilerGeneratedFilesOutputPath = task.CompilerGeneratedFilesOutputPath.Replace(workspace.Root, moved);
            AssertSucceeded(task);
            Assert.That(File.ReadAllBytes(Path.Combine(task.SnapshotRoot, ".morphant")), Is.EqualTo(owner));
            Assert.That(File.ReadAllText(Path.Combine(task.SnapshotRoot, "net10.0", Generated)), Is.EqualTo("// current\r\n"));
        }
        finally { Directory.Move(moved, workspace.Root); }
    }

    [Test]
    public void Git_line_ending_conversion_does_not_change_ownership()
    {
        using var workspace = new Workspace();
        var task = workspace.CreateTask();
        WriteOutput(task, "// current\r\n");
        AssertSucceeded(task);
        var owner = Path.Combine(task.SnapshotRoot, ".morphant");
        var normalized = File.ReadAllText(owner).Replace("\r\n", "\n");
        File.WriteAllText(owner, normalized);
        AssertSucceeded(task);
        Assert.That(File.ReadAllText(owner), Is.EqualTo(normalized));
        Assert.That(File.ReadAllText(Path.Combine(task.SnapshotRoot, "net10.0", Generated)), Is.EqualTo("// current\r\n"));
    }

    [TestCase("Snapshot", "Prepare")]
    [TestCase("Snapshot", "Publish")]
    [TestCase("Compiler", "Prepare")]
    [TestCase("Compiler", "Publish")]
    public void Existing_ownership_records_do_not_require_write_access(string location, string operation)
    {
        using var workspace = new Workspace();
        var task = workspace.CreateTask();
        WriteOutput(task, "// previous\r\n");
        AssertSucceeded(task);
        var owner = Path.Combine(location == "Snapshot" ? task.SnapshotRoot : task.CompilerGeneratedFilesOutputPath, ".morphant");
        var attributes = File.GetAttributes(owner);
        var content = File.ReadAllBytes(owner);
        var writeTime = File.GetLastWriteTimeUtc(owner);
        try
        {
            File.SetAttributes(owner, attributes | FileAttributes.ReadOnly);
            WriteOutput(task, "// current\r\n");
            task.Operation = operation;
            AssertSucceeded(task);
            Assert.That(File.ReadAllBytes(owner), Is.EqualTo(content));
            Assert.That(File.GetLastWriteTimeUtc(owner), Is.EqualTo(writeTime));
            Assert.That(File.GetAttributes(owner) & FileAttributes.ReadOnly, Is.EqualTo(FileAttributes.ReadOnly));
            Assert.That(File.ReadAllText(Path.Combine(task.SnapshotRoot, "net10.0", Generated)),
                Is.EqualTo(operation == "Publish" ? "// current\r\n" : "// previous\r\n"));
            if (operation == "Prepare")
                Assert.That(Sources(task.CompilerGeneratedFilesOutputPath), Is.Empty);
        }
        finally { File.SetAttributes(owner, attributes); }
    }

    [TestCase("Snapshot")]
    [TestCase("Compiler")]
    public async Task Ownership_access_errors_do_not_wait_or_modify_storage(string location)
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
        {
            Assert.Ignore("This test uses Unix permissions that do not restrict root.");
            return;
        }
        using var workspace = new Workspace();
        var task = workspace.CreateTask();
        WriteOutput(task, "// previous\r\n");
        AssertSucceeded(task);
        var snapshot = Sources(task.SnapshotRoot);
        var compiler = Sources(task.CompilerGeneratedFilesOutputPath);
        var owner = Path.Combine(location == "Snapshot" ? task.SnapshotRoot : task.CompilerGeneratedFilesOutputPath, ".morphant");
        var mode = File.GetUnixFileMode(owner);
        try
        {
            File.SetUnixFileMode(owner, UnixFileMode.None);
            task.Operation = "Prepare";
            Assert.That(await Task.Run(task.Execute).WaitAsync(TimeSpan.FromSeconds(10)), Is.False);
            var engine = (Engine)task.BuildEngine;
            Assert.That(engine.Errors.Select(error => error.Code), Is.EqualTo(new[] { "MORPHANTMSB999" }));
            Assert.That(engine.Waiting.Task.IsCompleted, Is.False);
            Assert.That(Sources(task.SnapshotRoot), Is.EqualTo(snapshot));
            Assert.That(Sources(task.CompilerGeneratedFilesOutputPath), Is.EqualTo(compiler));
        }
        finally { File.SetUnixFileMode(owner, mode); }
    }

    [TestCase("Checkout")]
    [TestCase("Compiler")]
    [TestCase("Snapshot")]
    public void Linked_roots_use_the_same_physical_storage(string location)
    {
        RequireLinks();
        using var workspace = new Workspace();
        var original = workspace.CreateTask();
        WriteOutput(original, "// first\r\n");
        AssertSucceeded(original);
        var task = workspace.CreateTask();
        var alias = Path.Combine(workspace.Root, "alias");
        var target = location switch
        {
            "Checkout" => task.ProjectDirectory,
            "Compiler" => task.CompilerGeneratedFilesOutputPath,
            _ => task.SnapshotRoot
        };
        Directory.CreateSymbolicLink(alias, target);
        if (location == "Checkout")
        {
            task.ProjectDirectory = alias;
            task.ProjectFile = Path.Combine(alias, "First.csproj");
        }
        else if (location == "Compiler") task.CompilerGeneratedFilesOutputPath = alias;
        else task.SnapshotRoot = alias;
        WriteOutput(task, "// second\r\n");
        AssertSucceeded(task);
        Assert.That(File.ReadAllText(Path.Combine(original.SnapshotRoot, "net10.0", Generated)), Is.EqualTo("// second\r\n"));
    }

    [Test]
    public void Link_aliases_do_not_hide_overlapping_roots()
    {
        RequireLinks();
        using var workspace = new Workspace();
        var task = workspace.CreateTask();
        Directory.CreateDirectory(task.SnapshotRoot);
        var alias = Path.Combine(workspace.Root, "alias");
        Directory.CreateSymbolicLink(alias, task.SnapshotRoot);
        task.CompilerGeneratedFilesOutputPath = Path.Combine(alias, "compiler");
        AssertRejected(task, "MORPHANTMSB003");
        Assert.That(Directory.GetFileSystemEntries(task.SnapshotRoot), Is.Empty);
    }

    [TestCase("Broken")]
    [TestCase("Cycle")]
    public void Unresolvable_links_fail_before_mutation(string kind)
    {
        RequireLinks();
        using var workspace = new Workspace();
        var task = workspace.CreateTask();
        var alias = Path.Combine(workspace.Root, "alias");
        Directory.CreateSymbolicLink(alias, kind == "Cycle" ? alias : Path.Combine(workspace.Root, "missing"));
        task.CompilerGeneratedFilesOutputPath = Path.Combine(alias, "compiler");
        AssertRejected(task, "MORPHANTMSB016");
        Assert.That(Directory.Exists(task.SnapshotRoot), Is.False);
    }

    [TestCase("Prepare", "File")]
    [TestCase("Publish", "File")]
    [TestCase("Prepare", "GeneratorDirectory")]
    [TestCase("Publish", "GeneratorDirectory")]
    public void Links_in_managed_generator_output_are_not_followed(string operation, string kind)
    {
        RequireLinks();
        using var workspace = new Workspace();
        var task = workspace.CreateTask();
        WriteOutput(task, "// old\r\n");
        var outside = Path.Combine(workspace.Root, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, Generated), "// outside\r\n");
        if (kind == "File")
        {
            File.Delete(Path.Combine(task.CompilerGeneratedFilesOutputPath, Generated));
            File.CreateSymbolicLink(Path.Combine(task.CompilerGeneratedFilesOutputPath, Generated), Path.Combine(outside, Generated));
        }
        else
            Directory.CreateSymbolicLink(Path.Combine(task.CompilerGeneratedFilesOutputPath, "Morphant.Generator"), outside);
        task.Operation = operation;
        AssertRejected(task, "MORPHANTMSB016");
        Assert.That(File.ReadAllText(Path.Combine(outside, Generated)), Is.EqualTo("// outside\r\n"));
        Assert.That(Directory.Exists(task.SnapshotRoot), Is.False);
    }

    [Test]
    public async Task Waiting_for_a_snapshot_lock_can_be_cancelled_and_a_fresh_task_can_publish()
    {
        using var workspace = new Workspace();
        var task = workspace.CreateTask();
        WriteOutput(task, "// previous\r\n");
        AssertSucceeded(task);
        var before = Sources(task.SnapshotRoot);
        WriteOutput(task, "// current\r\n");
        var context = Context(task);
        var engine = (Engine)task.BuildEngine;
        using (context.AcquireRootLock())
        {
            var running = Task.Run(task.Execute);
            await engine.Waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
            task.Cancel();
            Assert.That(await running.WaitAsync(TimeSpan.FromSeconds(10)), Is.False);
            Assert.That(engine.Errors, Is.Empty);
            Assert.That(Sources(task.SnapshotRoot), Is.EqualTo(before));
        }
        var retry = workspace.CreateTask();
        AssertSucceeded(retry);
        Assert.That(File.ReadAllText(Path.Combine(task.SnapshotRoot, "net10.0", Generated)), Is.EqualTo("// current\r\n"));
    }

    [Test]
    public async Task Lock_wait_finishes_when_the_previous_publisher_releases_storage()
    {
        using var workspace = new Workspace();
        var task = workspace.CreateTask();
        WriteOutput(task, "// current\r\n");
        Task<bool> running;
        using (Context(task).AcquireRootLock()) { }
        // The lock is on the destination itself, independent of a process's TEMP directory.
        using (new FileStream(Path.Combine(task.SnapshotRoot, ".morphant"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            running = Task.Run(task.Execute);
            await ((Engine)task.BuildEngine).Waiting.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.That(running.IsCompleted, Is.False);
        }
        Assert.That(await running.WaitAsync(TimeSpan.FromSeconds(10)), Is.True);
        Assert.That(File.ReadAllText(Path.Combine(task.SnapshotRoot, "net10.0", Generated)), Is.EqualTo("// current\r\n"));
    }

    [Test]
    public void Cancellation_before_execution_does_not_create_storage()
    {
        using var workspace = new Workspace();
        var task = workspace.CreateTask();
        task.Cancel();
        Assert.That(task.Execute(), Is.False);
        Assert.That(Directory.Exists(task.CompilerGeneratedFilesOutputPath), Is.False);
        Assert.That(Directory.Exists(task.SnapshotRoot), Is.False);
        Assert.That(((Engine)task.BuildEngine).Errors, Is.Empty);
    }

    [TestCase("Snapshot")]
    [TestCase("Compiler")]
    public async Task An_ownership_file_conflict_fails_without_waiting(string location)
    {
        using var workspace = new Workspace();
        var task = workspace.CreateTask();
        WriteOutput(task, "// current\r\n");
        Directory.CreateDirectory(Path.Combine(location == "Snapshot" ? task.SnapshotRoot : task.CompilerGeneratedFilesOutputPath, ".morphant"));
        Assert.That(await Task.Run(task.Execute).WaitAsync(TimeSpan.FromSeconds(10)), Is.False);
        Assert.That(((Engine)task.BuildEngine).Errors.Single().Code, Is.EqualTo("MORPHANTMSB015"));
        Assert.That(((Engine)task.BuildEngine).Waiting.Task.IsCompleted, Is.False);
    }

    private static GitSnapshotContext Context(ManageMorphantGitSnapshot task) => GitSnapshotContext.Create(
        task.ProjectDirectory, task.SnapshotRoot, task.SnapshotDetail, task.TargetFramework,
        task.TargetFrameworks, task.SnapshotTargetFrameworks, task.BaseIntermediateOutputPath,
        task.IntermediateOutputPath, task.CompilerGeneratedFilesOutputPath, task.EmitCompilerGeneratedFiles,
        task.ProjectFile, task.Configuration, task.RuntimeIdentifier);

    private static void WriteOutput(ManageMorphantGitSnapshot task, string text)
    {
        Directory.CreateDirectory(task.CompilerGeneratedFilesOutputPath);
        File.WriteAllText(Path.Combine(task.CompilerGeneratedFilesOutputPath, Generated), text);
    }

    private static Dictionary<string, string> Sources(string root) => Directory.Exists(root)
        ? Directory.GetFiles(root, "Morphant.Generated.*.g.cs", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllText)
        : new();

    private static void AssertSucceeded(ManageMorphantGitSnapshot task) =>
        Assert.That(task.Execute(), Is.True, string.Join("\n", ((Engine)task.BuildEngine).Errors.Select(error => error.Message)));

    private static void AssertRejected(ManageMorphantGitSnapshot task, string code)
    {
        Assert.That(task.Execute(), Is.False);
        Assert.That(((Engine)task.BuildEngine).Errors.Select(error => error.Code), Is.EqualTo(new[] { code }),
            string.Join("\n", ((Engine)task.BuildEngine).Errors.Select(error => error.Message)));
    }

    private static void RequireLinks()
    {
        if (OperatingSystem.IsWindows())
            Assert.Ignore("Creating directory symbolic links requires Windows developer mode or elevation.");
    }

    private sealed class Workspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), nameof(StorageTests), Guid.NewGuid().ToString("N"));
        public ManageMorphantGitSnapshot CreateTask(string project = "First")
        {
            var directory = Path.Combine(Root, "src", project);
            Directory.CreateDirectory(directory);
            return new ManageMorphantGitSnapshot
            {
                BuildEngine = new Engine(), Operation = "Publish", ProjectDirectory = directory,
                ProjectFile = Path.Combine(directory, project + ".csproj"),
                SnapshotRoot = Path.Combine(Root, "snapshots", project), SnapshotDetail = "Mappers",
                TargetFramework = "net10.0", Configuration = "Release",
                BaseIntermediateOutputPath = Path.Combine(Root, "restore", project),
                IntermediateOutputPath = Path.Combine(Root, "compile", project, "Release", "net10.0"),
                CompilerGeneratedFilesOutputPath = Path.Combine(Root, "generated", project, "Release", "net10.0"),
                EmitCompilerGeneratedFiles = "true"
            };
        }
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed class Engine : IBuildEngine
    {
        public List<BuildErrorEventArgs> Errors { get; } = [];
        public TaskCompletionSource<bool> Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool ContinueOnError => false;
        public int LineNumberOfTaskNode => 0;
        public int ColumnNumberOfTaskNode => 0;
        public string ProjectFileOfTaskNode => "Consumer.csproj";
        public void LogErrorEvent(BuildErrorEventArgs e) => Errors.Add(e);
        public void LogWarningEvent(BuildWarningEventArgs e) => Assert.Fail(e.Message ?? "Unexpected MSBuild warning.");
        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            if (e.Message?.StartsWith("Waiting for Morphant snapshot storage:", StringComparison.Ordinal) == true)
                Waiting.TrySetResult(true);
        }
        public void LogCustomEvent(CustomBuildEventArgs e) { }
        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties,
            IDictionary targetOutputs) => throw new NotSupportedException();
    }
}
