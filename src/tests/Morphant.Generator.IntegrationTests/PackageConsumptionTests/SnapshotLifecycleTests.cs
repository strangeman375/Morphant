using System.Xml.Linq;

namespace Morphant.Generator.IntegrationTests.PackageConsumptionTests;

internal sealed partial class PackageConsumptionTests
{
    [Test]
    public async Task Snapshots_are_opt_in_and_follow_mapping_and_detail_changes()
    {
        var snapshotDisabledRun = await DotNetCli.Run(
            RepositoryRoot,
            BaseConsumerArguments);
        AssertSucceeded(snapshotDisabledRun);
        Assert.That(
            Directory.Exists(ConsumerGenerated),
            Is.False,
            "Git snapshots must remain opt-in.");

        Directory.CreateDirectory(GeneratedDirectory);
        await File.WriteAllTextAsync(
            StaleMorphantFile,
            "// stale Morphant output");
        await File.WriteAllTextAsync(
            UnrelatedGeneratedFile,
            "// unrelated generator output");

        var run = await DotNetCli.Run(
            RepositoryRoot,
            RebuildArguments(ConsumerArguments, ConsumerProject));
        AssertSucceeded(run);

        Assert.Multiple(() =>
        {
            Assert.That(
                File.Exists(StaleMorphantFile),
                Is.False,
                "The package must remove stale Morphant snapshot " +
                "files after successful compilation.");
            Assert.That(
                File.Exists(UnrelatedGeneratedFile),
                Is.True,
                "Morphant cleanup must preserve other generators' " +
                "output.");
        });
        AssertGeneratedFileSet(
            GeneratedDirectory,
            MapperGeneratedFiles);

        string[] fullConsumerArguments =
        [
            .. ConsumerArguments,
            "-p:MorphantGitSnapshotDetail=Full"
        ];

        await File.WriteAllTextAsync(
            ConsumerSource,
            SecondMappingConsumerSource);
        var addedMappingRun = await DotNetCli.Run(
            RepositoryRoot,
            RebuildArguments(fullConsumerArguments, ConsumerProject));
        AssertSucceeded(addedMappingRun);
        AssertGeneratedFileSet(
            GeneratedDirectory,
            BothFullGeneratedFiles);

        await File.WriteAllTextAsync(
            ConsumerSource,
            OriginalConsumerSource);
        var removedMappingRun = await DotNetCli.Run(
            RepositoryRoot,
            fullConsumerArguments);
        AssertSucceeded(removedMappingRun);
        AssertGeneratedFileSet(
            GeneratedDirectory,
            PrimaryFullGeneratedFiles);

        var mapperDetailRun = await DotNetCli.Run(
            RepositoryRoot,
            RebuildArguments(ConsumerArguments, ConsumerProject));
        AssertSucceeded(mapperDetailRun);
        AssertGeneratedFileSet(
            GeneratedDirectory,
            MapperGeneratedFiles);

        await File.WriteAllTextAsync(
            ConsumerSource,
            EmptyConsumerSource);
        var emptyMappingRun = await DotNetCli.Run(
            RepositoryRoot,
            ConsumerArguments);
        AssertSucceeded(emptyMappingRun);
        AssertGeneratedFileSet(
            GeneratedDirectory,
            []);
        Assert.That(
            File.Exists(UnrelatedGeneratedFile),
            Is.True,
            "Removing every mapping must preserve files not owned by " +
            "Morphant.");

        await File.WriteAllTextAsync(
            ConsumerSource,
            OriginalConsumerSource);
        var mappingRestoredRun = await DotNetCli.Run(
            RepositoryRoot,
            ConsumerArguments);
        AssertSucceeded(mappingRestoredRun);
        AssertGeneratedFileSet(
            GeneratedDirectory,
            MapperGeneratedFiles);
    }

    [Test]
    public async Task Identical_output_preserves_snapshot_timestamps_across_builds_and_configurations()
    {
        await CreateSnapshot();

        var currentSnapshotWriteTimes = SnapshotWriteTimes(
            GeneratedDirectory);

        var noOpRun = await DotNetCli.Run(
            RepositoryRoot,
            ConsumerArguments);
        AssertSucceeded(noOpRun);

        Assert.That(
            SnapshotWriteTimes(GeneratedDirectory),
            Is.EqualTo(currentSnapshotWriteTimes),
            "An up-to-date build must not republish the Git snapshot.");

        await File.WriteAllTextAsync(
            ConsumerSource,
            OriginalConsumerSource +
            Environment.NewLine +
            "// Unrelated compiler input change." +
            Environment.NewLine);
        var unchangedGeneratorRun = await DotNetCli.Run(
            RepositoryRoot,
            ConsumerArguments);
        AssertSucceeded(unchangedGeneratorRun);
        Assert.That(
            SnapshotWriteTimes(GeneratedDirectory),
            Is.EqualTo(currentSnapshotWriteTimes),
            "A real compilation with identical Morphant output must " +
            "not touch snapshot timestamps.");

        var alternateConfiguration = Configuration == "Debug"
            ? "Release"
            : "Debug";
        var alternateConfigurationRun = await DotNetCli.Run(
            RepositoryRoot,
            WithConfiguration(
                ConsumerArguments,
                alternateConfiguration));
        AssertSucceeded(alternateConfigurationRun);
        Assert.Multiple(() =>
        {
            Assert.That(
                SnapshotWriteTimes(GeneratedDirectory),
                Is.EqualTo(currentSnapshotWriteTimes),
                "An equivalent build in another Configuration must " +
                "reuse the shared snapshot without metadata churn.");
            Assert.That(
                Directory.GetDirectories(
                    ConsumerGenerated,
                    "config-*",
                    SearchOption.TopDirectoryOnly),
                Is.Empty,
                "Debug and Release must not create duplicate snapshot " +
                "directories.");
        });
        await File.WriteAllTextAsync(
            ConsumerSource,
            OriginalConsumerSource);
    }

    [Test]
    public async Task Design_time_build_preserves_snapshot_until_normal_compilation()
    {
        await CreateSnapshot();

        await File.WriteAllTextAsync(
            StaleMorphantFile,
            "// stale Morphant output");
        await File.WriteAllTextAsync(
            ConsumerSource,
            SecondMappingConsumerSource);
        var snapshotBeforeDesignTimeBuild = SnapshotContents(
            GeneratedDirectory);
        var designTimeRun = await DotNetCli.Run(
            RepositoryRoot,
            [
                "build",
                ConsumerProject,
                "--configuration",
                Configuration,
                "-m:1",
                "-nodeReuse:false",
                "--nologo",
                $"-p:MorphantTestPackageVersion={PackageVersion}",
                $"-p:RestoreSources={PackageFeed}",
                $"-p:BaseOutputPath={ConsumerOutput}",
                $"-p:BaseIntermediateOutputPath={ConsumerIntermediate}",
                "-p:MorphantGitSnapshot=true",
                "-p:DesignTimeBuild=true",
                "-p:NuGetAudit=false"
            ]);
        AssertSucceeded(designTimeRun);
        Assert.That(
            File.Exists(StaleMorphantFile),
            Is.True,
            "A design-time build must not clean generated files used " +
            "by the editor.");
        Assert.That(
            SnapshotContents(GeneratedDirectory),
            Is.EqualTo(snapshotBeforeDesignTimeBuild),
            "A design-time build must not publish or clean the Git " +
            "snapshot.");

        await File.WriteAllTextAsync(
            ConsumerSource,
            OriginalConsumerSource);
        var finalRun = await DotNetCli.Run(
            RepositoryRoot,
            ConsumerArguments);
        AssertSucceeded(finalRun);
        AssertGeneratedFileSet(
            GeneratedDirectory,
            MapperGeneratedFiles);
    }

    [Test]
    public async Task Failed_compilation_preserves_snapshot_and_recovers()
    {
        await CreateSnapshot();

        var successfulSnapshot = SnapshotContents(
            GeneratedDirectory);
        await File.WriteAllTextAsync(
            ConsumerSource,
            BrokenConsumerSource);
        var failedBuild = await DotNetCli.Run(
            RepositoryRoot,
            BuildArguments(ConsumerArguments, ConsumerProject));
        AssertFailed(failedBuild);
        Assert.That(
            SnapshotContents(GeneratedDirectory),
            Is.EqualTo(successfulSnapshot),
            "A failed compilation must preserve the last successful " +
            "Git snapshot byte for byte.");

        await File.WriteAllTextAsync(
            ConsumerSource,
            OriginalConsumerSource);
        var recoveredRun = await DotNetCli.Run(
            RepositoryRoot,
            ConsumerArguments);
        AssertSucceeded(recoveredRun);
    }

    [Test]
    public async Task Rebuild_repairs_deleted_and_modified_snapshot_files()
    {
        await CreateSnapshot();

        var deletedSnapshotFile = Path.Combine(
            GeneratedDirectory,
            MapperGeneratedFiles[0]);
        File.Delete(deletedSnapshotFile);
        var upToDateBuild = await DotNetCli.Run(
            RepositoryRoot,
            BuildArguments(ConsumerArguments, ConsumerProject));
        AssertSucceeded(upToDateBuild);
        Assert.That(
            File.Exists(deletedSnapshotFile),
            Is.False,
            "An up-to-date build does not run the compiler and therefore " +
            "does not repair a snapshot file.");

        var repairedFileRun = await DotNetCli.Run(
            RepositoryRoot,
            RebuildArguments(ConsumerArguments, ConsumerProject));
        AssertSucceeded(repairedFileRun);
        AssertGeneratedFileSet(
            GeneratedDirectory,
            MapperGeneratedFiles);

        await AssertTamperedSnapshotIsRepairedByRebuild(
            RepositoryRoot,
            ConsumerArguments,
            ConsumerProject,
            GeneratedDirectory);
    }

    [Test]
    public async Task Project_snapshot_setting_enables_compiler_emission()
    {
        await CreateSnapshot();

        var originalProject = await File.ReadAllTextAsync(ConsumerProject);
        var expectedSnapshot = SnapshotContents(GeneratedDirectory);
        var expectedWriteTimes = SnapshotWriteTimes(GeneratedDirectory);
        var project = XDocument.Parse(originalProject);
        project.Root!.AddFirst(new XElement("PropertyGroup",
            new XElement("MorphantGitSnapshot", "true"),
            new XElement("EmitCompilerGeneratedFiles", "false")));

        try
        {
            await File.WriteAllTextAsync(ConsumerProject, project.ToString());
            await File.WriteAllTextAsync(Path.Combine(GeneratedDirectory,
                "Morphant.Generated.TypeMapper.StaleEmission.g.cs"), "// stale\r\n");

            // Both properties come from the project, without a global snapshot override.
            var rebuilt = await DotNetCli.Run(RepositoryRoot,
                RebuildArguments(BaseConsumerArguments, ConsumerProject));
            AssertSucceeded(rebuilt);
            Assert.Multiple(() =>
            {
                Assert.That(SnapshotContents(GeneratedDirectory), Is.EqualTo(expectedSnapshot),
                    "Enabling snapshots in the project must override ordinary EmitCompilerGeneratedFiles=false " +
                    "and publish the current snapshot, removing its stale file.");
                Assert.That(SnapshotWriteTimes(GeneratedDirectory), Is.EqualTo(expectedWriteTimes),
                    "Identical snapshot files and foreign files must remain untouched.");
            });
        }
        finally
        {
            await File.WriteAllTextAsync(ConsumerProject, originalProject);
        }
    }

    private static async Task AssertTamperedSnapshotIsRepairedByRebuild(
        string repositoryRoot,
        IReadOnlyList<string> consumerArguments,
        string consumerProject,
        string generatedDirectory)
    {
        var expected = SnapshotContents(generatedDirectory);
        var generatedFile = Path.Combine(
            generatedDirectory,
            MapperGeneratedFiles[0]);
        await File.AppendAllTextAsync(
            generatedFile,
            "// manual edit\n",
            new System.Text.UTF8Encoding(false));

        var repairedContentRun = await DotNetCli.Run(
            repositoryRoot,
            RebuildArguments(consumerArguments, consumerProject));
        AssertSucceeded(repairedContentRun);
        Assert.That(
            SnapshotContents(generatedDirectory),
            Is.EqualTo(expected),
            "A rebuild must restore manually edited generated files.");
    }

    // lang=c#
    private const string SecondMappingConsumerSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.PackageTests.Consumer
{
    public sealed class Source
    {
        public int Value { get; init; }

        public int ImplicitOnly { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; } = 41;

        public int ImplicitOnly { get; set; } = 43;
    }

    public sealed class SecondSource
    {
        public int Value { get; init; }
    }

    public sealed class SecondDestination
    {
        public int Value { get; set; } = 59;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>()
                .Members((source, _) => new()
                {
                    Value = source.Value
                });
            builder.Map<SecondSource, SecondDestination>()
                .Members((source, _) => new()
                {
                    Value = source.Value
                });
        }
    }

    internal static class Program
    {
        public static void Main()
        {
            var mapper = new TestMapper();
            var primary =
                ((ITypeMapper<Source, Destination>)mapper).Create(
                    new Source { Value = 17, ImplicitOnly = 73 },
                    default(MappingContext));
            var second =
                ((ITypeMapper<SecondSource, SecondDestination>)mapper).Create(
                    new SecondSource { Value = 29 },
                    default(MappingContext));

            if (primary.Value != 17 ||
                primary.ImplicitOnly != 43 ||
                second.Value != 29 ||
                typeof(global::Morphant.Generated.N_8178863269c5aa3285fa9684f4ebcf69.DestinationMembers).Name !=
                "DestinationMembers")
            {
                throw new InvalidOperationException(
                    "The packaged generator did not actualize both " +
                    "mapping contracts.");
            }
        }
    }
}
""";

    // lang=c#
    private const string BrokenConsumerSource =
"""
#nullable enable

namespace Morphant.Generator.PackageTests.Consumer
{
    internal static class Broken
    {
        private static int Value => MissingSymbol;
    }
}
""";

    // lang=c#
    private const string EmptyConsumerSource =
"""
#nullable enable

namespace Morphant.Generator.PackageTests.Consumer
{
    internal static class Program
    {
        public static void Main()
        {
        }
    }
}
""";
}
