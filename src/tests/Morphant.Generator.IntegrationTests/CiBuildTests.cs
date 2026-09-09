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

    [TestCase("Mappers")]
    [TestCase("Full")]
    public async Task A_case_only_mapper_rename_updates_the_snapshot_and_allows_rebuilding(string detail)
    {
        using var consumer = CreateConsumer();
        consumer.SetProperty("MorphantGitSnapshotDetail", detail);
        AssertSucceeded(await consumer.Run("build"));
        File.WriteAllText(Path.Combine(consumer.ProjectDirectory, "Mapping.cs"),
            MappingSource.Replace("TestMapper", "TESTMapper"));

        AssertSucceeded(await consumer.Run("build", "--no-restore"));
        const string renamedMapper = "Morphant.Generated.TypeMapper.CiConsumer_TESTMapper.g.cs";
        string[] expected = detail == "Mappers" ? [renamedMapper] :
        [
            "Morphant.Generated.Construction.CiConsumer_Destination.g.cs",
            "Morphant.Generated.MappingExtension.CiConsumer_Source__CiConsumer_Destination__CiConsumer_TESTMapper.g.cs",
            "Morphant.Generated.Member.CiConsumer_Destination.g.cs",
            "Morphant.Generated.MemberExtension.CiConsumer_Source__CiConsumer_Destination__CiConsumer_TESTMapper.g.cs",
            renamedMapper
        ];
        Assert.That(consumer.Snapshot().Keys,
            Is.EquivalentTo(expected.Select(name => Path.Combine("net10.0", name))));
        foreach (var name in expected)
        {
            var compilerFile = Directory.GetFiles(Path.Combine(consumer.ProjectDirectory, "obj"),
                name, SearchOption.AllDirectories).Single();
            Assert.That(File.ReadAllBytes(Path.Combine(consumer.SnapshotRoot, "net10.0", name)),
                Is.EqualTo(File.ReadAllBytes(compilerFile)));
        }

        var snapshot = consumer.Snapshot();
        AssertSucceeded(await consumer.Run("build", "--no-restore", "-t:Rebuild"));
        Assert.That(consumer.Snapshot(), Is.EqualTo(snapshot));
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
    public async Task Parallel_configurations_with_separate_directories_publish_independent_snapshots()
    {
        using var consumer = CreateConsumer();
        AssertSucceeded(await consumer.Run("restore"));
        var debugSnapshot = Path.Combine(consumer.Root, "snapshots", "Debug");
        var releaseSnapshot = Path.Combine(consumer.Root, "snapshots", "Release");
        var builds = await Task.WhenAll(
            consumer.RunWithEnvironment("build", TemporaryEnvironment("debug-temp"), "--no-restore",
                "-p:Configuration=Debug", "-p:MorphantGitSnapshotPath=" + debugSnapshot),
            consumer.RunWithEnvironment("build", TemporaryEnvironment("release-temp"), "--no-restore",
                "-p:Configuration=Release", "-p:MorphantGitSnapshotPath=" + releaseSnapshot));
        foreach (var build in builds) AssertSucceeded(build);
        Assert.That(consumer.Snapshot(), Is.Empty);
        var debug = File.ReadAllBytes(Directory.GetFiles(
            Path.Combine(consumer.ProjectDirectory, "obj", "Debug"), MapperFile, SearchOption.AllDirectories).Single());
        var release = File.ReadAllBytes(Directory.GetFiles(
            Path.Combine(consumer.ProjectDirectory, "obj", "Release"), MapperFile, SearchOption.AllDirectories).Single());
        foreach (var root in new[] { debugSnapshot, releaseSnapshot })
            Assert.That(consumer.Snapshot(root).Keys, Is.EqualTo(new[] { Path.Combine("net10.0", MapperFile) }));

        Assert.Multiple(() =>
        {
            Assert.That(debug, Is.Not.EqualTo(release), "The two configurations must generate different mappings.");
            Assert.That(File.ReadAllBytes(Path.Combine(debugSnapshot, "net10.0", MapperFile)), Is.EqualTo(debug));
            Assert.That(File.ReadAllBytes(Path.Combine(releaseSnapshot, "net10.0", MapperFile)), Is.EqualTo(release));
        });

        Dictionary<string, string> TemporaryEnvironment(string name)
        {
            var directory = Path.Combine(consumer.Root, name);
            Directory.CreateDirectory(directory);
            return new() { ["TMPDIR"] = directory, ["TEMP"] = directory, ["TMP"] = directory, ["DOTNET_SYSTEM_IO_DISABLEFILELOCKING"] = "1" };
        }
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
    public async Task Parallel_frameworks_publish_independent_slices()
    {
        using var consumer = CreateConsumer(targetFramework: "netstandard2.0;net10.0");
        AssertSucceeded(await consumer.Run("restore"));
        var builds = await Task.WhenAll(new[] { "netstandard2.0", "net10.0" }.Select(framework =>
            consumer.Run("build", "--no-restore", "--framework", framework)));
        foreach (var build in builds) AssertSucceeded(build);
        Assert.That(consumer.Snapshot().Keys, Is.EquivalentTo(new[]
        {
            Path.Combine("netstandard2.0", MapperFile), Path.Combine("net10.0", MapperFile)
        }));
        foreach (var framework in new[] { "netstandard2.0", "net10.0" })
        {
            var compilerFile = Directory.GetFiles(Path.Combine(consumer.ProjectDirectory, "obj", "Release", framework),
                MapperFile, SearchOption.AllDirectories).Single();
            Assert.That(File.ReadAllBytes(Path.Combine(consumer.SnapshotRoot, framework, MapperFile)),
                Is.EqualTo(File.ReadAllBytes(compilerFile)));
        }
        var otherSlice = Path.Combine(consumer.SnapshotRoot, "netstandard2.0");
        var before = consumer.Snapshot(otherSlice);
        AssertSucceeded(await consumer.Run("build", "--no-restore", "-t:Rebuild", "--framework", "net10.0"));
        Assert.That(consumer.Snapshot(otherSlice), Is.EqualTo(before));
    }

    [TestCase("netstandard2.0;net10.0")]
    [TestCase("net10.0;netstandard2.0")]
    public async Task Building_one_framework_publishes_it_regardless_of_declaration_order(string frameworks)
    {
        using var consumer = CreateConsumer(targetFramework: frameworks);
        File.WriteAllText(Path.Combine(consumer.ProjectDirectory, "Mapping.cs"),
            MappingSource.Replace("#if DEBUG", "#if NET10_0_OR_GREATER"));
        var declared = frameworks.Split(';');
        AssertSucceeded(await consumer.Run("build", "--framework", declared[0]));
        AssertMapperSnapshot(consumer, declared[0]);
        var first = consumer.Snapshot();

        AssertSucceeded(await consumer.Run("build", "--framework", declared[1], "--no-restore"));
        var both = consumer.Snapshot();
        Assert.That(both.Keys, Is.EquivalentTo(declared.Select(framework => Path.Combine(framework, MapperFile))));
        Assert.That(both[Path.Combine(declared[0], MapperFile)], Is.EqualTo(first.Values.Single()));
        foreach (var framework in declared)
        {
            var compilerFile = Directory.GetFiles(Path.Combine(consumer.ProjectDirectory, "obj", "Release", framework),
                MapperFile, SearchOption.AllDirectories).Single();
            Assert.That(File.ReadAllBytes(Path.Combine(consumer.SnapshotRoot, framework, MapperFile)),
                Is.EqualTo(File.ReadAllBytes(compilerFile)));
        }
        Assert.That(File.ReadAllBytes(Path.Combine(consumer.SnapshotRoot, declared[0], MapperFile)),
            Is.Not.EqualTo(File.ReadAllBytes(Path.Combine(consumer.SnapshotRoot, declared[1], MapperFile))));
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

    [Test]
    public async Task Build_output_distinguishes_publication_unchanged_output_and_skipped_compilation()
    {
        using var consumer = CreateConsumer();
        var slice = Path.Combine(consumer.SnapshotRoot, "net10.0");
        var build = await consumer.Run("build");
        AssertSucceeded(build);
        Assert.That(build.Output, Does.Contain($"Morphant Git snapshot '{slice}': 1 updated, 0 removed, 0 unchanged."));
        Assert.That(build.Output, Does.Not.Contain("was not updated because compilation was skipped"));
        var before = consumer.Snapshot();

        var skipped = await consumer.Run("build", "--no-restore");
        AssertSucceeded(skipped);
        Assert.That(skipped.Output, Does.Contain(
            $"Morphant Git snapshot '{slice}' was not updated because compilation was skipped. Run a rebuild (-t:Rebuild) to refresh it."));
        Assert.That(consumer.Snapshot(), Is.EqualTo(before));

        File.AppendAllText(Path.Combine(consumer.ProjectDirectory, "Mapping.cs"), "\n// Recompile without changing generated output.\n");
        var unchanged = await consumer.Run("build", "--no-restore");
        AssertSucceeded(unchanged);
        Assert.That(unchanged.Output, Does.Contain($"Morphant Git snapshot '{slice}': 0 updated, 0 removed, 1 unchanged."));
        Assert.That(unchanged.Output, Does.Not.Contain("was not updated because compilation was skipped"));
        Assert.That(consumer.Snapshot(), Is.EqualTo(before));
    }

    [Test]
    public async Task A_conditionally_disabled_framework_does_not_validate_or_modify_snapshot_storage()
    {
        using var consumer = CreateConsumer(targetFramework: "netstandard2.0;net10.0");
        AssertSucceeded(await consumer.Run("build"));
        var before = consumer.Snapshot();
        var owners = Directory.GetDirectories(consumer.Root, ".morphant", SearchOption.AllDirectories);
        consumer.SetProperty("MorphantGitSnapshot", "false", "'$(TargetFramework)' == 'netstandard2.0'");
        File.WriteAllText(Path.Combine(consumer.ProjectDirectory, "Mapping.cs"),
            MappingSource.Replace("source.Value + 2", "source.Value + 3"));

        AssertSucceeded(await consumer.Run("build", "--framework", "netstandard2.0", "--no-restore", "-t:Rebuild",
            "-p:EmitCompilerGeneratedFiles=false",
            "-p:MorphantGitSnapshotPath=" + consumer.ProjectDirectory));
        Assert.That(consumer.Snapshot(), Is.EqualTo(before));
        Assert.That(Directory.GetDirectories(consumer.Root, ".morphant", SearchOption.AllDirectories), Is.EquivalentTo(owners));
    }

    [TestCase("DesignTimeBuild", "true")]
    [TestCase("SkipCompilerExecution", "true")]
    [TestCase("BuildingProject", "false")]
    public async Task Nonpublishing_compile_modes_preserve_the_snapshot(string property, string value)
    {
        using var consumer = CreateConsumer();
        AssertSucceeded(await consumer.Run("build"));
        var before = consumer.Snapshot();
        var owner = File.ReadAllBytes(Path.Combine(consumer.SnapshotRoot, ".morphant", "owner"));

        AssertSucceeded(await consumer.Run("msbuild", "-t:Compile", $"-p:{property}={value}",
            "-p:EmitCompilerGeneratedFiles=false",
            "-p:MorphantGitSnapshotPath=" + consumer.ProjectDirectory));
        Assert.That(consumer.Snapshot(), Is.EqualTo(before));
        Assert.That(File.ReadAllBytes(Path.Combine(consumer.SnapshotRoot, ".morphant", "owner")), Is.EqualTo(owner));
        Assert.That(Directory.GetFileSystemEntries(consumer.ProjectDirectory, ".morphant*"), Is.Empty);
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
    public async Task Global_snapshot_enablement_updates_each_referenced_project_framework()
    {
        using var consumer = CreateConsumer();
        using var dependency = CreateConsumer(targetFramework: "netstandard2.0");
        dependency.SetProperty("AssemblyName", "CiDependency");
        consumer.AddReference(dependency);
        AssertSucceeded(await consumer.Run("build"));
        var consumerSnapshot = consumer.Snapshot();
        var dependencySnapshot = dependency.Snapshot();
        File.WriteAllText(Path.Combine(dependency.SnapshotRoot, "netstandard2.0", "Morphant.Generated.TypeMapper.Stale.g.cs"), "// stale");
        const string enable = "-p:MorphantGitSnapshot=true";

        var result = await consumer.Run("msbuild", "-restore", "-graphBuild", "-m:2", "-t:Rebuild", enable);
        AssertSucceeded(result);
        Assert.That(consumer.Snapshot(), Is.EqualTo(consumerSnapshot));
        Assert.That(dependency.Snapshot(), Is.EqualTo(dependencySnapshot));
    }

    [TestCase("netstandard2.0")]
    [TestCase("net10.0")]
    public async Task Conditional_enablement_and_global_overrides_apply_to_each_framework(string enabled)
    {
        using var consumer = CreateConsumer(targetFramework: "netstandard2.0;net10.0");
        consumer.SetProperty("MorphantGitSnapshot", "false");
        consumer.SetProperty("MorphantGitSnapshot", "true", $"'$(TargetFramework)' == '{enabled}'");
        AssertSucceeded(await consumer.Run("build"));
        AssertMapperSnapshot(consumer, enabled);

        AssertSucceeded(await consumer.Run("build", "--no-restore", "-t:Rebuild", "-p:MorphantGitSnapshot=true"));
        Assert.That(consumer.Snapshot().Keys, Is.EquivalentTo(new[]
        {
            Path.Combine("netstandard2.0", MapperFile), Path.Combine("net10.0", MapperFile)
        }));
        var before = consumer.Snapshot();
        File.WriteAllText(Path.Combine(consumer.ProjectDirectory, "Mapping.cs"),
            MappingSource.Replace("source.Value + 2", "source.Value + 3"));
        AssertSucceeded(await consumer.Run("build", "--no-restore", "-p:MorphantGitSnapshot=false"));
        Assert.That(consumer.Snapshot(), Is.EqualTo(before));
    }

    [TestCase("Checkout")]
    [TestCase("Compiler")]
    [TestCase("Snapshot")]
    public async Task Linked_storage_paths_update_the_same_snapshot(string location)
    {
        using var consumer = CreateConsumer();
        var compiler = Path.Combine(consumer.Root, "compiler");
        consumer.SetProperty("EmitCompilerGeneratedFiles", "true");
        consumer.SetProperty("CompilerGeneratedFilesOutputPath", compiler);
        AssertSucceeded(await consumer.Run("build"));
        var snapshot = consumer.Snapshot();
        var owner = File.ReadAllBytes(Path.Combine(consumer.SnapshotRoot, ".morphant", "owner"));
        var linkedDirectory = Path.Combine(consumer.Root, "linked-storage");
        var target = location switch
        {
            "Checkout" => consumer.ProjectDirectory,
            "Compiler" => compiler,
            _ => consumer.SnapshotRoot
        };
        if (OperatingSystem.IsWindows())
        {
            var start = new System.Diagnostics.ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var argument in new[] { "/c", "mklink", "/J", linkedDirectory, target })
                start.ArgumentList.Add(argument);
            using var process = System.Diagnostics.Process.Start(start)!;
            await process.WaitForExitAsync();
            Assert.That(process.ExitCode, Is.Zero, await process.StandardError.ReadToEndAsync());
        }
        else
            Directory.CreateSymbolicLink(linkedDirectory, target);
        try
        {
            if (location == "Checkout")
                consumer.ProjectPath = Path.Combine(linkedDirectory, "CiConsumer.csproj");
            else
                consumer.SetProperty(location == "Compiler" ? "CompilerGeneratedFilesOutputPath" : "MorphantGitSnapshotPath", linkedDirectory);
            AssertSucceeded(await consumer.Run("build", "-t:Rebuild"));
            Assert.That(consumer.Snapshot(), Is.EqualTo(snapshot));
            Assert.That(File.ReadAllBytes(Path.Combine(consumer.SnapshotRoot, ".morphant", "owner")), Is.EqualTo(owner));
        }
        finally
        {
            // Remove the alias before recursive cleanup removes its target.
            Directory.Delete(linkedDirectory);
        }
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
            "GlobalWithExistingPublication" => ["-p:TargetsTriggeredByCompilation=\"CiAfterCompile;PublishMorphantGitSnapshot\""],
            _ when origin.StartsWith("Global", StringComparison.Ordinal) => ["-p:TargetsTriggeredByCompilation=CiAfterCompile"],
            _ => []
        };
        var firstBuild = await consumer.Run("build", settings);
        AssertSucceeded(firstBuild);
        Assert.That(firstBuild.Output, Does.Not.Contain("was not updated because compilation was skipped"));
        AssertMapperSnapshot(consumer);
        var hook = Path.Combine(consumer.ProjectDirectory, "obj", "Release", "net10.0", "ci-after-compile.txt");
        Assert.That(File.ReadAllText(hook).Trim(), Is.EqualTo("executed"));
        Assert.That(File.ReadAllLines(Path.Combine(consumer.ProjectDirectory, "obj", "Release", "net10.0", "ci-registered-hooks.txt")),
            Is.EqualTo(new[] { "CiAfterCompile", "PublishMorphantGitSnapshot" }));
        if (origin.StartsWith("Global", StringComparison.Ordinal))
            Assert.That(File.ReadAllText(Path.Combine(dependency.ProjectDirectory, "obj", "Release", "netstandard2.0", "ci-after-compile.txt")).Trim(), Is.EqualTo("executed"));
        var before = consumer.Snapshot();
        File.Delete(hook);
        var skippedBuild = await consumer.Run("build", [.. settings, "--no-restore"]);
        AssertSucceeded(skippedBuild);
        Assert.That(skippedBuild.Output, Does.Contain(
            $"Morphant Git snapshot '{Path.Combine(consumer.SnapshotRoot, "net10.0")}' was not updated because compilation was skipped."));
        Assert.That(skippedBuild.Output, Does.Contain(
            $"Morphant Git snapshot '{Path.Combine(dependency.SnapshotRoot, "netstandard2.0")}' was not updated because compilation was skipped."));
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
    [TestCase("Snapshot", "Generated$(literal)@(items)%(metadata)'CI")]
    [TestCase("Compiler", "Generated$(literal)@(items)%(metadata)'CI")]
    [TestCase("Snapshot", "Generated*?CI")]
    [TestCase("Compiler", "Generated*?CI")]
    [TestCase("Snapshot", "Backslash")]
    [TestCase("Compiler", "Backslash")]
    public async Task Escaped_literal_storage_paths_are_preserved(string location, string name)
    {
        if (OperatingSystem.IsWindows() && name.Contains('*'))
            Assert.Ignore("Windows does not support literal wildcard characters in directory names.");
        using var consumer = CreateConsumer();
        var output = Path.Combine(consumer.Root, name);
        var escaped = output.Replace("%", "%25").Replace(";", "%3B")
            .Replace("$", "%24").Replace("@", "%40").Replace("(", "%28")
            .Replace(")", "%29").Replace("'", "%27").Replace("*", "%2A").Replace("?", "%3F");
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
    public async Task Parallel_projects_cannot_claim_the_same_empty_snapshot_directory()
    {
        using var first = CreateConsumer();
        using var second = CreateConsumer();
        var shared = Path.Combine(first.Root, "shared-snapshot");
        first.SetProperty("MorphantGitSnapshotPath", shared);
        second.SetProperty("MorphantGitSnapshotPath", shared);
        File.WriteAllText(Path.Combine(second.ProjectDirectory, "Mapping.cs"),
            MappingSource.Replace("source.Value + 2", "source.Value + 3"));
        foreach (var project in new[] { first, second }) AssertSucceeded(await project.Run("restore"));
        var results = await Task.WhenAll(first.Run("build", "--no-restore"), second.Run("build", "--no-restore"));
        Assert.That(results.Count(result => result.ExitCode == 0), Is.EqualTo(1),
            string.Join("\n", results.Select(result => result.Output)));
        var winner = results[0].ExitCode == 0 ? first : second;
        AssertRejected(results.Single(result => result.ExitCode != 0), "MORPHANTMSB005");
        Assert.That(first.Snapshot(shared).Keys, Is.EqualTo(new[] { Path.Combine("net10.0", MapperFile) }));
        var compilerFile = Directory.GetFiles(Path.Combine(winner.ProjectDirectory, "obj"),
            MapperFile, SearchOption.AllDirectories).Single();
        Assert.That(File.ReadAllBytes(Path.Combine(shared, "net10.0", MapperFile)), Is.EqualTo(File.ReadAllBytes(compilerFile)));
        Assert.That(File.ReadAllText(Path.Combine(shared, ".morphant", "owner")), Is.EqualTo(
            "Morphant Git snapshot 1\r\n" + Path.GetRelativePath(shared, winner.ProjectPath).Replace('\\', '/') + "\r\n"));
        Assert.That(Directory.GetFileSystemEntries(shared, ".morphant*"), Is.EqualTo(new[] { Path.Combine(shared, ".morphant") }));
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

    [Test]
    public async Task Existing_read_only_snapshot_ownership_allows_updates()
    {
        using var consumer = CreateConsumer();
        var compiler = Path.Combine(consumer.Root, "compiler");
        consumer.SetProperty("EmitCompilerGeneratedFiles", "true");
        consumer.SetProperty("CompilerGeneratedFilesOutputPath", compiler);
        AssertSucceeded(await consumer.Run("build"));
        var before = consumer.Snapshot();
        var owners = new[] { Path.Combine(consumer.SnapshotRoot, ".morphant", "owner") };
        var attributes = owners.Select(File.GetAttributes).ToArray();
        var contents = owners.Select(File.ReadAllBytes).ToArray();
        try
        {
            for (var i = 0; i < owners.Length; i++)
                File.SetAttributes(owners[i], attributes[i] | FileAttributes.ReadOnly);
            File.WriteAllText(Path.Combine(consumer.ProjectDirectory, "Mapping.cs"),
                MappingSource.Replace("source.Value + 2", "source.Value + 3"));
            AssertSucceeded(await consumer.Run("build", "--no-restore"));
            AssertMapperSnapshot(consumer);
            Assert.That(consumer.Snapshot(), Is.Not.EqualTo(before));
            Assert.That(File.ReadAllBytes(Path.Combine(consumer.SnapshotRoot, "net10.0", MapperFile)),
                Is.EqualTo(File.ReadAllBytes(Directory.GetFiles(compiler, MapperFile, SearchOption.AllDirectories).Single())));
            for (var i = 0; i < owners.Length; i++)
            {
                Assert.That(File.ReadAllBytes(owners[i]), Is.EqualTo(contents[i]));
                Assert.That(File.GetAttributes(owners[i]) & FileAttributes.ReadOnly, Is.EqualTo(FileAttributes.ReadOnly));
            }
        }
        finally
        {
            for (var i = 0; i < owners.Length; i++)
                File.SetAttributes(owners[i], attributes[i]);
        }
    }

    [TestCase("Configuration")]
    [TestCase("Framework")]
    [TestCase("Runtime")]
    public async Task Compiler_storage_can_be_reused_sequentially(string difference)
    {
        using var consumer = CreateConsumer(targetFramework: difference == "Framework" ? "netstandard2.0;net10.0" : "net10.0");
        var compiler = Path.Combine(consumer.Root, "compiler");
        consumer.SetProperty("EmitCompilerGeneratedFiles", "true");
        consumer.SetProperty("CompilerGeneratedFilesOutputPath", compiler);
        AssertSucceeded(await consumer.Run("build", "--framework", "net10.0"));
        var before = consumer.Snapshot();
        var stale = Path.Combine(compiler, "Morphant.Generated.TypeMapper.Stale.g.cs");
        File.WriteAllText(stale, "// stale");
        File.WriteAllText(Path.Combine(consumer.ProjectDirectory, "Mapping.cs"),
            MappingSource.Replace("source.Value + 2", "source.Value + 3"));

        string[] arguments = difference switch
        {
            "Configuration" => ["--no-restore", "-p:Configuration=Debug"],
            "Framework" => ["--no-restore", "--framework", "netstandard2.0"],
            _ => ["--runtime", RuntimeInformation.RuntimeIdentifier, "-p:SelfContained=false"]
        };
        AssertSucceeded(await consumer.Run("build", arguments));
        var framework = difference == "Framework" ? "netstandard2.0" : "net10.0";
        var after = consumer.Snapshot();
        Assert.That(after.Keys, Is.EquivalentTo(difference == "Framework"
            ? new[] { Path.Combine("net10.0", MapperFile), Path.Combine("netstandard2.0", MapperFile) }
            : new[] { Path.Combine("net10.0", MapperFile) }));
        if (difference == "Framework")
            Assert.That(after[Path.Combine("net10.0", MapperFile)], Is.EqualTo(before[Path.Combine("net10.0", MapperFile)]));
        else
            Assert.That(after, Is.Not.EqualTo(before));
        Assert.That(File.ReadAllBytes(Path.Combine(consumer.SnapshotRoot, framework, MapperFile)),
            Is.EqualTo(File.ReadAllBytes(Directory.GetFiles(compiler, MapperFile, SearchOption.AllDirectories).Single())));
        Assert.That(File.Exists(stale), Is.False);
        Assert.That(Directory.GetFileSystemEntries(compiler, ".morphant*"), Is.Empty);
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
        public string Root { get; } = Path.Combine(TemporaryDirectory(), nameof(CiBuildTests), Guid.NewGuid().ToString("N"));
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

        public Task<ProcessResult> Run(string command, params string[] arguments) =>
            RunWithEnvironment(command, null, arguments);

        public Task<ProcessResult> RunWithEnvironment(string command,
            IReadOnlyDictionary<string, string>? environment, params string[] arguments) => DotNetCli.Run(
            IntegrationTestEnvironment.RepositoryRoot,
            [
                command, ProjectPath, "-p:Configuration=Release", "-m:1", "-nodeReuse:false",
                "-p:UseSharedCompilation=false", "-p:ContinuousIntegrationBuild=true",
                $"-p:RestoreSources={packageFeed}", "-p:NuGetAudit=false", .. arguments
            ], environment);

        public void AddReference(Consumer dependency)
        {
            var project = XDocument.Load(ProjectPath);
            project.Root!.Add(new XElement("ItemGroup",
                new XElement("ProjectReference", new XAttribute("Include", dependency.ProjectPath))));
            project.Save(ProjectPath);
        }

        public void SetProperty(string name, string value, string? condition = null)
        {
            var project = XDocument.Load(ProjectPath);
            var property = new XElement(name, value);
            if (condition is not null) property.SetAttributeValue("Condition", condition);
            project.Root!.Element("PropertyGroup")!.Add(property);
            project.Save(ProjectPath);
        }

        public Dictionary<string, string> Snapshot(string? root = null)
        {
            root ??= SnapshotRoot;
            return Directory.Exists(root)
                ? Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                    .Where(path => Path.GetRelativePath(root, path) != Path.Combine(".morphant", "owner")).ToDictionary(
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
