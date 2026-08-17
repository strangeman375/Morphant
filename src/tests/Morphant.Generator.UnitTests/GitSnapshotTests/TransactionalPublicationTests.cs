using System.Text;
using Morphant.Build.Tasks;

namespace Morphant.Generator.UnitTests.GitSnapshotTests;

[TestFixture]
internal sealed class TransactionalPublicationTests
{
    [TestCase("slice-replaced")]
    [TestCase("slice-backed-up")]
    [TestCase("root-manifest-replaced")]
    [TestCase("trusted-root-state-replaced")]
    [TestCase("trusted-state-replaced")]
    [TestCase("outputs-project-replaced")]
    public void Restores_the_complete_previous_snapshot_when_publication_fails(
        string checkpoint)
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            nameof(TransactionalPublicationTests),
            Guid.NewGuid().ToString("N"));
        var projectDirectory = Path.Combine(testDirectory, "consumer");
        var projectFile = Path.Combine(projectDirectory, "Consumer.csproj");
        var snapshotRoot = Path.Combine(projectDirectory, "Generated");
        var intermediate = Path.Combine(projectDirectory, "obj", "Release");
        var compilerOutput = Path.Combine(
            intermediate,
            "Morphant.CompilerGenerated");
        var generatedFile = Path.Combine(
            compilerOutput,
            "Morphant.Generator",
            "Morphant.Generated.TypeMapper.Test.g.cs");

        try
        {
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(projectFile, "<Project />");
            WriteGeneratedFile(generatedFile, "// previous\r\n");

            var path = SnapshotPath.Create(
                projectFile,
                projectDirectory,
                snapshotRoot,
                "net10.0",
                string.Empty,
                Path.Combine(projectDirectory, "obj"),
                intermediate,
                compilerOutput,
                "true");

            SnapshotLifecycle.Publish(path, clean: true);
            var previousSnapshot = DirectoryContents(snapshotRoot);
            var previousState = DirectoryContents(path.StateDirectory);
            var previousRootState = DirectoryContents(path.RootStateDirectory);

            WriteGeneratedFile(generatedFile, "// replacement\r\n");

            Assert.Throws<InjectedPublicationFailure>(() =>
                SnapshotLifecycle.Publish(
                    path,
                    clean: true,
                    new ThrowingObserver(checkpoint)));

            Assert.Multiple(() =>
            {
                Assert.That(
                    DirectoryContents(snapshotRoot),
                    Is.EqualTo(previousSnapshot),
                    "The source-tree snapshot was only partially rolled back.");
                Assert.That(
                    DirectoryContents(path.StateDirectory),
                    Is.EqualTo(previousState),
                    "Trusted intermediate state was only partially rolled back.");
                Assert.That(
                    DirectoryContents(path.RootStateDirectory),
                    Is.EqualTo(previousRootState),
                    "Trusted root state was only partially rolled back.");
            });
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    [Test]
    public void Refuses_to_delete_an_obsolete_slice_from_an_untrusted_root_index()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            nameof(TransactionalPublicationTests),
            Guid.NewGuid().ToString("N"));
        var projectDirectory = Path.Combine(testDirectory, "consumer");
        var projectFile = Path.Combine(projectDirectory, "Consumer.csproj");
        var snapshotRoot = Path.Combine(projectDirectory, "Generated");
        var baseIntermediate = Path.Combine(projectDirectory, "obj");

        try
        {
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(projectFile, "<Project />");

            var net8Path = CreatePath("net8.0", "net8.0;net10.0");
            WriteGeneratedFile(
                Path.Combine(
                    net8Path.CompilerGeneratedDirectory,
                    "Morphant.Generator",
                    "Morphant.Generated.TypeMapper.Test.g.cs"),
                "// net8\r\n");
            SnapshotLifecycle.Publish(net8Path, clean: true);

            var net10Path = CreatePath("net10.0", "net8.0;net10.0");
            WriteGeneratedFile(
                Path.Combine(
                    net10Path.CompilerGeneratedDirectory,
                    "Morphant.Generator",
                    "Morphant.Generated.TypeMapper.Test.g.cs"),
                "// net10\r\n");
            SnapshotLifecycle.Publish(net10Path, clean: true);

            File.AppendAllText(net10Path.RootManifest, "\n");
            var stateBeforeCleanup = DirectoryContents(snapshotRoot);
            var cleanupPath = CreatePath("net10.0", "net10.0");

            var exception = Assert.Throws<SnapshotException>(() =>
                SnapshotLifecycle.CleanObsoleteSlices(cleanupPath));

            Assert.Multiple(() =>
            {
                Assert.That(exception!.Code, Is.EqualTo("MORPHANTMSB018"));
                Assert.That(
                    DirectoryContents(snapshotRoot),
                    Is.EqualTo(stateBeforeCleanup),
                    "Untrusted root metadata authorized deletion.");
                Assert.That(
                    Directory.Exists(net8Path.SliceDirectory),
                    Is.True,
                    "The obsolete slice was deleted from untrusted state.");
            });
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }

        SnapshotPath CreatePath(string targetFramework, string targetFrameworks)
        {
            var intermediate = Path.Combine(
                baseIntermediate,
                "Release",
                targetFramework);
            return SnapshotPath.Create(
                projectFile,
                projectDirectory,
                snapshotRoot,
                targetFramework,
                targetFrameworks,
                baseIntermediate,
                intermediate,
                Path.Combine(intermediate, "Morphant.CompilerGenerated"),
                "true");
        }
    }

    [Test]
    public void Debug_and_release_share_one_slice_and_last_publication_wins()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            nameof(TransactionalPublicationTests),
            Guid.NewGuid().ToString("N"));
        var projectDirectory = Path.Combine(testDirectory, "consumer");
        var projectFile = Path.Combine(projectDirectory, "Consumer.csproj");
        var snapshotRoot = Path.Combine(projectDirectory, "Generated");
        var baseIntermediate = Path.Combine(projectDirectory, "obj");
        const string generatedName =
            "Morphant.Generated.TypeMapper.Test.g.cs";

        try
        {
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(projectFile, "<Project />");

            var releasePath = CreatePath("Release");
            WriteGeneratedFile(
                Path.Combine(
                    releasePath.CompilerGeneratedDirectory,
                    "Morphant.Generator",
                    generatedName),
                "// release\r\n");
            SnapshotLifecycle.Publish(releasePath, clean: true);

            var debugPath = CreatePath("Debug");
            Assert.That(
                debugPath.SliceDirectory,
                Is.EqualTo(releasePath.SliceDirectory),
                "Build configurations must publish the same TFM slice.");

            WriteGeneratedFile(
                Path.Combine(
                    debugPath.CompilerGeneratedDirectory,
                    "Morphant.Generator",
                    generatedName),
                "// debug\r\n");
            SnapshotLifecycle.Publish(debugPath, clean: true);

            Assert.Multiple(() =>
            {
                Assert.That(
                    File.ReadAllText(Path.Combine(
                        debugPath.SliceDirectory,
                        generatedName)),
                    Is.EqualTo("// debug\r\n"),
                    "The last successful configuration must replace the " +
                    "shared snapshot.");
                Assert.That(
                    Directory.GetDirectories(
                        snapshotRoot,
                        "config-*",
                        SearchOption.TopDirectoryOnly),
                    Is.Empty,
                    "Configuration-specific snapshot copies are forbidden.");
            });
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }

        SnapshotPath CreatePath(string configuration)
        {
            var intermediate = Path.Combine(
                baseIntermediate,
                configuration,
                "net10.0");
            return SnapshotPath.Create(
                projectFile,
                projectDirectory,
                snapshotRoot,
                "net10.0",
                string.Empty,
                baseIntermediate,
                intermediate,
                Path.Combine(intermediate, "Morphant.CompilerGenerated"),
                "true");
        }
    }

    private static void WriteGeneratedFile(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents, new UTF8Encoding(false));
    }

    private static Dictionary<string, byte[]> DirectoryContents(string root) =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(root, path)
                    .Replace(Path.DirectorySeparatorChar, '/'),
                File.ReadAllBytes,
                StringComparer.Ordinal);

    private sealed class ThrowingObserver(string checkpoint) :
        ISnapshotPublicationObserver
    {
        public void Reached(string current)
        {
            if (current == checkpoint)
            {
                throw new InjectedPublicationFailure();
            }
        }
    }

    private sealed class InjectedPublicationFailure : Exception;
}
