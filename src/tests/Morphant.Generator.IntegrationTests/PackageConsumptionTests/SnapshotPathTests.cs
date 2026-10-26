using System.Xml.Linq;

namespace Morphant.Generator.IntegrationTests.PackageConsumptionTests;

internal sealed partial class PackageConsumptionTests
{
    [Test]
    public async Task Custom_compiler_output_preserves_snapshot_lifecycle()
    {
        await CreateSnapshot();

        var originalProject = await File.ReadAllTextAsync(ConsumerProject);
        var originalSource = await File.ReadAllTextAsync(ConsumerSource);
        var expectedSnapshot = SnapshotContents(GeneratedDirectory);
        var expectedWriteTimes = SnapshotWriteTimes(GeneratedDirectory);
        var projectOutput = Path.Combine(CurrentIntermediate, "Custom Generated", "Project");

        try
        {
            foreach (var useGlobalProperty in new[] { false, true })
            {
                var project = XDocument.Parse(originalProject);
                project.Root!.AddFirst(new XElement("PropertyGroup",
                    new XElement("EmitCompilerGeneratedFiles", "true"),
                    new XElement("CompilerGeneratedFilesOutputPath", projectOutput)));
                await File.WriteAllTextAsync(ConsumerProject, project.ToString());

                var output = useGlobalProperty
                    ? Path.Combine(CurrentIntermediate, "Custom Generated", "Global")
                    : projectOutput;
                var configuredPath = useGlobalProperty
                    ? Path.GetRelativePath(Path.GetDirectoryName(ConsumerProject)!, output)
                    : output;
                string[] arguments =
                [
                    .. ConsumerArguments,
                    $"-p:MorphantTestExpectedCompilerOutputPath={configuredPath}",
                    .. useGlobalProperty
                        ? new[] { $"-p:CompilerGeneratedFilesOutputPath={configuredPath}" }
                        : Array.Empty<string>()
                ];
                Directory.CreateDirectory(output);
                var stale = Path.Combine(output, "Morphant.Generated.TypeMapper.Stale.g.cs");
                var foreign = Path.Combine(output, "Other.Generator.Output.g.cs");
                await File.WriteAllTextAsync(stale, "// stale\r\n");
                await File.WriteAllTextAsync(foreign, "// foreign\r\n");

                var designTimeBuild = await DotNetCli.Run(RepositoryRoot,
                    [.. BuildArguments(arguments, ConsumerProject), "-p:DesignTimeBuild=true"]);
                AssertSucceeded(designTimeBuild);
                Assert.Multiple(() =>
                {
                    Assert.That(File.ReadAllText(stale), Is.EqualTo("// stale\r\n"));
                    Assert.That(SnapshotContents(GeneratedDirectory), Is.EqualTo(expectedSnapshot));
                    Assert.That(SnapshotWriteTimes(GeneratedDirectory), Is.EqualTo(expectedWriteTimes));
                });

                var rebuilt = await DotNetCli.Run(RepositoryRoot,
                    RebuildArguments(arguments, ConsumerProject));
                AssertSucceeded(rebuilt);
                Assert.Multiple(() =>
                {
                    Assert.That(File.Exists(stale), Is.False);
                    Assert.That(File.ReadAllText(foreign), Is.EqualTo("// foreign\r\n"));
                    Assert.That(Directory.GetFiles(output, "Morphant.Generated.*.g.cs", SearchOption.AllDirectories)
                            .Select(Path.GetFileName).Order(StringComparer.Ordinal),
                        Is.EqualTo(PrimaryFullGeneratedFiles.Order(StringComparer.Ordinal)),
                        "Roslyn must emit into the configured path, including when a global property overrides the project.");
                    Assert.That(SnapshotContents(GeneratedDirectory), Is.EqualTo(expectedSnapshot));
                    Assert.That(SnapshotWriteTimes(GeneratedDirectory), Is.EqualTo(expectedWriteTimes));
                });

                await File.WriteAllTextAsync(ConsumerSource, BrokenConsumerSource);
                var failed = await DotNetCli.Run(RepositoryRoot,
                    BuildArguments(arguments, ConsumerProject));
                AssertFailed(failed);
                Assert.Multiple(() =>
                {
                    Assert.That(SnapshotContents(GeneratedDirectory), Is.EqualTo(expectedSnapshot));
                    Assert.That(SnapshotWriteTimes(GeneratedDirectory), Is.EqualTo(expectedWriteTimes));
                    Assert.That(File.ReadAllText(foreign), Is.EqualTo("// foreign\r\n"));
                });

                await File.WriteAllTextAsync(ConsumerSource, originalSource);
                var recovered = await DotNetCli.Run(RepositoryRoot,
                    BuildArguments(arguments, ConsumerProject));
                AssertSucceeded(recovered);
                Assert.That(SnapshotContents(GeneratedDirectory), Is.EqualTo(expectedSnapshot));
                Assert.That(SnapshotWriteTimes(GeneratedDirectory), Is.EqualTo(expectedWriteTimes));
            }

            var sdkProject = XDocument.Parse(originalProject);
            sdkProject.Root!.AddFirst(new XElement("PropertyGroup",
                new XElement("EmitCompilerGeneratedFiles", "true")));
            await File.WriteAllTextAsync(ConsumerProject, sdkProject.ToString());
            var sdkDefaultBuild = await DotNetCli.Run(RepositoryRoot,
                RebuildArguments(ConsumerArguments, ConsumerProject));
            AssertSucceeded(sdkDefaultBuild);
            Assert.That(Directory.GetFiles(Path.Combine(CurrentIntermediate, "generated"),
                        "Morphant.Generated.*.g.cs", SearchOption.AllDirectories)
                    .Select(Path.GetFileName).Order(StringComparer.Ordinal),
                Is.EqualTo(PrimaryFullGeneratedFiles.Order(StringComparer.Ordinal)),
                "The SDK's generated-files path must also be preserved when emission is already enabled.");
            Assert.That(SnapshotContents(GeneratedDirectory), Is.EqualTo(expectedSnapshot));
            Assert.That(SnapshotWriteTimes(GeneratedDirectory), Is.EqualTo(expectedWriteTimes));
        }
        finally
        {
            await File.WriteAllTextAsync(ConsumerProject, originalProject);
            await File.WriteAllTextAsync(ConsumerSource, originalSource);
        }
    }

    [Test]
    public async Task Unsafe_configuration_fails_before_mutating_snapshot()
    {
        await CreateSnapshot();

        var expected = SnapshotContents(GeneratedDirectory);
        var unsafeCases = new[]
        {
            ("-p:MorphantGitSnapshot=invalid", "MORPHANTMSB022"),
            (
                $"-p:MorphantGitSnapshotPath={ConsumerDirectory}",
                "MORPHANTMSB005"),
            (
                "-p:MorphantGitSnapshotPath=" +
                Path.Combine(ConsumerDirectory, "Generated", "One") + ";" +
                Path.Combine(ConsumerDirectory, "Generated", "Two"),
                "MSB1006"),
            (
                $"-p:CompilerGeneratedFilesOutputPath={GeneratedDirectory}",
                "MORPHANTMSB003"),
            (
                $"-p:CompilerGeneratedFilesOutputPath={ConsumerDirectory}",
                "MORPHANTMSB004"),
            ("-p:CompilerGeneratedFilesOutputPath=", "MORPHANTMSB006"),
            ("-p:EmitCompilerGeneratedFiles=false", "MORPHANTMSB002"),
            (
                "-p:MorphantGitSnapshotDetail=Everything",
                "MORPHANTMSB020")
        };

        foreach (var (property, expectedCode) in unsafeCases)
        {
            var result = await DotNetCli.Run(
                RepositoryRoot,
                [
                    .. BuildArguments(ConsumerArguments, ConsumerProject),
                    property
                ]);
            AssertFailed(result);
            if (expectedCode == "MORPHANTMSB022")
            {
                Assert.That(result.Output, Does.Contain(
                    "error MORPHANTMSB022: MorphantGitSnapshot must be true or false. The effective value is 'invalid'."));
            }
            if (expectedCode == "MORPHANTMSB020")
            {
                Assert.That(result.Output, Does.Contain(
                    "error MORPHANTMSB020: MorphantGitSnapshotDetail must be Mappers or Full. The effective value is 'Everything'."));
            }
            Assert.That(
                result.Output,
                Does.Contain(expectedCode),
                $"Unsafe configuration '{property}' failed without the " +
                "expected actionable diagnostic.");
            Assert.That(
                SnapshotContents(GeneratedDirectory),
                Is.EqualTo(expected),
                $"Unsafe configuration '{property}' mutated the last " +
                "successful snapshot.");
        }
    }

    [Test]
    public async Task Destination_directory_collision_fails_atomically_and_recovers()
    {
        await CreateSnapshot();

        var collision = Path.Combine(
            GeneratedDirectory,
            MapperGeneratedFiles[0]);
        File.Delete(collision);
        Directory.CreateDirectory(collision);
        var expectedFailureState = SnapshotTree(GeneratedDirectory);

        var failedPublication = await DotNetCli.Run(
            RepositoryRoot,
            RebuildArguments(ConsumerArguments, ConsumerProject));
        AssertFailed(failedPublication);
        Assert.That(failedPublication.Output, Does.Contain("MORPHANTMSB015"));
        Assert.That(
            SnapshotTree(GeneratedDirectory),
            Is.EqualTo(expectedFailureState),
            "A publication failure must not leave a partially replaced " +
            "snapshot.");

        Directory.Delete(collision);
        var repairedPublication = await DotNetCli.Run(
            RepositoryRoot,
            RebuildArguments(ConsumerArguments, ConsumerProject));
        AssertSucceeded(repairedPublication);
        AssertGeneratedFileSet(GeneratedDirectory, MapperGeneratedFiles);
    }

    [Test]
    public async Task Disabling_or_moving_snapshot_keeps_compilation_safe()
    {
        await CreateSnapshot();

        var disabledRun = await DotNetCli.Run(
            RepositoryRoot,
            BaseConsumerArguments);
        AssertSucceeded(disabledRun);

        var alternativeRoot = Path.Combine(
            ConsumerDirectory,
            "Generated",
            "Alternative");
        var changedPathRun = await DotNetCli.Run(
            RepositoryRoot,
            RebuildArguments(
                [
                    .. BaseConsumerArguments,
                    "-p:MorphantGitSnapshot=true",
                    $"-p:MorphantGitSnapshotPath={alternativeRoot}"
                ],
                ConsumerProject));
        AssertSucceeded(changedPathRun);
        AssertGeneratedFileSet(
            Path.Combine(
                alternativeRoot,
                "net10.0"),
            MapperGeneratedFiles);
        Assert.That(
            Directory.Exists(ConsumerGenerated),
            Is.True,
            "Changing the path must not silently delete a committed old " +
            "snapshot; its reserved files remain excluded from Compile.");
    }



}
