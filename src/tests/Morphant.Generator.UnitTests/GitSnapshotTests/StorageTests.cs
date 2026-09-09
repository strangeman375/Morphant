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
        var parent = layout == "obj-siblings"
            ? Path.Combine(task.ProjectDirectory, "obj", "Release", "net10.0")
            : Path.Combine(workspace.Root, layout);
        task.CompilerGeneratedFilesOutputPath = Path.Combine(parent, "compiler");
        task.SnapshotRoot = Path.Combine(parent, "snapshot");
        // These SDK paths impose no containment requirements on snapshot storage.
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
        Assert.That(File.ReadAllText(Path.Combine(task.SnapshotRoot, ".morphant", "owner")),
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
        var owner = File.ReadAllBytes(Path.Combine(first.SnapshotRoot, ".morphant", "owner"));
        var second = workspace.CreateTask("Second");
        second.SnapshotRoot = first.SnapshotRoot;
        second.Operation = operation;
        WriteOutput(second, "// second\r\n");
        AssertRejected(second, "MORPHANTMSB005");
        Assert.That(((Engine)second.BuildEngine).Errors.Single().Message, Is.EqualTo(
            $"Morphant snapshot directory '{first.SnapshotRoot}' records project '../../src/First/First.csproj', " +
            "but the current project is '../../src/Second/Second.csproj'. Use a separate snapshot directory. " +
            $"If this project was renamed or moved, remove '{Path.Combine(first.SnapshotRoot, ".morphant")}' and rebuild."));
        Assert.That(Sources(first.SnapshotRoot), Is.EqualTo(before));
        Assert.That(File.ReadAllBytes(Path.Combine(first.SnapshotRoot, ".morphant", "owner")), Is.EqualTo(owner));
        Assert.That(File.ReadAllText(Path.Combine(second.CompilerGeneratedFilesOutputPath, Generated)), Is.EqualTo("// second\r\n"));
    }

    [TestCase("Project")]
    [TestCase("Framework")]
    public void Compiler_output_can_be_reused_by_sequential_compilations(string difference)
    {
        using var workspace = new Workspace();
        var first = workspace.CreateTask();
        WriteOutput(first, "// first\r\n");
        AssertSucceeded(first);
        var second = workspace.CreateTask(difference == "Project" ? "Second" : "First");
        second.CompilerGeneratedFilesOutputPath = first.CompilerGeneratedFilesOutputPath;
        second.SnapshotRoot = Path.Combine(workspace.Root, "other-snapshot");
        second.Operation = "Prepare";
        if (difference == "Framework") second.TargetFramework = "netstandard2.0";
        var before = Sources(first.SnapshotRoot);
        AssertSucceeded(second);
        Assert.That(Sources(first.CompilerGeneratedFilesOutputPath), Is.Empty);
        WriteOutput(second, "// second\r\n");
        second.Operation = "Publish";
        AssertSucceeded(second);
        Assert.That(Sources(first.SnapshotRoot), Is.EqualTo(before));
        Assert.That(Sources(second.SnapshotRoot), Is.EqualTo(new Dictionary<string, string>
        {
            [Path.Combine(second.TargetFramework, Generated)] = "// second\r\n"
        }));
        Assert.That(Directory.GetFileSystemEntries(second.CompilerGeneratedFilesOutputPath, ".morphant*"), Is.Empty);
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
        var owner = File.ReadAllBytes(Path.Combine(task.SnapshotRoot, ".morphant", "owner"));
        var moved = workspace.Root + "-moved";
        Directory.Move(workspace.Root, moved);
        try
        {
            task.ProjectDirectory = task.ProjectDirectory.Replace(workspace.Root, moved);
            task.ProjectFile = task.ProjectFile.Replace(workspace.Root, moved);
            task.SnapshotRoot = task.SnapshotRoot.Replace(workspace.Root, moved);
            task.CompilerGeneratedFilesOutputPath = task.CompilerGeneratedFilesOutputPath.Replace(workspace.Root, moved);
            AssertSucceeded(task);
            Assert.That(File.ReadAllBytes(Path.Combine(task.SnapshotRoot, ".morphant", "owner")), Is.EqualTo(owner));
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
        var owner = Path.Combine(task.SnapshotRoot, ".morphant", "owner");
        var normalized = File.ReadAllText(owner).Replace("\r\n", "\n");
        File.WriteAllText(owner, normalized);
        AssertSucceeded(task);
        Assert.That(File.ReadAllText(owner), Is.EqualTo(normalized));
        Assert.That(File.ReadAllText(Path.Combine(task.SnapshotRoot, "net10.0", Generated)), Is.EqualTo("// current\r\n"));
    }

    [TestCase("Prepare")]
    [TestCase("Publish")]
    public void Existing_snapshot_ownership_does_not_require_write_access(string operation)
    {
        using var workspace = new Workspace();
        var task = workspace.CreateTask();
        WriteOutput(task, "// previous\r\n");
        AssertSucceeded(task);
        var owner = Path.Combine(task.SnapshotRoot, ".morphant", "owner");
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

    [Test]
    public async Task Ownership_access_errors_do_not_modify_storage()
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
        var owner = Path.Combine(task.SnapshotRoot, ".morphant", "owner");
        var mode = File.GetUnixFileMode(owner);
        try
        {
            File.SetUnixFileMode(owner, UnixFileMode.None);
            task.Operation = "Prepare";
            Assert.That(await Task.Run(task.Execute).WaitAsync(TimeSpan.FromSeconds(10)), Is.False);
            var engine = (Engine)task.BuildEngine;
            Assert.That(engine.Errors.Select(error => error.Code), Is.EqualTo(new[] { "MORPHANTMSB999" }));
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
    public async Task Independent_frameworks_can_initialize_one_project_snapshot_together()
    {
        using var workspace = new Workspace();
        var tasks = new[] { "net8.0", "net10.0" }.Select(framework =>
        {
            var task = workspace.CreateTask();
            task.TargetFramework = framework;
            task.CompilerGeneratedFilesOutputPath = Path.Combine(workspace.Root, "compiler", framework);
            WriteOutput(task, "// " + framework + "\r\n");
            return task;
        }).ToArray();
        await Task.WhenAll(tasks.Select(task => Task.Run(() => AssertSucceeded(task))));
        Assert.That(Sources(tasks[0].SnapshotRoot), Is.EqualTo(new Dictionary<string, string>
        {
            [Path.Combine("net8.0", Generated)] = "// net8.0\r\n",
            [Path.Combine("net10.0", Generated)] = "// net10.0\r\n"
        }));
        Assert.That(Directory.GetFiles(tasks[0].SnapshotRoot), Is.Empty);
        Assert.That(Directory.GetDirectories(tasks[0].SnapshotRoot, ".morphant*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName), Is.EqualTo(new[] { ".morphant" }));
        Assert.That(Directory.GetFileSystemEntries(Path.Combine(tasks[0].SnapshotRoot, ".morphant"))
            .Select(Path.GetFileName), Is.EqualTo(new[] { "owner" }));
        Assert.That(File.ReadAllText(Path.Combine(tasks[0].SnapshotRoot, ".morphant", "owner")),
            Is.EqualTo("Morphant Git snapshot 1\r\n../../src/First/First.csproj\r\n"));
    }

    [Test, Repeat(32)]
    public async Task Concurrent_projects_cannot_claim_the_same_snapshot()
    {
        using var workspace = new Workspace();
        var tasks = new[] { workspace.CreateTask("First"), workspace.CreateTask("Second") };
        tasks[1].SnapshotRoot = tasks[0].SnapshotRoot;
        WriteOutput(tasks[0], "// first\r\n");
        WriteOutput(tasks[1], "// second\r\n");
        using var start = new Barrier(2);
        var results = await Task.WhenAll(tasks.Select(task => Task.Run(() =>
        {
            Assert.That(start.SignalAndWait(TimeSpan.FromSeconds(10)), Is.True);
            return task.Execute();
        })));
        Assert.That(results.Count(result => result), Is.EqualTo(1));
        var winner = results[0] ? 0 : 1;
        Assert.That(((Engine)tasks[1 - winner].BuildEngine).Errors.Select(error => error.Code),
            Is.EqualTo(new[] { "MORPHANTMSB005" }));
        Assert.That(Sources(tasks[0].SnapshotRoot), Is.EqualTo(new Dictionary<string, string>
        {
            [Path.Combine("net10.0", Generated)] = winner == 0 ? "// first\r\n" : "// second\r\n"
        }));
        Assert.That(Directory.GetFiles(tasks[0].SnapshotRoot), Is.Empty);
        Assert.That(Directory.GetDirectories(tasks[0].SnapshotRoot, ".morphant*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName), Is.EqualTo(new[] { ".morphant" }));
        Assert.That(Directory.GetFileSystemEntries(Path.Combine(tasks[0].SnapshotRoot, ".morphant"))
            .Select(Path.GetFileName), Is.EqualTo(new[] { "owner" }));
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

    [Test]
    public async Task An_ownership_file_conflict_fails_without_mutation()
    {
        using var workspace = new Workspace();
        var task = workspace.CreateTask();
        WriteOutput(task, "// current\r\n");
        Directory.CreateDirectory(Path.Combine(task.SnapshotRoot, ".morphant", "owner"));
        Assert.That(await Task.Run(task.Execute).WaitAsync(TimeSpan.FromSeconds(10)), Is.False);
        Assert.That(((Engine)task.BuildEngine).Errors.Single().Code, Is.EqualTo("MORPHANTMSB015"));
        Assert.That(Sources(task.CompilerGeneratedFilesOutputPath).Values, Is.EqualTo(new[] { "// current\r\n" }));
    }

    [TestCase(null)]
    [TestCase("incomplete")]
    [TestCase("Morphant Git snapshot 1\n\n")]
    [TestCase("Morphant Git snapshot 1\n../../First.csproj\nextra\n")]
    [TestCase("oversized")]
    public void Invalid_ownership_records_report_recovery_without_mutation(string? record)
    {
        using var workspace = new Workspace();
        var task = workspace.CreateTask();
        WriteOutput(task, "// current\r\n");
        var directory = Path.Combine(task.SnapshotRoot, ".morphant");
        var path = Path.Combine(directory, "owner");
        Directory.CreateDirectory(directory);
        if (record is not null)
            File.WriteAllText(path, record == "oversized" ? new string('x', 65537) : record);
        var before = File.Exists(path) ? File.ReadAllBytes(path) : null;
        AssertRejected(task, "MORPHANTMSB005");
        Assert.That(((Engine)task.BuildEngine).Errors.Single().Message, Is.EqualTo(
            $"Morphant ownership record '{path}' is missing or invalid. Restore it from Git, " +
            $"or remove '{directory}' and rebuild if this snapshot belongs to the current project '../../src/First/First.csproj'."));
        Assert.That(File.Exists(path) ? File.ReadAllBytes(path) : null, Is.EqualTo(before));
        Assert.That(Sources(task.SnapshotRoot), Is.Empty);
        Assert.That(Sources(task.CompilerGeneratedFilesOutputPath).Values, Is.EqualTo(new[] { "// current\r\n" }));
    }

    [Test]
    public void A_previous_format_ownership_file_explains_how_to_rebuild()
    {
        using var workspace = new Workspace();
        var task = workspace.CreateTask();
        WriteOutput(task, "// current\r\n");
        Directory.CreateDirectory(task.SnapshotRoot);
        var path = Path.Combine(task.SnapshotRoot, ".morphant");
        const string previous = "Morphant Git snapshot 1\r\n../../src/First/First.csproj\r\n";
        File.WriteAllText(path, previous);
        AssertRejected(task, "MORPHANTMSB015");
        Assert.That(((Engine)task.BuildEngine).Errors.Single().Message, Is.EqualTo(
            $"Morphant ownership path '{path}' is a file. Remove the previous-format ownership file and rebuild to create '.morphant/owner'."));
        Assert.That(File.ReadAllText(path), Is.EqualTo(previous));
        Assert.That(Sources(task.SnapshotRoot), Is.Empty);
        File.Delete(path);
        task.BuildEngine = new Engine();
        AssertSucceeded(task);
        Assert.That(File.ReadAllText(Path.Combine(path, "owner")), Is.EqualTo(previous));
        Assert.That(Sources(task.SnapshotRoot).Values, Is.EqualTo(new[] { "// current\r\n" }));
    }

    [Test]
    public void A_renamed_project_can_reclaim_its_snapshot_after_removing_ownership_metadata()
    {
        using var workspace = new Workspace();
        var task = workspace.CreateTask();
        WriteOutput(task, "// previous\r\n");
        AssertSucceeded(task);
        task.ProjectFile = Path.Combine(task.ProjectDirectory, "Renamed.csproj");
        WriteOutput(task, "// current\r\n");
        AssertRejected(task, "MORPHANTMSB005");
        Assert.That(Sources(task.SnapshotRoot).Values, Is.EqualTo(new[] { "// previous\r\n" }));
        var directory = Path.Combine(task.SnapshotRoot, ".morphant");
        Directory.Delete(directory, recursive: true);
        task.BuildEngine = new Engine();
        AssertSucceeded(task);
        Assert.That(File.ReadAllText(Path.Combine(directory, "owner")),
            Is.EqualTo("Morphant Git snapshot 1\r\n../../src/First/Renamed.csproj\r\n"));
        Assert.That(Sources(task.SnapshotRoot).Values, Is.EqualTo(new[] { "// current\r\n" }));
    }

    [TestCase("Directory")]
    [TestCase("File")]
    public void Links_in_ownership_metadata_are_not_followed(string kind)
    {
        RequireLinks();
        using var workspace = new Workspace();
        var task = workspace.CreateTask();
        WriteOutput(task, "// current\r\n");
        var outside = Path.Combine(workspace.Root, "outside");
        Directory.CreateDirectory(outside);
        var outsideOwner = Path.Combine(outside, "owner");
        File.WriteAllText(outsideOwner, "// foreign\r\n");
        var directory = Path.Combine(task.SnapshotRoot, ".morphant");
        Directory.CreateDirectory(task.SnapshotRoot);
        if (kind == "Directory")
            Directory.CreateSymbolicLink(directory, outside);
        else
        {
            Directory.CreateDirectory(directory);
            File.CreateSymbolicLink(Path.Combine(directory, "owner"), outsideOwner);
        }
        AssertRejected(task, "MORPHANTMSB016");
        Assert.That(File.ReadAllText(outsideOwner), Is.EqualTo("// foreign\r\n"));
        Assert.That(Sources(task.SnapshotRoot), Is.Empty);
        Assert.That(Sources(task.CompilerGeneratedFilesOutputPath).Values, Is.EqualTo(new[] { "// current\r\n" }));
    }

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
        public string Root { get; } = Path.Combine(TemporaryDirectory(), nameof(StorageTests), Guid.NewGuid().ToString("N"));
        private static string TemporaryDirectory()
        {
            // Resolve ancestor links such as macOS /var before checking exact diagnostic paths.
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

        public ManageMorphantGitSnapshot CreateTask(string project = "First")
        {
            var directory = Path.Combine(Root, "src", project);
            Directory.CreateDirectory(directory);
            return new ManageMorphantGitSnapshot
            {
                BuildEngine = new Engine(), Operation = "Publish", ProjectDirectory = directory,
                ProjectFile = Path.Combine(directory, project + ".csproj"),
                SnapshotRoot = Path.Combine(Root, "snapshots", project), SnapshotDetail = "Mappers",
                TargetFramework = "net10.0",
                CompilerGeneratedFilesOutputPath = Path.Combine(Root, "generated", project, "Release", "net10.0"),
                EmitCompilerGeneratedFiles = "true"
            };
        }
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed class Engine : IBuildEngine
    {
        public List<BuildErrorEventArgs> Errors { get; } = [];
        public bool ContinueOnError => false;
        public int LineNumberOfTaskNode => 0;
        public int ColumnNumberOfTaskNode => 0;
        public string ProjectFileOfTaskNode => "Consumer.csproj";
        public void LogErrorEvent(BuildErrorEventArgs e) => Errors.Add(e);
        public void LogWarningEvent(BuildWarningEventArgs e) => Assert.Fail(e.Message ?? "Unexpected MSBuild warning.");
        public void LogMessageEvent(BuildMessageEventArgs e) { }
        public void LogCustomEvent(CustomBuildEventArgs e) { }
        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties,
            IDictionary targetOutputs) => throw new NotSupportedException();
    }
}
