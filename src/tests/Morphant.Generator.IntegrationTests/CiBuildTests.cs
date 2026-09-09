using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class CiBuildTests
{
    private const string MapperFile = "Morphant.Generated.TypeMapper.CiConsumer_TestMapper.g.cs";
    private readonly ConsumerBuildWorkspace packages = new();
    private readonly string packageVersion = $"0.0.0-ci-builds.{Guid.NewGuid():N}";

    [OneTimeSetUp]
    public async Task Pack() => AssertSucceeded(await packages.PackMorphant(packageVersion));

    [OneTimeTearDown]
    public void DisposePackages() => packages.Dispose();

    [Test]
    public async Task Restore_build_publish_pack_and_clean_support_separate_CI_steps()
    {
        using var consumer = CreateConsumer();
        AssertSucceeded(await consumer.Run("restore"));
        Assert.That(consumer.Snapshot(), Is.Empty);
        AssertSucceeded(await consumer.Run("build", "--no-restore"));
        AssertMapperSnapshot(consumer);
        var snapshot = consumer.Snapshot();

        // These settings would fail validation if any command ran the compiler.
        string[] inactiveSettings =
        [
            "-p:EmitCompilerGeneratedFiles=false",
            "-p:MorphantGitSnapshotDetail=Everything",
            $"-p:MorphantGitSnapshotPath={Path.Combine(consumer.Root, "external-snapshot")}"
        ];
        AssertSucceeded(await consumer.Run("restore", inactiveSettings));
        var publishDirectory = Path.Combine(consumer.Root, "publish");
        AssertSucceeded(await consumer.Run("publish",
            ["--no-build", "--no-restore", "--output", publishDirectory, .. inactiveSettings]));
        Assert.That(File.Exists(Path.Combine(publishDirectory, "CiConsumer.dll")), Is.True);
        var packageDirectory = Path.Combine(consumer.Root, "packed");
        AssertSucceeded(await consumer.Run("pack",
            ["--no-build", "--no-restore", "--output", packageDirectory, .. inactiveSettings]));
        Assert.That(Directory.GetFiles(packageDirectory, "*.nupkg"), Has.Length.EqualTo(1));
        AssertSucceeded(await consumer.Run("clean"));
        Assert.That(consumer.Snapshot(), Is.EqualTo(snapshot));

        File.WriteAllText(Path.Combine(consumer.SnapshotRoot, "net10.0", "Morphant.Generated.Stale.g.cs"), "// stale");
        AssertSucceeded(await consumer.Run("publish", "--no-restore", "--output", publishDirectory));
        Assert.That(consumer.Snapshot(), Is.EqualTo(snapshot),
            "Publishing with compilation must repair the snapshot after clean, preserving identical files.");
    }

    [Test]
    public async Task Centralized_artifacts_support_build_and_runtime_specific_publish()
    {
        using var consumer = CreateConsumer();
        var artifacts = Path.Combine(consumer.Root, "artifacts");
        AssertSucceeded(await consumer.Run("build", "--artifacts-path", artifacts));
        AssertMapperSnapshot(consumer);
        var snapshot = consumer.Snapshot();
        var publishDirectory = Path.Combine(consumer.Root, "publish");

        AssertSucceeded(await consumer.Run("publish", "--artifacts-path", artifacts,
            "--runtime", RuntimeInformation.RuntimeIdentifier, "--self-contained", "false",
            "--output", publishDirectory));

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(publishDirectory, "CiConsumer.dll")), Is.True);
            Assert.That(Directory.Exists(Path.Combine(artifacts, "obj", "CiConsumer")), Is.True);
            Assert.That(consumer.Snapshot(), Is.EqualTo(snapshot));
        });
    }

    [Test]
    public async Task Parallel_configurations_publish_one_complete_snapshot()
    {
        using var consumer = CreateConsumer();
        AssertSucceeded(await consumer.Run("restore"));
        var builds = await Task.WhenAll(
            consumer.Run("build", "--no-restore", "-p:Configuration=Debug"),
            consumer.Run("build", "--no-restore", "-p:Configuration=Release"));
        foreach (var build in builds) AssertSucceeded(build);
        AssertMapperSnapshot(consumer);
        var debug = File.ReadAllBytes(Directory.GetFiles(
            Path.Combine(consumer.ProjectDirectory, "obj", "Debug"), MapperFile, SearchOption.AllDirectories).Single());
        var release = File.ReadAllBytes(Directory.GetFiles(
            Path.Combine(consumer.ProjectDirectory, "obj", "Release"), MapperFile, SearchOption.AllDirectories).Single());
        var published = File.ReadAllBytes(Path.Combine(consumer.SnapshotRoot, "net10.0", MapperFile));

        Assert.Multiple(() =>
        {
            Assert.That(debug, Is.Not.EqualTo(release), "The two configurations must generate different mappings.");
            Assert.That(published, Is.EqualTo(debug).Or.EqualTo(release),
                "The shared snapshot must contain one whole successful configuration.");
        });
    }

    [Test]
    public async Task Graph_build_keeps_referenced_projects_and_their_snapshots_independent()
    {
        using var consumer = CreateConsumer();
        using var dependency = CreateConsumer(targetFramework: "netstandard2.0");
        dependency.SetProperty("AssemblyName", "CiDependency");
        consumer.AddReference(dependency);

        AssertSucceeded(await consumer.Run("msbuild", "-restore", "-graphBuild", "-m:2"));
        AssertMapperSnapshot(consumer);
        AssertMapperSnapshot(dependency, "netstandard2.0");
    }

    [Test]
    public async Task Building_one_TFM_only_publishes_it_when_selected()
    {
        using var consumer = CreateConsumer(targetFramework: "netstandard2.0;net10.0");
        AssertSucceeded(await consumer.Run("build", "--framework", "netstandard2.0"));
        Assert.That(consumer.Snapshot(), Is.Empty,
            "The default selection remains the last declared TFM, even if another TFM is built alone.");

        AssertSucceeded(await consumer.Run("build", "--framework", "net10.0", "--no-restore"));
        AssertMapperSnapshot(consumer);
    }

    [Test]
    public async Task CI_properties_accept_case_insensitive_snapshot_values()
    {
        using var consumer = CreateConsumer();
        AssertSucceeded(await consumer.Run("build", "-p:MorphantGitSnapshot=TRUE", "-p:MorphantGitSnapshotDetail=full"));
        string[] expected =
        [
            "Morphant.Generated.Construction.CiConsumer_Destination.g.cs",
            "Morphant.Generated.MappingExtension.CiConsumer_Source__CiConsumer_Destination__CiConsumer_TestMapper.g.cs",
            "Morphant.Generated.Member.CiConsumer_Destination.g.cs",
            "Morphant.Generated.MemberExtension.CiConsumer_Source__CiConsumer_Destination__CiConsumer_TestMapper.g.cs",
            MapperFile
        ];
        Assert.That(consumer.Snapshot().Keys.Order(StringComparer.Ordinal),
            Is.EqualTo(expected.Select(name => Path.Combine("net10.0", name)).Order(StringComparer.Ordinal)));
    }

    [TestCase("Checkout")]
    [TestCase("Snapshot")]
    [TestCase("CompilerOutput")]
    public async Task Literal_brackets_in_CI_paths_work_with_and_without_snapshots(string location)
    {
        using var consumer = CreateConsumer(directoryName: location == "Checkout" ? "Consumer [CI]" : "consumer");
        var snapshotRoot = location == "Snapshot"
            ? Path.Combine(consumer.ProjectDirectory, "Generated", "Snapshot [CI]")
            : consumer.SnapshotRoot;
        string[] settings = location switch
        {
            "Snapshot" => [$"-p:MorphantGitSnapshotPath={snapshotRoot}"],
            "CompilerOutput" =>
            [
                "-p:EmitCompilerGeneratedFiles=true",
                "-p:CompilerGeneratedFilesOutputPath=" + Path.Combine(
                    consumer.ProjectDirectory, "obj", "Release", "net10.0", "Compiler [CI]")
            ],
            _ => []
        };

        AssertSucceeded(await consumer.Run("build", [.. settings, "-p:MorphantGitSnapshot=false"]));
        Assert.That(consumer.Snapshot(snapshotRoot), Is.Empty);
        AssertSucceeded(await consumer.Run("build", [.. settings, "-t:Rebuild"]));
        Assert.That(consumer.Snapshot(snapshotRoot).Keys,
            Is.EqualTo(new[] { Path.Combine("net10.0", MapperFile) }));
    }

    [TestCase("IndependentIntermediate")]
    [TestCase("ExternalSnapshot")]
    [TestCase("CompilerOutput")]
    public async Task Independent_build_and_snapshot_directories_are_supported(string scenario)
    {
        using var consumer = CreateConsumer();
        AssertSucceeded(await consumer.Run("build"));
        var snapshot = consumer.Snapshot();
        string[] settings = scenario switch
        {
            "IndependentIntermediate" =>
                [$"-p:IntermediateOutputPath={Path.Combine(consumer.Root, "intermediate")}{Path.DirectorySeparatorChar}"],
            "ExternalSnapshot" => [$"-p:MorphantGitSnapshotPath={Path.Combine(consumer.Root, "snapshot")}"],
            "CompilerOutput" =>
            [
                "-p:EmitCompilerGeneratedFiles=true",
                $"-p:CompilerGeneratedFilesOutputPath={Path.Combine(consumer.Root, "compiler-output")}"
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        AssertSucceeded(await consumer.Run("build", [.. settings, "-t:Rebuild"]));
        if (scenario == "ExternalSnapshot")
        {
            var external = consumer.Snapshot(Path.Combine(consumer.Root, "snapshot"));
            Assert.That(external.Keys, Is.EqualTo(snapshot.Keys));
            Assert.That(external.Values.Select(value => value[(value.IndexOf(':') + 1)..]),
                Is.EqualTo(snapshot.Values.Select(value => value[(value.IndexOf(':') + 1)..])));
        }
        else
            Assert.That(consumer.Snapshot(), Is.EqualTo(snapshot));
    }

    [Test]
    public async Task A_global_framework_selection_updates_the_referenced_project_default()
    {
        using var consumer = CreateConsumer();
        using var dependency = CreateConsumer(targetFramework: "netstandard2.0");
        dependency.SetProperty("AssemblyName", "CiDependency");
        consumer.AddReference(dependency);
        AssertSucceeded(await consumer.Run("build"));
        var consumerSnapshot = consumer.Snapshot();
        var dependencySnapshot = dependency.Snapshot();
        File.WriteAllText(Path.Combine(dependency.SnapshotRoot, "netstandard2.0", "Morphant.Generated.TypeMapper.Stale.g.cs"), "// stale");
        const string selection = "-p:MorphantGitSnapshotTargetFrameworks=net10.0";

        var result = await consumer.Run("msbuild", "-restore", "-graphBuild", "-m:2", "-t:Rebuild", selection);
        AssertSucceeded(result);
        Assert.That(consumer.Snapshot(), Is.EqualTo(consumerSnapshot));
        Assert.That(dependency.Snapshot(), Is.EqualTo(dependencySnapshot));
    }

    [TestCase("net9.0", "net10.0")]
    [TestCase("NETSTANDARD2.0;net9.0", "netstandard2.0")]
    [TestCase("net9.0;NET10.0", "net10.0")]
    public async Task Multi_target_selection_uses_matching_frameworks_or_the_last_declared(
        string requested, string expected)
    {
        using var consumer = CreateConsumer(targetFramework: "netstandard2.0;net10.0");
        consumer.SetProperty("MorphantGitSnapshotTargetFrameworks", requested);
        AssertSucceeded(await consumer.Run("build"));
        AssertMapperSnapshot(consumer, expected);
    }

    [Test]
    public async Task A_linked_checkout_updates_the_same_snapshot()
    {
        using var consumer = CreateConsumer();
        AssertSucceeded(await consumer.Run("build"));
        var snapshot = consumer.Snapshot();
        var linkedDirectory = Path.Combine(consumer.Root, "linked-checkout");
        if (OperatingSystem.IsWindows())
        {
            var start = new System.Diagnostics.ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var argument in new[] { "/c", "mklink", "/J", linkedDirectory, consumer.ProjectDirectory })
                start.ArgumentList.Add(argument);
            using var process = System.Diagnostics.Process.Start(start)!;
            await process.WaitForExitAsync();
            Assert.That(process.ExitCode, Is.Zero, await process.StandardError.ReadToEndAsync());
        }
        else
            Directory.CreateSymbolicLink(linkedDirectory, consumer.ProjectDirectory);
        consumer.ProjectPath = Path.Combine(linkedDirectory, "CiConsumer.csproj");

        AssertSucceeded(await consumer.Run("build", "-t:Rebuild"));
        Assert.That(consumer.Snapshot(), Is.EqualTo(snapshot));
    }

    [TestCase("Project")]
    [TestCase("Global")]
    [TestCase("GlobalLateImport")]
    [TestCase("GlobalWithProjectLocalOverride")]
    [TestCase("GlobalWithExistingPublication")]
    [TestCase("LateImport")]
    public async Task Post_compile_hooks_are_preserved_without_publishing_skipped_compilations(string origin)
    {
        using var consumer = CreateConsumer();
        using var dependency = CreateConsumer(targetFramework: "netstandard2.0");
        dependency.SetProperty("AssemblyName", "CiDependency");
        consumer.AddReference(dependency);
        if (origin == "Project")
            consumer.SetProperty("TargetsTriggeredByCompilation", "CiAfterCompile");
        if (origin is "LateImport" or "GlobalLateImport")
            File.WriteAllText(Path.Combine(consumer.ProjectDirectory, "Directory.Build.targets"),
                "<Project><PropertyGroup><TargetsTriggeredByCompilation>" +
                (origin == "GlobalLateImport" ? "MustNotRun" : "CiAfterCompile") +
                "</TargetsTriggeredByCompilation></PropertyGroup></Project>");
        if (origin == "GlobalWithProjectLocalOverride")
        {
            foreach (var project in new[] { consumer, dependency })
            {
                var document = XDocument.Load(project.ProjectPath);
                document.Root!.SetAttributeValue("TreatAsLocalProperty", "TargetsTriggeredByCompilation");
                document.Save(project.ProjectPath);
                project.SetProperty("TargetsTriggeredByCompilation", "CiAfterCompile");
            }
        }
        string[] settings = origin switch
        {
            "GlobalWithProjectLocalOverride" => ["-p:TargetsTriggeredByCompilation=MustNotRun"],
            "GlobalWithExistingPublication" => ["-p:TargetsTriggeredByCompilation=CiAfterCompile%3BPublishMorphantGitSnapshot"],
            _ when origin.StartsWith("Global", StringComparison.Ordinal) => ["-p:TargetsTriggeredByCompilation=CiAfterCompile"],
            _ => []
        };
        AssertSucceeded(await consumer.Run("build", settings));
        AssertMapperSnapshot(consumer);
        var hook = Path.Combine(consumer.ProjectDirectory, "obj", "Release", "net10.0", "ci-after-compile.txt");
        Assert.That(File.ReadAllText(hook).Trim(), Is.EqualTo("executed"));
        Assert.That(File.ReadAllLines(Path.Combine(consumer.ProjectDirectory, "obj", "Release", "net10.0", "ci-registered-hooks.txt")),
            Is.EqualTo(new[] { "CiAfterCompile", "PublishMorphantGitSnapshot" }));
        if (origin.StartsWith("Global", StringComparison.Ordinal))
            Assert.That(File.ReadAllText(Path.Combine(dependency.ProjectDirectory, "obj", "Release", "netstandard2.0", "ci-after-compile.txt")).Trim(), Is.EqualTo("executed"));
        var before = consumer.Snapshot();
        File.Delete(hook);
        AssertSucceeded(await consumer.Run("build", [.. settings, "--no-restore"]));
        Assert.That(File.Exists(hook), Is.False);
        Assert.That(consumer.Snapshot(), Is.EqualTo(before));
        File.WriteAllText(Path.Combine(consumer.ProjectDirectory, "Broken.cs"), "This does not compile.");
        Assert.That((await consumer.Run("build", settings)).ExitCode, Is.Not.Zero);
        Assert.That(consumer.Snapshot(), Is.EqualTo(before));
    }

    [TestCase("Snapshot", "Generated #1 %20 [CI]")]
    [TestCase("Compiler", "Generated #1 %20 [CI]")]
    [TestCase("Snapshot", "Generated;CI")]
    [TestCase("Compiler", "Generated;CI")]
    [TestCase("Snapshot", "Backslash")]
    [TestCase("Compiler", "Backslash")]
    public async Task Escaped_literal_storage_paths_are_preserved(string location, string name)
    {
        using var consumer = CreateConsumer();
        var output = Path.Combine(consumer.Root, name);
        var escaped = output.Replace("%", "%25").Replace(";", "%3B");
        if (name == "Backslash")
            escaped = escaped.Replace('/', '\\');
        consumer.SetProperty("EmitCompilerGeneratedFiles", "true");
        consumer.SetProperty(location == "Snapshot" ? "MorphantGitSnapshotPath" : "CompilerGeneratedFilesOutputPath", escaped);
        AssertSucceeded(await consumer.Run("build"));
        Assert.That(Directory.Exists(output), Is.True);
        var snapshot = consumer.Snapshot(location == "Snapshot" ? output : consumer.SnapshotRoot);
        Assert.That(snapshot.Keys, Is.EqualTo(new[] { Path.Combine("net10.0", MapperFile) }));
    }

    [Test]
    public async Task External_snapshots_are_excluded_even_when_explicitly_added_to_compile_items()
    {
        using var consumer = CreateConsumer();
        var snapshot = Path.Combine(consumer.Root, "snapshot");
        consumer.SetProperty("MorphantGitSnapshotPath", snapshot);
        AssertSucceeded(await consumer.Run("build"));
        var before = consumer.Snapshot(snapshot);
        var project = XDocument.Load(consumer.ProjectPath);
        project.Root!.Add(new XElement("ItemGroup", new XElement("Compile",
            new XAttribute("Include", Path.Combine(snapshot, "**", "*.g.cs")))));
        project.Save(consumer.ProjectPath);
        AssertSucceeded(await consumer.Run("build", "-t:Rebuild"));
        Assert.That(consumer.Snapshot(snapshot), Is.EqualTo(before));
    }

    [Test]
    public async Task Projects_cannot_overwrite_each_others_external_snapshot()
    {
        using var first = CreateConsumer();
        using var second = CreateConsumer();
        var shared = Path.Combine(first.Root, "shared-snapshot");
        first.SetProperty("MorphantGitSnapshotPath", shared);
        second.SetProperty("MorphantGitSnapshotPath", shared);
        AssertSucceeded(await first.Run("build"));
        var before = first.Snapshot(shared);
        AssertRejected(await second.Run("build"), "MORPHANTMSB005");
        Assert.That(first.Snapshot(shared), Is.EqualTo(before));
    }

    [Test]
    public async Task Read_only_sources_can_build_with_external_artifact_directories()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Unix directory permissions are used to make this checkout read-only.");
            return;
        }
        using var consumer = CreateConsumer();
        var mode = File.GetUnixFileMode(consumer.ProjectDirectory);
        try
        {
            File.SetUnixFileMode(consumer.ProjectDirectory,
                UnixFileMode.UserRead | UnixFileMode.UserExecute | UnixFileMode.GroupRead |
                UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            var snapshot = Path.Combine(consumer.Root, "snapshot");
            AssertSucceeded(await consumer.Run("build",
                "-p:BaseIntermediateOutputPath=" + Path.Combine(consumer.Root, "restore") + Path.DirectorySeparatorChar,
                "-p:IntermediateOutputPath=" + Path.Combine(consumer.Root, "compile") + Path.DirectorySeparatorChar,
                "-p:OutputPath=" + Path.Combine(consumer.Root, "bin") + Path.DirectorySeparatorChar,
                "-p:CompilerGeneratedFilesOutputPath=" + Path.Combine(consumer.Root, "generated"),
                "-p:MorphantGitSnapshotPath=" + snapshot));
            Assert.That(consumer.Snapshot(snapshot).Keys, Is.EqualTo(new[] { Path.Combine("net10.0", MapperFile) }));
            Assert.That(Directory.GetDirectories(consumer.ProjectDirectory), Is.Empty);
        }
        finally { File.SetUnixFileMode(consumer.ProjectDirectory, mode); }
    }

    [Test]
    public async Task An_empty_global_hook_list_still_publishes_the_snapshot()
    {
        using var consumer = CreateConsumer();
        consumer.SetProperty("TargetsTriggeredByCompilation", "MustNotRun");
        AssertSucceeded(await consumer.Run("build", "-p:TargetsTriggeredByCompilation="));
        AssertMapperSnapshot(consumer);
    }

    private Consumer CreateConsumer(string directoryName = "consumer", string targetFramework = "net10.0") =>
        new(packages.PackageFeed, packageVersion, directoryName, targetFramework);

    private static void AssertMapperSnapshot(Consumer consumer, string framework = "net10.0") =>
        Assert.That(consumer.Snapshot().Keys, Is.EqualTo(new[] { Path.Combine(framework, MapperFile) }));

    private static void AssertSucceeded(ProcessResult result) =>
        Assert.That(result.ExitCode, Is.Zero, result.Command + Environment.NewLine + result.Output);

    private static void AssertRejected(ProcessResult result, string code)
    {
        Assert.That(result.ExitCode, Is.Not.Zero, result.Command + Environment.NewLine + result.Output);
        Assert.That(result.Output, Does.Contain("error " + code + ":"), result.Command + Environment.NewLine + result.Output);
    }

    private sealed class Consumer : IDisposable
    {
        private readonly string packageFeed;
        public string Root { get; } = Path.Combine(Path.GetTempPath(), nameof(CiBuildTests), Guid.NewGuid().ToString("N"));
        public string ProjectDirectory { get; }
        public string ProjectPath { get; set; }
        public string SnapshotRoot => Path.Combine(ProjectDirectory, "Generated", "Morphant");

        public Consumer(string packageFeed, string version, string directoryName, string targetFramework)
        {
            this.packageFeed = packageFeed;
            ProjectDirectory = Path.Combine(Root, directoryName);
            ProjectPath = Path.Combine(ProjectDirectory, "CiConsumer.csproj");
            Directory.CreateDirectory(ProjectDirectory);
            var project = XDocument.Parse(ProjectSource);
            var framework = project.Root!.Element("PropertyGroup")!.Element("TargetFramework")!;
            framework.Name = targetFramework.Contains(';') ? "TargetFrameworks" : "TargetFramework";
            framework.Value = targetFramework;
            project.Descendants("PackageReference").Single().SetAttributeValue("Version", version);
            project.Save(ProjectPath);
            File.WriteAllText(Path.Combine(ProjectDirectory, "Mapping.cs"), MappingSource);
        }

        public Task<ProcessResult> Run(string command, params string[] arguments) => DotNetCli.Run(
            IntegrationTestEnvironment.RepositoryRoot,
            [
                command, ProjectPath, "-p:Configuration=Release", "-m:1", "-nodeReuse:false",
                "-p:UseSharedCompilation=false", "-p:ContinuousIntegrationBuild=true",
                $"-p:RestoreSources={packageFeed}", "-p:NuGetAudit=false", .. arguments
            ]);

        public void AddReference(Consumer dependency)
        {
            var project = XDocument.Load(ProjectPath);
            project.Root!.Add(new XElement("ItemGroup",
                new XElement("ProjectReference", new XAttribute("Include", dependency.ProjectPath))));
            project.Save(ProjectPath);
        }

        public void SetProperty(string name, string value)
        {
            var project = XDocument.Load(ProjectPath);
            project.Root!.Element("PropertyGroup")!.Add(new XElement(name, value));
            project.Save(ProjectPath);
        }

        public Dictionary<string, string> Snapshot(string? root = null)
        {
            root ??= SnapshotRoot;
            return Directory.Exists(root)
                ? Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                    .Where(path => Path.GetFileName(path) != ".morphant").ToDictionary(
                    path => Path.GetRelativePath(root, path),
                    path => File.GetLastWriteTimeUtc(path).Ticks + ":" + Convert.ToBase64String(File.ReadAllBytes(path)),
                    StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private const string ProjectSource =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <PackageVersion>1.0.0-ci</PackageVersion>
            <LangVersion>9.0</LangVersion>
            <Nullable>enable</Nullable>
            <ImplicitUsings>disable</ImplicitUsings>
            <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
            <UseAppHost>false</UseAppHost>
            <MorphantGitSnapshot>true</MorphantGitSnapshot>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Morphant" Version="__VERSION__" />
          </ItemGroup>
          <Target Name="CiAfterCompile">
            <WriteLinesToFile File="$(IntermediateOutputPath)ci-after-compile.txt" Lines="executed" Overwrite="true" />
            <WriteLinesToFile File="$(IntermediateOutputPath)ci-registered-hooks.txt" Lines="$(TargetsTriggeredByCompilation)" Overwrite="true" />
          </Target>
        </Project>
        """;

    private const string MappingSource =
        """
        #nullable enable
        using Morphant;

        namespace CiConsumer
        {
            internal sealed class Source { public int Value { get; set; } }
            internal sealed class Destination { public int Value { get; set; } }

            [MorphantMapper]
            internal partial class TestMapper : TypeMapper<TestMapper>
            {
                protected override void Configure(MapperBuilder builder)
                {
                    builder.Map<Source, Destination>().Members((source, _) => new()
                    {
        #if DEBUG
                        Value = source.Value + 1
        #else
                        Value = source.Value + 2
        #endif
                    });
                }
            }
        }
        """;
}
