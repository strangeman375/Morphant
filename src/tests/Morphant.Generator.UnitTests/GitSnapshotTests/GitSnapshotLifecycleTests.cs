using System.Text;
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
        var unrelated = "Other.Generator.Output.g.cs";

        workspace.WriteCompilerOutput(context, unchanged, "// unchanged\r\n");
        workspace.WriteCompilerOutput(context, added, "// added\r\n");
        workspace.WriteSnapshot(context, unchanged, "// unchanged\r\n");
        workspace.WriteSnapshot(context, stale, "// stale\r\n");
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
                File.ReadAllText(Path.Combine(context.SliceDirectory, unrelated)),
                Is.EqualTo("// unrelated\r\n"));
            Assert.That(
                File.GetLastWriteTimeUtc(unchangedPath),
                Is.EqualTo(originalWriteTime));
        });
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
    public void Removes_owned_files_for_a_removed_target_framework_only()
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

        workspace.WriteCompilerOutput(net8, fileName, "// net8\r\n");
        GitSnapshotLifecycle.Publish(net8);
        workspace.WriteSnapshot(net8, "Notes.txt", "keep");
        workspace.WriteCompilerOutput(net10, fileName, "// net10\r\n");
        GitSnapshotLifecycle.Publish(net10);

        var net10Only = workspace.CreateContext(
            "Release",
            "net10.0",
            "net10.0");
        GitSnapshotLifecycle.Publish(net10Only);

        Assert.Multiple(() =>
        {
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
            string targetFrameworks = "")
        {
            var intermediate = Path.Combine(
                BaseIntermediate,
                configuration,
                targetFramework);
            return GitSnapshotContext.Create(
                ProjectDirectory,
                SnapshotRoot,
                targetFramework,
                targetFrameworks,
                BaseIntermediate,
                intermediate,
                Path.Combine(intermediate, "Morphant.CompilerGenerated"),
                "true");
        }

        public void WriteCompilerOutput(
            GitSnapshotContext context,
            string fileName,
            string contents) => Write(
            Path.Combine(
                context.CompilerGeneratedDirectory,
                "Morphant.Generator",
                fileName),
            contents);

        public void WriteSnapshot(
            GitSnapshotContext context,
            string fileName,
            string contents) => Write(
            Path.Combine(context.SliceDirectory, fileName),
            contents);

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
}
