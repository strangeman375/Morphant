using System.Collections;
using System.Text;
using Microsoft.Build.Framework;
using Morphant.Build.Tasks;

namespace Morphant.Generator.UnitTests.GitSnapshotTests;

[TestFixture]
internal sealed class GitSnapshotLifecycleTests
{
    [Test]
    public void Publishes_the_current_set_without_rewriting_identical_files()
    {
        using var workspace = new SnapshotWorkspace();
        var context = workspace.CreateContext("Release", "net10.0");
        var unchanged = "Morphant.Generated.TypeMapper.Unchanged.g.cs";
        var added = "Morphant.Generated.TypeMapper.Added.g.cs";
        var stale = "Morphant.Generated.TypeMapper.Stale.g.cs";
        var fullArtifact = "Morphant.Generated.Construction.Template.g.cs";
        var unrelated = "Other.Generator.Output.g.cs";

        workspace.WriteCompilerOutput(context, unchanged, "// unchanged\r\n");
        workspace.WriteCompilerOutput(context, added, "// added\r\n");
        workspace.WriteCompilerOutput(context, fullArtifact, "// template\r\n");
        workspace.WriteSnapshot(context, unchanged, "// unchanged\r\n");
        workspace.WriteSnapshot(context, stale, "// stale\r\n");
        workspace.WriteSnapshot(context, fullArtifact, "// old template\r\n");
        workspace.WriteSnapshot(context, unrelated, "// unrelated\r\n");
        var unchangedPath = Path.Combine(context.SliceDirectory, unchanged);
        var originalWriteTime = new DateTime(
            2020,
            1,
            2,
            3,
            4,
            5,
            DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(unchangedPath, originalWriteTime);

        GitSnapshotLifecycle.Publish(context);

        Assert.Multiple(() =>
        {
            Assert.That(
                SnapshotFileNames(context),
                Is.EqualTo(new[] { added, unchanged }));
            Assert.That(File.Exists(Path.Combine(context.SliceDirectory, stale)), Is.False);
            Assert.That(
                File.Exists(Path.Combine(context.SliceDirectory, fullArtifact)),
                Is.False);
            Assert.That(
                File.ReadAllText(Path.Combine(context.SliceDirectory, unrelated)),
                Is.EqualTo("// unrelated\r\n"));
            Assert.That(
                File.GetLastWriteTimeUtc(unchangedPath),
                Is.EqualTo(originalWriteTime));
        });
    }

    [Test]
    public void Full_detail_publishes_every_morphant_artifact()
    {
        using var workspace = new SnapshotWorkspace();
        var context = workspace.CreateContext(
            "Release",
            "net10.0",
            snapshotDetail: "Full");
        string[] files =
        [
            "Morphant.Generated.Construction.Destination.g.cs",
            "Morphant.Generated.MappingExtension.Pair.g.cs",
            "Morphant.Generated.Member.Destination.g.cs",
            "Morphant.Generated.MemberExtension.Pair.g.cs",
            "Morphant.Generated.TypeMapper.Mapper.g.cs"
        ];

        foreach (var file in files)
        {
            workspace.WriteCompilerOutput(context, file, "// generated\r\n");
        }

        GitSnapshotLifecycle.Publish(context);

        Assert.That(
            SnapshotFileNames(context),
            Is.EqualTo(files.Order(StringComparer.Ordinal)));
    }

    [Test]
    public void Rejects_unknown_snapshot_detail_before_publication()
    {
        using var workspace = new SnapshotWorkspace();

        var exception = Assert.Throws<SnapshotException>(() =>
            workspace.CreateContext(
                "Release",
                "net10.0",
                snapshotDetail: "Everything"));

        Assert.That(exception!.Code, Is.EqualTo("MORPHANTMSB020"));
    }

    [Test]
    public void Debug_and_release_publish_one_target_framework_slice()
    {
        using var workspace = new SnapshotWorkspace();
        const string fileName =
            "Morphant.Generated.TypeMapper.Configuration.g.cs";
        var release = workspace.CreateContext("Release", "net10.0");
        var debug = workspace.CreateContext("Debug", "net10.0");

        workspace.WriteCompilerOutput(release, fileName, "// release\r\n");
        GitSnapshotLifecycle.Publish(release);
        workspace.WriteCompilerOutput(debug, fileName, "// debug\r\n");
        GitSnapshotLifecycle.Publish(debug);

        Assert.Multiple(() =>
        {
            Assert.That(debug.SliceDirectory, Is.EqualTo(release.SliceDirectory));
            Assert.That(
                File.ReadAllText(Path.Combine(debug.SliceDirectory, fileName)),
                Is.EqualTo("// debug\r\n"));
        });
    }

    [Test]
    public void Multi_target_project_publishes_only_last_framework_by_default()
    {
        using var workspace = new SnapshotWorkspace();
        const string fileName =
            "Morphant.Generated.TypeMapper.TargetFramework.g.cs";
        var net8 = workspace.CreateContext(
            "Release",
            "net8.0",
            "net8.0;net10.0");
        var net10 = workspace.CreateContext(
            "Release",
            "net10.0",
            "net8.0;net10.0");

        var net8Output = workspace.WriteCompilerOutput(
            net8,
            fileName,
            "// net8\r\n");
        GitSnapshotLifecycle.Prepare(net8);
        GitSnapshotLifecycle.Publish(net8);
        workspace.WriteCompilerOutput(net10, fileName, "// net10\r\n");
        GitSnapshotLifecycle.Publish(net10);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(net8Output), Is.True);
            Assert.That(Directory.Exists(net8.SliceDirectory), Is.False);
            Assert.That(
                File.ReadAllText(Path.Combine(net10.SliceDirectory, fileName)),
                Is.EqualTo("// net10\r\n"));
        });
    }

    [Test]
    public void Removes_owned_files_for_a_no_longer_selected_framework_only()
    {
        using var workspace = new SnapshotWorkspace();
        const string fileName =
            "Morphant.Generated.TypeMapper.TargetFramework.g.cs";
        var net8 = workspace.CreateContext(
            "Release",
            "net8.0",
            "net8.0;net10.0",
            snapshotTargetFrameworks: " net8.0 ; NET8.0 ; net10.0 ");
        var net10 = workspace.CreateContext(
            "Release",
            "net10.0",
            "net8.0;net10.0",
            snapshotTargetFrameworks: " net8.0 ; NET8.0 ; net10.0 ");

        workspace.WriteCompilerOutput(net8, fileName, "// net8\r\n");
        GitSnapshotLifecycle.Publish(net8);
        workspace.WriteSnapshot(net8, "Notes.txt", "keep");
        workspace.WriteCompilerOutput(net10, fileName, "// net10\r\n");
        GitSnapshotLifecycle.Publish(net10);

        var net10Only = workspace.CreateContext(
            "Release",
            "net10.0",
            "net8.0;net10.0",
            snapshotTargetFrameworks: "net10.0");
        GitSnapshotLifecycle.Publish(net10Only);

        Assert.Multiple(() =>
        {
            Assert.That(
                net8.SelectedTargetFrameworks,
                Is.EqualTo(new[] { "net8.0", "net10.0" }));
            Assert.That(
                File.Exists(Path.Combine(net8.SliceDirectory, fileName)),
                Is.False);
            Assert.That(
                File.ReadAllText(Path.Combine(net8.SliceDirectory, "Notes.txt")),
                Is.EqualTo("keep"));
            Assert.That(
                File.Exists(Path.Combine(net10.SliceDirectory, fileName)),
                Is.True);
        });
    }

    [Test]
    public void Rejects_snapshot_framework_not_declared_by_the_project()
    {
        using var workspace = new SnapshotWorkspace();

        var exception = Assert.Throws<SnapshotException>(() =>
            workspace.CreateContext(
                "Release",
                "net10.0",
                "net8.0;net10.0",
                snapshotTargetFrameworks: "net9.0"));

        Assert.That(exception!.Code, Is.EqualTo("MORPHANTMSB021"));
    }

    [Test]
    public void Prepare_removes_only_stale_morphant_files_from_staging()
    {
        using var workspace = new SnapshotWorkspace();
        var context = workspace.CreateContext("Release", "net10.0");
        const string morphant =
            "Morphant.Generated.TypeMapper.Stale.g.cs";
        const string unrelated = "Other.Generator.Output.g.cs";

        workspace.WriteCompilerOutput(context, morphant, "// stale\r\n");
        workspace.WriteCompilerOutput(context, unrelated, "// unrelated\r\n");

        GitSnapshotLifecycle.Prepare(context);

        Assert.Multiple(() =>
        {
            Assert.That(
                Directory.GetFiles(
                    context.CompilerGeneratedDirectory,
                    morphant,
                    SearchOption.AllDirectories),
                Is.Empty);
            Assert.That(
                Directory.GetFiles(
                    context.CompilerGeneratedDirectory,
                    unrelated,
                    SearchOption.AllDirectories),
                Has.Length.EqualTo(1));
        });
    }

    [Test]
    public void Prepare_rejects_nested_directory_links_before_deleting_files()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore(
                "Creating directory symbolic links is not generally " +
                "available to Windows test runners.");
        }

        using var workspace = new SnapshotWorkspace();
        var context = workspace.CreateContext("Release", "net10.0");
        const string generated =
            "Morphant.Generated.TypeMapper.Stale.g.cs";
        var generatedPath = workspace.WriteCompilerOutput(
            context,
            generated,
            "// stale\r\n");
        var outsidePath = workspace.CreateCompilerOutputDirectoryLink(
            context,
            "Morphant.Generated.TypeMapper.Outside.g.cs",
            "// outside\r\n");

        var exception = Assert.Throws<SnapshotException>(() =>
            GitSnapshotLifecycle.Prepare(context));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Code, Is.EqualTo("MORPHANTMSB016"));
            Assert.That(File.Exists(generatedPath), Is.True);
            Assert.That(File.Exists(outsidePath), Is.True);
        });
    }

    [Test]
    public void Publish_preflights_obsolete_slice_links_before_mutation()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore(
                "Creating directory symbolic links is not generally " +
                "available to Windows test runners.");
        }

        using var workspace = new SnapshotWorkspace();
        var context = workspace.CreateContext("Release", "net10.0");
        const string generated =
            "Morphant.Generated.TypeMapper.Current.g.cs";
        workspace.WriteCompilerOutput(context, generated, "// new\r\n");
        var snapshotPath = workspace.WriteSnapshot(
            context,
            generated,
            "// previous\r\n");
        workspace.CreateObsoleteSliceDirectoryLink("net8.0");

        var exception = Assert.Throws<SnapshotException>(() =>
            GitSnapshotLifecycle.Publish(context));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Code, Is.EqualTo("MORPHANTMSB016"));
            Assert.That(
                File.ReadAllText(snapshotPath),
                Is.EqualTo("// previous\r\n"));
        });
    }

    [Test]
    public void Path_overlap_validation_uses_platform_case_rules()
    {
        using var workspace = new SnapshotWorkspace();

        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            var exception = Assert.Throws<SnapshotException>(() =>
                workspace.CreateCaseAliasedIntermediateContext());

            Assert.That(exception!.Code, Is.EqualTo("MORPHANTMSB003"));
        }
        else
        {
            Assert.That(
                workspace.CreateCaseAliasedIntermediateContext,
                Throws.Nothing);
        }
    }

    [Test]
    public void Msbuild_task_executes_prepare_operation()
    {
        using var workspace = new SnapshotWorkspace();
        var context = workspace.CreateContext("Release", "net10.0");
        var generatedPath = workspace.WriteCompilerOutput(
            context,
            "Morphant.Generated.TypeMapper.Stale.g.cs",
            "// stale\r\n");
        var buildEngine = new RecordingBuildEngine();
        var task = workspace.CreateTask("Prepare", buildEngine);

        var succeeded = task.Execute();

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.True);
            Assert.That(buildEngine.Errors, Is.Empty);
            Assert.That(File.Exists(generatedPath), Is.False);
        });
    }

    [Test]
    public void Msbuild_task_reports_missing_publication_target()
    {
        using var workspace = new SnapshotWorkspace();
        var buildEngine = new RecordingBuildEngine();
        var task = workspace.CreateTask(
            "Prepare",
            buildEngine,
            targetsTriggeredByCompilation: "ForeignTarget");

        var succeeded = task.Execute();

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.False);
            Assert.That(
                buildEngine.Errors.Select(static error => error.Code),
                Is.EqualTo(new[] { "MORPHANTMSB017" }));
        });
    }

    [Test]
    public void Msbuild_task_reports_unknown_operation()
    {
        using var workspace = new SnapshotWorkspace();
        var buildEngine = new RecordingBuildEngine();
        var task = workspace.CreateTask("Unknown", buildEngine);

        var succeeded = task.Execute();

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.False);
            Assert.That(
                buildEngine.Errors.Select(static error => error.Code),
                Is.EqualTo(new[] { "MORPHANTMSB001" }));
        });
    }

    private static string[] SnapshotFileNames(GitSnapshotContext context) =>
        Directory.GetFiles(
                context.SliceDirectory,
                "Morphant.Generated.*.g.cs",
                SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;

    private sealed class SnapshotWorkspace : IDisposable
    {
        public SnapshotWorkspace()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                nameof(GitSnapshotLifecycleTests),
                Guid.NewGuid().ToString("N"));
            ProjectDirectory = Path.Combine(Root, "consumer");
            SnapshotRoot = Path.Combine(ProjectDirectory, "Generated");
            BaseIntermediate = Path.Combine(ProjectDirectory, "obj");
            Directory.CreateDirectory(ProjectDirectory);
        }

        private string Root { get; }

        private string ProjectDirectory { get; }

        private string SnapshotRoot { get; }

        private string BaseIntermediate { get; }

        public GitSnapshotContext CreateContext(
            string configuration,
            string targetFramework,
            string targetFrameworks = "",
            string snapshotTargetFrameworks = "",
            string snapshotDetail = "Mappers")
        {
            var intermediate = Path.Combine(
                BaseIntermediate,
                configuration,
                targetFramework);
            return GitSnapshotContext.Create(
                ProjectDirectory,
                SnapshotRoot,
                snapshotDetail,
                targetFramework,
                targetFrameworks,
                snapshotTargetFrameworks,
                BaseIntermediate,
                intermediate,
                Path.Combine(intermediate, "Morphant.CompilerGenerated"),
                "true");
        }

        public string WriteCompilerOutput(
            GitSnapshotContext context,
            string fileName,
            string contents)
        {
            var path = Path.Combine(
                context.CompilerGeneratedDirectory,
                "Morphant.Generator",
                fileName);
            Write(path, contents);
            return path;
        }

        public string WriteSnapshot(
            GitSnapshotContext context,
            string fileName,
            string contents)
        {
            var path = Path.Combine(context.SliceDirectory, fileName);
            Write(path, contents);
            return path;
        }

        public string CreateCompilerOutputDirectoryLink(
            GitSnapshotContext context,
            string fileName,
            string contents)
        {
            var outsideDirectory = Path.Combine(Root, "outside-compiler");
            var outsidePath = Path.Combine(outsideDirectory, fileName);
            Write(outsidePath, contents);
            Directory.CreateDirectory(context.CompilerGeneratedDirectory);
            Directory.CreateSymbolicLink(
                Path.Combine(context.CompilerGeneratedDirectory, "linked"),
                outsideDirectory);
            return outsidePath;
        }

        public void CreateObsoleteSliceDirectoryLink(string targetFramework)
        {
            var outsideDirectory = Path.Combine(Root, "outside-snapshot");
            Directory.CreateDirectory(outsideDirectory);
            Directory.CreateDirectory(SnapshotRoot);
            Directory.CreateSymbolicLink(
                Path.Combine(SnapshotRoot, targetFramework),
                outsideDirectory);
        }

        public GitSnapshotContext CreateCaseAliasedIntermediateContext()
        {
            var baseIntermediate = Path.Combine(
                ProjectDirectory,
                "generated");
            var intermediate = Path.Combine(
                baseIntermediate,
                "Release",
                "net10.0");
            return GitSnapshotContext.Create(
                ProjectDirectory,
                SnapshotRoot,
                "Mappers",
                "net10.0",
                string.Empty,
                string.Empty,
                baseIntermediate,
                intermediate,
                Path.Combine(intermediate, "Morphant.CompilerGenerated"),
                "true");
        }

        public ManageMorphantGitSnapshot CreateTask(
            string operation,
            IBuildEngine buildEngine,
            string targetsTriggeredByCompilation =
                "PublishMorphantGitSnapshot")
        {
            var intermediate = Path.Combine(
                BaseIntermediate,
                "Release",
                "net10.0");
            return new ManageMorphantGitSnapshot
            {
                BuildEngine = buildEngine,
                Operation = operation,
                ProjectDirectory = ProjectDirectory,
                SnapshotRoot = SnapshotRoot,
                SnapshotDetail = "Mappers",
                TargetFramework = "net10.0",
                TargetFrameworks = string.Empty,
                SnapshotTargetFrameworks = string.Empty,
                BaseIntermediateOutputPath = BaseIntermediate,
                IntermediateOutputPath = intermediate,
                CompilerGeneratedFilesOutputPath = Path.Combine(
                    intermediate,
                    "Morphant.CompilerGenerated"),
                EmitCompilerGeneratedFiles = "true",
                TargetsTriggeredByCompilation =
                    targetsTriggeredByCompilation
            };
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private static void Write(string path, string contents)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents, new UTF8Encoding(false));
        }
    }

    private sealed class RecordingBuildEngine : IBuildEngine
    {
        public List<BuildErrorEventArgs> Errors { get; } = [];

        public bool ContinueOnError => false;

        public int LineNumberOfTaskNode => 0;

        public int ColumnNumberOfTaskNode => 0;

        public string ProjectFileOfTaskNode => "GitSnapshotTests.proj";

        public void LogErrorEvent(BuildErrorEventArgs e) => Errors.Add(e);

        public void LogWarningEvent(BuildWarningEventArgs e)
        {
        }

        public void LogMessageEvent(BuildMessageEventArgs e)
        {
        }

        public void LogCustomEvent(CustomBuildEventArgs e)
        {
        }

        public bool BuildProjectFile(
            string projectFileName,
            string[] targetNames,
            IDictionary globalProperties,
            IDictionary targetOutputs) => throw new NotSupportedException();
    }
}
