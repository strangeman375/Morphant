using System.Globalization;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class PackageConsumptionTests
{
    private const string HintPrefix =
        "Morphant_Generator_PackageTests_Consumer_";

    private static readonly string[] MapperGeneratedFiles =
    [
        "Morphant.Generated.TypeMapper." + HintPrefix + "TestMapper.g.cs"
    ];

    private static readonly string[] PrimaryFullGeneratedFiles =
    [
        "Morphant.Generated.Construction." +
        HintPrefix + "Destination.g.cs",
        "Morphant.Generated.MappingExtension." +
        HintPrefix + "Source__" + HintPrefix + "Destination.g.cs",
        "Morphant.Generated.Member." + HintPrefix + "Destination.g.cs",
        "Morphant.Generated.MemberExtension." +
        HintPrefix + "Source__" + HintPrefix + "Destination.g.cs",
        "Morphant.Generated.TypeMapper." + HintPrefix + "TestMapper.g.cs"
    ];

    private static readonly string[] BothFullGeneratedFiles =
    [
        .. PrimaryFullGeneratedFiles,
        "Morphant.Generated.Construction." +
        HintPrefix + "SecondDestination.g.cs",
        "Morphant.Generated.MappingExtension." +
        HintPrefix + "SecondSource__" +
        HintPrefix + "SecondDestination.g.cs",
        "Morphant.Generated.Member." +
        HintPrefix + "SecondDestination.g.cs",
        "Morphant.Generated.MemberExtension." +
        HintPrefix + "SecondSource__" +
        HintPrefix + "SecondDestination.g.cs"
    ];

    [Test]
    public async Task Packs_complete_assets_and_applies_buildTransitive_contract()
    {
        var repositoryRoot = IntegrationTestEnvironment.RepositoryRoot;
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "Morphant.PackageConsumptionTests",
            Guid.NewGuid().ToString("N"));
        var packageFeed = Path.Combine(testDirectory, "packages");
        var consumerOutput = Path.Combine(testDirectory, "bin") +
            Path.DirectorySeparatorChar;
        var consumerIntermediate = Path.Combine(testDirectory, "obj") +
            Path.DirectorySeparatorChar;
        var packageVersion =
            $"0.0.0-package-consumption.{Guid.NewGuid():N}";
        var configuration = IntegrationTestEnvironment.BuildConfiguration;
        var sourceConsumerDirectory = Path.Combine(
            repositoryRoot,
            "src",
            "tests",
            "Morphant.Generator.PackageTests.Consumer");
        var consumerDirectory = Path.Combine(testDirectory, "consumer");
        var consumerProject = Path.Combine(
            consumerDirectory,
            "Morphant.Generator.PackageTests.Consumer.csproj");
        var consumerSource = Path.Combine(consumerDirectory, "Program.cs");
        var consumerGenerated = Path.Combine(
            consumerDirectory,
            "Generated",
            "Morphant");

        Directory.CreateDirectory(packageFeed);
        Directory.CreateDirectory(consumerDirectory);
        File.Copy(
            Path.Combine(
                sourceConsumerDirectory,
                "Morphant.Generator.PackageTests.Consumer.csproj"),
            consumerProject);
        File.Copy(
            Path.Combine(sourceConsumerDirectory, "Program.cs"),
            consumerSource);
        var originalConsumerSource = await File.ReadAllTextAsync(
            consumerSource);

        try
        {
            var pack = await DotNetCli.Run(
                repositoryRoot,
                [
                    "pack",
                    Path.Combine(
                        repositoryRoot,
                        "src",
                        "Morphant",
                        "Morphant.csproj"),
                    "--configuration",
                    configuration,
                    "-m:1",
                    "-nodeReuse:false",
                    "--no-build",
                    "--no-restore",
                    "--output",
                    packageFeed,
                    $"-p:PackageVersion={packageVersion}",
                    "-p:NuGetAudit=false"
                ]);
            AssertSucceeded(pack);

            AssertPackageContents(
                repositoryRoot,
                packageFeed,
                packageVersion);

            var morphantGeneratedDirectory = Path.Combine(
                consumerGenerated,
                "net10.0");
            var staleMorphantFile = Path.Combine(
                morphantGeneratedDirectory,
                "Morphant.Generated.Removed.g.cs");
            var unrelatedGeneratedFile = Path.Combine(
                morphantGeneratedDirectory,
                "Other.Generator.Unrelated.g.cs");

            string[] baseConsumerArguments =
            [
                "run",
                "--project",
                consumerProject,
                "--configuration",
                configuration,
                $"-p:MorphantTestPackageVersion={packageVersion}",
                $"-p:RestoreSources={packageFeed}",
                $"-p:BaseOutputPath={consumerOutput}",
                $"-p:BaseIntermediateOutputPath={consumerIntermediate}",
                "-p:NuGetAudit=false"
            ];
            var snapshotDisabledRun = await DotNetCli.Run(
                repositoryRoot,
                baseConsumerArguments);
            AssertSucceeded(snapshotDisabledRun);
            Assert.That(
                Directory.Exists(consumerGenerated),
                Is.False,
                "Git snapshots must remain opt-in.");

            Directory.CreateDirectory(morphantGeneratedDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(
                unrelatedGeneratedFile)!);
            await File.WriteAllTextAsync(
                staleMorphantFile,
                "// stale Morphant output");
            await File.WriteAllTextAsync(
                unrelatedGeneratedFile,
                "// unrelated generator output");

            string[] consumerArguments =
            [
                .. baseConsumerArguments,
                "-p:MorphantGitSnapshot=true"
            ];
            var run = await DotNetCli.Run(
                repositoryRoot,
                RebuildArguments(consumerArguments, consumerProject));
            AssertSucceeded(run);

            Assert.Multiple(() =>
            {
                Assert.That(
                    File.Exists(staleMorphantFile),
                    Is.False,
                    "The package must remove stale Morphant snapshot " +
                    "files after successful compilation.");
                Assert.That(
                    File.Exists(unrelatedGeneratedFile),
                    Is.True,
                    "Morphant cleanup must preserve other generators' " +
                    "output.");
            });
            AssertGeneratedFileSet(
                morphantGeneratedDirectory,
                MapperGeneratedFiles);

            string[] fullConsumerArguments =
            [
                .. consumerArguments,
                "-p:MorphantGitSnapshotDetail=Full"
            ];

            await File.WriteAllTextAsync(
                consumerSource,
                SecondMappingConsumerSource);
            var addedMappingRun = await DotNetCli.Run(
                repositoryRoot,
                RebuildArguments(fullConsumerArguments, consumerProject));
            AssertSucceeded(addedMappingRun);
            AssertGeneratedFileSet(
                morphantGeneratedDirectory,
                BothFullGeneratedFiles);

            await File.WriteAllTextAsync(
                consumerSource,
                originalConsumerSource);
            var removedMappingRun = await DotNetCli.Run(
                repositoryRoot,
                fullConsumerArguments);
            AssertSucceeded(removedMappingRun);
            AssertGeneratedFileSet(
                morphantGeneratedDirectory,
                PrimaryFullGeneratedFiles);

            var mapperDetailRun = await DotNetCli.Run(
                repositoryRoot,
                RebuildArguments(consumerArguments, consumerProject));
            AssertSucceeded(mapperDetailRun);
            AssertGeneratedFileSet(
                morphantGeneratedDirectory,
                MapperGeneratedFiles);

            await File.WriteAllTextAsync(
                consumerSource,
                EmptyConsumerSource);
            var emptyMappingRun = await DotNetCli.Run(
                repositoryRoot,
                consumerArguments);
            AssertSucceeded(emptyMappingRun);
            AssertGeneratedFileSet(
                morphantGeneratedDirectory,
                []);
            Assert.That(
                File.Exists(unrelatedGeneratedFile),
                Is.True,
                "Removing every mapping must preserve files not owned by " +
                "Morphant.");

            await File.WriteAllTextAsync(
                consumerSource,
                originalConsumerSource);
            var mappingRestoredRun = await DotNetCli.Run(
                repositoryRoot,
                consumerArguments);
            AssertSucceeded(mappingRestoredRun);
            AssertGeneratedFileSet(
                morphantGeneratedDirectory,
                MapperGeneratedFiles);

            var currentSnapshotWriteTimes = SnapshotWriteTimes(
                morphantGeneratedDirectory);

            var noOpRun = await DotNetCli.Run(
                repositoryRoot,
                consumerArguments);
            AssertSucceeded(noOpRun);

            Assert.That(
                SnapshotWriteTimes(morphantGeneratedDirectory),
                Is.EqualTo(currentSnapshotWriteTimes),
                "An up-to-date build must not republish the Git snapshot.");

            await File.WriteAllTextAsync(
                consumerSource,
                originalConsumerSource +
                Environment.NewLine +
                "// Unrelated compiler input change." +
                Environment.NewLine);
            var unchangedGeneratorRun = await DotNetCli.Run(
                repositoryRoot,
                consumerArguments);
            AssertSucceeded(unchangedGeneratorRun);
            Assert.That(
                SnapshotWriteTimes(morphantGeneratedDirectory),
                Is.EqualTo(currentSnapshotWriteTimes),
                "A real compilation with identical Morphant output must " +
                "not touch snapshot timestamps.");

            var alternateConfiguration = configuration == "Debug"
                ? "Release"
                : "Debug";
            var alternateConfigurationRun = await DotNetCli.Run(
                repositoryRoot,
                WithConfiguration(
                    consumerArguments,
                    alternateConfiguration));
            AssertSucceeded(alternateConfigurationRun);
            Assert.Multiple(() =>
            {
                Assert.That(
                    SnapshotWriteTimes(morphantGeneratedDirectory),
                    Is.EqualTo(currentSnapshotWriteTimes),
                    "An equivalent build in another configuration must " +
                    "reuse the shared snapshot without metadata churn.");
                Assert.That(
                    Directory.GetDirectories(
                        consumerGenerated,
                        "config-*",
                        SearchOption.TopDirectoryOnly),
                    Is.Empty,
                    "Debug and Release must not create duplicate snapshot " +
                    "directories.");
            });
            await File.WriteAllTextAsync(
                consumerSource,
                originalConsumerSource);

            await File.WriteAllTextAsync(
                staleMorphantFile,
                "// stale Morphant output");
            await File.WriteAllTextAsync(
                consumerSource,
                SecondMappingConsumerSource);
            var snapshotBeforeDesignTimeBuild = SnapshotContents(
                morphantGeneratedDirectory);
            var designTimeRun = await DotNetCli.Run(
                repositoryRoot,
                [
                    "build",
                    consumerProject,
                    "--configuration",
                    configuration,
                    "-m:1",
                    "-nodeReuse:false",
                    "--nologo",
                    $"-p:MorphantTestPackageVersion={packageVersion}",
                    $"-p:RestoreSources={packageFeed}",
                    $"-p:BaseOutputPath={consumerOutput}",
                    $"-p:BaseIntermediateOutputPath={consumerIntermediate}",
                    "-p:MorphantGitSnapshot=true",
                    "-p:DesignTimeBuild=true",
                    "-p:NuGetAudit=false"
                ]);
            AssertSucceeded(designTimeRun);
            Assert.That(
                File.Exists(staleMorphantFile),
                Is.True,
                "A design-time build must not clean generated files used " +
                "by the editor.");
            Assert.That(
                SnapshotContents(morphantGeneratedDirectory),
                Is.EqualTo(snapshotBeforeDesignTimeBuild),
                "A design-time build must not publish or clean the Git " +
                "snapshot.");

            await File.WriteAllTextAsync(
                consumerSource,
                originalConsumerSource);
            var finalRun = await DotNetCli.Run(
                repositoryRoot,
                consumerArguments);
            AssertSucceeded(finalRun);
            AssertGeneratedFileSet(
                morphantGeneratedDirectory,
                MapperGeneratedFiles);

            var successfulSnapshot = SnapshotContents(
                morphantGeneratedDirectory);
            await File.WriteAllTextAsync(
                consumerSource,
                BrokenConsumerSource);
            var failedBuild = await DotNetCli.Run(
                repositoryRoot,
                BuildArguments(consumerArguments, consumerProject));
            AssertFailed(failedBuild);
            Assert.That(
                SnapshotContents(morphantGeneratedDirectory),
                Is.EqualTo(successfulSnapshot),
                "A failed compilation must preserve the last successful " +
                "Git snapshot byte for byte.");

            await File.WriteAllTextAsync(
                consumerSource,
                originalConsumerSource);
            var recoveredRun = await DotNetCli.Run(
                repositoryRoot,
                consumerArguments);
            AssertSucceeded(recoveredRun);

            var deletedSnapshotFile = Path.Combine(
                morphantGeneratedDirectory,
                MapperGeneratedFiles[0]);
            File.Delete(deletedSnapshotFile);
            var upToDateBuild = await DotNetCli.Run(
                repositoryRoot,
                BuildArguments(consumerArguments, consumerProject));
            AssertSucceeded(upToDateBuild);
            Assert.That(
                File.Exists(deletedSnapshotFile),
                Is.False,
                "An up-to-date build does not run the compiler and therefore " +
                "does not repair a snapshot file.");

            var repairedFileRun = await DotNetCli.Run(
                repositoryRoot,
                RebuildArguments(consumerArguments, consumerProject));
            AssertSucceeded(repairedFileRun);
            AssertGeneratedFileSet(
                morphantGeneratedDirectory,
                MapperGeneratedFiles);

            await AssertTamperedSnapshotIsRepairedByRebuild(
                repositoryRoot,
                consumerArguments,
                consumerProject,
                morphantGeneratedDirectory);

            await AssertUnsafeConfigurationFailsBeforeMutation(
                repositoryRoot,
                consumerArguments,
                consumerProject,
                consumerDirectory,
                morphantGeneratedDirectory);

            await AssertDestinationDirectoryCollisionFailsBeforeMutation(
                repositoryRoot,
                consumerArguments,
                consumerProject,
                morphantGeneratedDirectory);

            await AssertDisabledAndChangedPathRemainCompilerSafe(
                repositoryRoot,
                baseConsumerArguments,
                consumerProject,
                consumerDirectory,
                consumerGenerated);

            await AssertMultiTargetSnapshotSelection(
                repositoryRoot,
                testDirectory,
                packageFeed,
                packageVersion,
                configuration);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    private static void AssertGeneratedFileSet(
        string directory,
        IReadOnlyCollection<string> expected)
    {
        var actual = Directory.GetFiles(
                directory,
                "Morphant.Generated.*.g.cs",
                SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(
            actual,
            Is.EqualTo(expected.Order(StringComparer.Ordinal)),
            "The on-disk generated file set must match the current " +
            "mapping configuration exactly.");

        foreach (var file in actual)
        {
            var bytes = File.ReadAllBytes(Path.Combine(directory, file!));
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            var withoutCrLf = text.Replace("\r\n", string.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(
                    withoutCrLf,
                    Does.Not.Contain('\n').And.Not.Contain('\r'),
                    $"{file} must use deterministic CRLF line endings.");
            });
        }
    }

    private static Dictionary<string, DateTime> SnapshotWriteTimes(
        string directory)
    {
        return Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .ToDictionary(
                static path => Path.GetFileName(path)!,
                File.GetLastWriteTimeUtc,
                StringComparer.Ordinal);
    }

    private static Dictionary<string, byte[]> SnapshotContents(
        string directory)
    {
        return Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .ToDictionary(
                static path => Path.GetFileName(path)!,
                File.ReadAllBytes,
                StringComparer.Ordinal);
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

    private static async Task AssertUnsafeConfigurationFailsBeforeMutation(
        string repositoryRoot,
        IReadOnlyList<string> consumerArguments,
        string consumerProject,
        string consumerDirectory,
        string generatedDirectory)
    {
        var expected = SnapshotContents(generatedDirectory);
        var unsafeCases = new[]
        {
            (
                $"-p:MorphantGitSnapshotPath={consumerDirectory}",
                "MORPHANTMSB005"),
            (
                $"-p:MorphantGitSnapshotPath={Path.Combine(consumerDirectory, "Generated", "*")}",
                "MORPHANTMSB006"),
            (
                "-p:MorphantGitSnapshotPath=" + Path.Combine(
                    Path.GetDirectoryName(consumerDirectory)!,
                    "external-snapshot"),
                "MORPHANTMSB005"),
            (
                "-p:MorphantGitSnapshotPath=" +
                Path.Combine(consumerDirectory, "Generated", "One") + ";" +
                Path.Combine(consumerDirectory, "Generated", "Two"),
                "MSB1006"),
            (
                $"-p:CompilerGeneratedFilesOutputPath={generatedDirectory}",
                "MORPHANTMSB004"),
            ("-p:CompilerGeneratedFilesOutputPath=", "MORPHANTMSB006"),
            ("-p:EmitCompilerGeneratedFiles=false", "MORPHANTMSB002"),
            (
                "-p:TargetsTriggeredByCompilation=ForeignTarget",
                "MORPHANTMSB017"),
            (
                "-p:MorphantGitSnapshotDetail=Everything",
                "MORPHANTMSB020"),
            (
                "-p:MorphantGitSnapshotTargetFrameworks=net9.0",
                "MORPHANTMSB021")
        };

        foreach (var (property, expectedCode) in unsafeCases)
        {
            var result = await DotNetCli.Run(
                repositoryRoot,
                [
                    .. BuildArguments(consumerArguments, consumerProject),
                    property
                ]);
            AssertFailed(result);
            Assert.That(
                result.Output,
                Does.Contain(expectedCode),
                $"Unsafe configuration '{property}' failed without the " +
                "expected actionable diagnostic.");
            Assert.That(
                SnapshotContents(generatedDirectory),
                Is.EqualTo(expected),
                $"Unsafe configuration '{property}' mutated the last " +
                "successful snapshot.");
        }
    }

    private static async Task AssertDestinationDirectoryCollisionFailsBeforeMutation(
        string repositoryRoot,
        IReadOnlyList<string> consumerArguments,
        string consumerProject,
        string generatedDirectory)
    {
        var collision = Path.Combine(
            generatedDirectory,
            MapperGeneratedFiles[0]);
        File.Delete(collision);
        Directory.CreateDirectory(collision);
        var expectedFailureState = SnapshotTree(generatedDirectory);

        var failedPublication = await DotNetCli.Run(
            repositoryRoot,
            RebuildArguments(consumerArguments, consumerProject));
        AssertFailed(failedPublication);
        Assert.That(
            SnapshotTree(generatedDirectory),
            Is.EqualTo(expectedFailureState),
            "A publication failure must not leave a partially replaced " +
            "snapshot.");

        Directory.Delete(collision);
        var repairedPublication = await DotNetCli.Run(
            repositoryRoot,
            RebuildArguments(consumerArguments, consumerProject));
        AssertSucceeded(repairedPublication);
        AssertGeneratedFileSet(generatedDirectory, MapperGeneratedFiles);
    }

    private static async Task AssertDisabledAndChangedPathRemainCompilerSafe(
        string repositoryRoot,
        IReadOnlyList<string> baseConsumerArguments,
        string consumerProject,
        string consumerDirectory,
        string defaultGeneratedRoot)
    {
        var disabledRun = await DotNetCli.Run(
            repositoryRoot,
            baseConsumerArguments);
        AssertSucceeded(disabledRun);

        var alternativeRoot = Path.Combine(
            consumerDirectory,
            "Generated",
            "Alternative");
        var changedPathRun = await DotNetCli.Run(
            repositoryRoot,
            RebuildArguments(
                [
                    .. baseConsumerArguments,
                    "-p:MorphantGitSnapshot=true",
                    $"-p:MorphantGitSnapshotPath={alternativeRoot}"
                ],
                consumerProject));
        AssertSucceeded(changedPathRun);
        AssertGeneratedFileSet(
            Path.Combine(
                alternativeRoot,
                "net10.0"),
            MapperGeneratedFiles);
        Assert.That(
            Directory.Exists(defaultGeneratedRoot),
            Is.True,
            "Changing the path must not silently delete a committed old " +
            "snapshot; its reserved files remain excluded from Compile.");
    }

    private static Dictionary<string, string> SnapshotTree(string directory)
    {
        var result = Directory.GetDirectories(
                directory,
                "*",
                SearchOption.AllDirectories)
            .ToDictionary(
                path => "D:" + Path.GetRelativePath(directory, path),
                static _ => string.Empty,
                StringComparer.Ordinal);

        foreach (var file in Directory.GetFiles(
                     directory,
                     "*",
                     SearchOption.AllDirectories))
        {
            result.Add(
                "F:" + Path.GetRelativePath(directory, file),
                Convert.ToBase64String(File.ReadAllBytes(file)));
        }

        return result;
    }

    private static string[] BuildArguments(
        IReadOnlyList<string> runArguments,
        string project)
    {
        return
        [
            "build",
            project,
            .. runArguments.Skip(3)
        ];
    }

    private static string[] RebuildArguments(
        IReadOnlyList<string> runArguments,
        string project) =>
    [
        .. BuildArguments(runArguments, project),
        "-t:Rebuild"
    ];

    private static string[] WithConfiguration(
        IReadOnlyList<string> arguments,
        string configuration)
    {
        var result = arguments.ToArray();
        var option = Array.IndexOf(result, "--configuration");
        Assert.That(option, Is.GreaterThanOrEqualTo(0));
        result[option + 1] = configuration;
        return result;
    }

    private static async Task AssertMultiTargetSnapshotSelection(
        string repositoryRoot,
        string testDirectory,
        string packageFeed,
        string packageVersion,
        string configuration)
    {
        var projectDirectory = Path.Combine(
            testDirectory,
            "multi-target-consumer");
        var projectPath = Path.Combine(
            projectDirectory,
            "MultiTargetConsumer.csproj");
        var outputDirectory = Path.Combine(projectDirectory, "bin") +
            Path.DirectorySeparatorChar;
        var intermediateDirectory = Path.Combine(projectDirectory, "obj") +
            Path.DirectorySeparatorChar;

        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            projectPath,
            MultiTargetConsumerProjectText(packageVersion));
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "Mapping.cs"),
            MultiTargetConsumerSource);

        string[] buildArguments =
        [
            "build",
            projectPath,
            "--configuration",
            configuration,
            string.Equals(
                Environment.GetEnvironmentVariable("CI"),
                "true",
                StringComparison.OrdinalIgnoreCase)
                ? "-m"
                : "-m:1",
            "-nodeReuse:false",
            "--nologo",
            $"-p:RestoreSources={packageFeed}",
            $"-p:BaseOutputPath={outputDirectory}",
            $"-p:BaseIntermediateOutputPath={intermediateDirectory}",
            "-p:MorphantGitSnapshot=true",
            $"-p:MorphantGitSnapshotPath={Path.Combine(projectDirectory, "generated")}",
            "-p:NuGetAudit=false"
        ];
        var build = await DotNetCli.Run(repositoryRoot, buildArguments);
        AssertSucceeded(build);

        string[] expected =
        [
            "Morphant.Generated.TypeMapper.MultiTarget_TestMapper.g.cs"
        ];

        Assert.Multiple(() =>
        {
            Assert.That(
                Directory.Exists(Path.Combine(
                    projectDirectory,
                    "generated",
                    "netstandard2.0")),
                Is.False,
                "Only the last declared target framework is selected by " +
                "default.");
            AssertGeneratedFileSet(
                Path.Combine(projectDirectory, "generated", "net10.0"),
                expected);
        });

        await File.WriteAllTextAsync(
            projectPath,
            MultiTargetConsumerProjectText(
                packageVersion,
                snapshotTargetFrameworks: "netstandard2.0;net10.0"));
        var allTargetFrameworksBuild = await DotNetCli.Run(
            repositoryRoot,
            [.. buildArguments, "-t:Rebuild"]);
        AssertSucceeded(allTargetFrameworksBuild);

        foreach (var targetFramework in new[] { "netstandard2.0", "net10.0" })
        {
            AssertGeneratedFileSet(
                Path.Combine(
                    projectDirectory,
                    "generated",
                    targetFramework),
                expected);
        }

        await File.WriteAllTextAsync(
            projectPath,
            MultiTargetConsumerProjectText(
                packageVersion,
                snapshotTargetFrameworks: "netstandard2.0"));
        var selectedTargetFrameworkBuild = await DotNetCli.Run(
            repositoryRoot,
            [.. buildArguments, "-t:Rebuild"]);
        AssertSucceeded(selectedTargetFrameworkBuild);
        Assert.Multiple(() =>
        {
            AssertGeneratedFileSet(
                Path.Combine(
                    projectDirectory,
                    "generated",
                    "netstandard2.0"),
                expected);
            Assert.That(
                Directory.Exists(Path.Combine(
                    projectDirectory,
                    "generated",
                    "net10.0")),
                Is.False,
                "A no-longer-selected target framework must be removed from " +
                "the owned snapshot root.");
        });

        await File.WriteAllTextAsync(
            projectPath,
            MultiTargetConsumerProjectText(
                packageVersion,
                targetFrameworks: "net10.0"));
        var removedTargetFrameworkBuild = await DotNetCli.Run(
            repositoryRoot,
            [.. buildArguments, "-t:Rebuild"]);
        AssertSucceeded(removedTargetFrameworkBuild);
        Assert.Multiple(() =>
        {
            Assert.That(
                Directory.Exists(Path.Combine(
                    projectDirectory,
                    "generated",
                    "netstandard2.0")),
                Is.False,
                "A removed target framework must be removed from the owned " +
                "snapshot root.");
            AssertGeneratedFileSet(
                Path.Combine(
                    projectDirectory,
                    "generated",
                    "net10.0"),
                expected);
        });
    }

    private static string MultiTargetConsumerProjectText(
        string packageVersion,
        string targetFrameworks = "netstandard2.0;net10.0",
        string? snapshotTargetFrameworks = null)
    {
        var snapshotProperty = snapshotTargetFrameworks is null
            ? string.Empty
            : "<MorphantGitSnapshotTargetFrameworks>" +
              snapshotTargetFrameworks +
              "</MorphantGitSnapshotTargetFrameworks>";

        return MultiTargetConsumerProjectTemplate
            .Replace("__PACKAGE_VERSION__", packageVersion)
            .Replace("__TARGET_FRAMEWORKS__", targetFrameworks)
            .Replace("__SNAPSHOT_TARGET_FRAMEWORKS__", snapshotProperty);
    }

    // lang=xml
    private const string MultiTargetConsumerProjectTemplate =
"""
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>__TARGET_FRAMEWORKS__</TargetFrameworks>
    __SNAPSHOT_TARGET_FRAMEWORKS__
    <LangVersion>9.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnableDefaultEmbeddedResourceItems>false</EnableDefaultEmbeddedResourceItems>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Morphant" Version="__PACKAGE_VERSION__" />
  </ItemGroup>

</Project>
""";

    // lang=c#
    private const string MultiTargetConsumerSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace MultiTarget
{
    public sealed class Source
    {
        public int Value { get; set; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

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
    public partial class TestMapper : TypeMapper
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
                typeof(Morphant.Generated.DestinationMembers).Name !=
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

    private static void AssertPackageContents(
        string repositoryRoot,
        string packageFeed,
        string packageVersion)
    {
        const string expectedPublicKeyToken = "ba27fb6be8f80649";
        var packagePath = Path.Combine(
            packageFeed,
            $"Morphant.{packageVersion}.nupkg");

        using var package = ZipFile.OpenRead(packagePath);

        Assert.Multiple(() =>
        {
            AssertPackagePayload(package);
            AssertPackagedRepositoryFiles(package, repositoryRoot);
            AssertPackageMetadata(package, packageVersion);
            AssertBuildTransitiveProperties(package);
            AssertStrongName(
                package,
                "lib/netstandard2.0/Morphant.dll",
                expectedPublicKeyToken);
            AssertStrongName(
                package,
                "analyzers/dotnet/cs/Morphant.Generator.dll",
                expectedPublicKeyToken);
            AssertStrongName(
                package,
                "buildTransitive/Morphant.Build.Tasks.dll",
                expectedPublicKeyToken);
            AssertSymbolPackage(packageFeed, packageVersion);
        });
    }

    private static void AssertPackagePayload(ZipArchive package)
    {
        var payload = package.Entries
            .Select(static entry => entry.FullName)
            .Where(static name =>
                name != "[Content_Types].xml" &&
                !name.StartsWith("_rels/", StringComparison.Ordinal) &&
                !name.StartsWith(
                    "package/services/metadata/",
                    StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(
            payload,
            Is.EqualTo(new[]
            {
                "LICENSE",
                "Morphant.nuspec",
                "README.md",
                "analyzers/dotnet/cs/Morphant.Generator.dll",
                "buildTransitive/Morphant.Build.Tasks.dll",
                "buildTransitive/Morphant.props",
                "buildTransitive/Morphant.targets",
                "lib/netstandard2.0/Morphant.dll",
                "lib/netstandard2.0/Morphant.xml",
                "logo.png"
            }));
    }

    private static void AssertPackagedRepositoryFiles(
        ZipArchive package,
        string repositoryRoot)
    {
        Assert.That(
            ReadEntryBytes(package, "logo.png"),
            Is.EqualTo(File.ReadAllBytes(Path.Combine(
                repositoryRoot,
                "logo.png"))));
        Assert.That(
            ReadEntryBytes(package, "README.md"),
            Is.EqualTo(File.ReadAllBytes(Path.Combine(
                repositoryRoot,
                "README.md"))));
        Assert.That(
            ReadEntryBytes(package, "LICENSE"),
            Is.EqualTo(File.ReadAllBytes(Path.Combine(
                repositoryRoot,
                "LICENSE"))));
    }

    private static void AssertPackageMetadata(
        ZipArchive package,
        string packageVersion)
    {
        var expectedCopyright = "Copyright (c) strangeman375 " +
            DateTime.UtcNow.ToString("yyyy", CultureInfo.InvariantCulture);
        var document = XDocument.Parse(ReadEntryText(
            package,
            "Morphant.nuspec"));
        var packageNamespace = document.Root!.Name.Namespace;
        var metadata = document.Root.Element(
            packageNamespace + "metadata")!;
        string Value(string name) =>
            metadata.Element(packageNamespace + name)?.Value ?? string.Empty;

        Assert.Multiple(() =>
        {
            Assert.That(Value("id"), Is.EqualTo("Morphant"));
            Assert.That(Value("version"), Is.EqualTo(packageVersion));
            Assert.That(Value("title"), Is.EqualTo("Morphant"));
            Assert.That(Value("authors"), Is.EqualTo("strangeman375"));
            Assert.That(Value("license"), Is.EqualTo("MIT"));
            Assert.That(
                metadata.Element(packageNamespace + "license")!
                    .Attribute("type")?.Value,
                Is.EqualTo("expression"));
            Assert.That(Value("icon"), Is.EqualTo("logo.png"));
            Assert.That(Value("readme"), Is.EqualTo("README.md"));
            Assert.That(
                Value("projectUrl"),
                Is.EqualTo("https://github.com/strangeman375/Morphant"));
            Assert.That(Value("description"), Is.Not.Empty);
            Assert.That(Value("releaseNotes"), Does.Contain("0.1"));
            Assert.That(
                Value("copyright"),
                Is.EqualTo(expectedCopyright));
            Assert.That(Value("tags"), Does.Contain("source-generator"));

            var repository = metadata.Element(
                packageNamespace + "repository")!;
            Assert.That(
                repository.Attribute("type")?.Value,
                Is.EqualTo("git"));
            Assert.That(
                repository.Attribute("url")?.Value,
                Is.EqualTo("https://github.com/strangeman375/Morphant"));

            var dependencies = metadata.Element(
                packageNamespace + "dependencies")!;
            var dependencyGroup = dependencies.Elements(
                packageNamespace + "group").Single();
            Assert.That(
                dependencyGroup.Attribute("targetFramework")?.Value,
                Is.EqualTo(".NETStandard2.0"));
            Assert.That(
                dependencies.Descendants(packageNamespace + "dependency"),
                Is.Empty);
        });
    }

    private static void AssertBuildTransitiveProperties(ZipArchive package)
    {
        var document = XDocument.Parse(ReadEntryText(
            package,
            "buildTransitive/Morphant.props"));
        var properties = document
            .Descendants("CompilerVisibleProperty")
            .Select(static element => element.Attribute("Include")?.Value)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                properties,
                Is.EqualTo(new[]
                {
                    "MorphantMappingMode",
                    "MorphantNullSourceHandling",
                    "MorphantNullDestinationHandling",
                    "MorphantConstructorSelection",
                    "MorphantMemberSelection",
                    "MorphantUnmappedMemberValidation"
                }));
            Assert.That(
                document.Descendants("DefaultItemExcludes").Single().Value,
                Is.EqualTo(
                    "$(DefaultItemExcludes);" +
                    "**/Morphant.Generated.*.g.cs"));
        });
    }

    private static void AssertSymbolPackage(
        string packageFeed,
        string packageVersion)
    {
        var symbolPackagePath = Path.Combine(
            packageFeed,
            $"Morphant.{packageVersion}.snupkg");

        Assert.That(
            File.Exists(symbolPackagePath),
            Is.True,
            "The symbol package was not produced.");

        using var symbols = ZipFile.OpenRead(symbolPackagePath);
        var payload = symbols.Entries
            .Select(static entry => entry.FullName)
            .Where(static name =>
                name != "[Content_Types].xml" &&
                !name.StartsWith("_rels/", StringComparison.Ordinal) &&
                !name.StartsWith(
                    "package/services/metadata/",
                    StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(
            payload,
            Is.EqualTo(new[]
            {
                "Morphant.nuspec",
                "lib/netstandard2.0/Morphant.pdb"
            }));
        Assert.That(
            symbols.GetEntry("lib/netstandard2.0/Morphant.pdb")!.Length,
            Is.GreaterThan(0));
    }

    private static void AssertStrongName(
        ZipArchive package,
        string entryName,
        string expectedPublicKeyToken)
    {
        var entry = package.GetEntry(entryName);

        Assert.That(entry, Is.Not.Null, $"Missing package entry {entryName}.");

        using var entryStream = entry!.Open();
        using var stream = new MemoryStream();
        entryStream.CopyTo(stream);
        stream.Position = 0;
        using var peReader = new PEReader(stream);
        var metadataReader = peReader.GetMetadataReader();
        var assembly = metadataReader.GetAssemblyDefinition();
        var publicKey = metadataReader.GetBlobBytes(assembly.PublicKey);
        var hash = SHA1.HashData(publicKey);
        var publicKeyToken = Convert.ToHexString(
                hash[^8..].Reverse().ToArray())
            .ToLowerInvariant();

        Assert.That(publicKey, Is.Not.Empty, $"{entryName} is not strong-named.");
        Assert.That(publicKeyToken, Is.EqualTo(expectedPublicKeyToken));
    }

    private static byte[] ReadEntryBytes(
        ZipArchive package,
        string entryName)
    {
        var entry = package.GetEntry(entryName);

        Assert.That(entry, Is.Not.Null, $"Missing package entry {entryName}.");

        using var entryStream = entry!.Open();
        using var stream = new MemoryStream();
        entryStream.CopyTo(stream);
        return stream.ToArray();
    }

    private static string ReadEntryText(
        ZipArchive package,
        string entryName) =>
        System.Text.Encoding.UTF8.GetString(
            ReadEntryBytes(package, entryName)).TrimStart('\uFEFF');

    private static void AssertSucceeded(ProcessResult result)
    {
        Assert.That(
            result.ExitCode,
            Is.EqualTo(0),
            $"{result.Command} failed.{Environment.NewLine}{result.Output}");
    }

    private static void AssertFailed(ProcessResult result)
    {
        Assert.That(
            result.ExitCode,
            Is.Not.EqualTo(0),
            $"{result.Command} unexpectedly succeeded." +
            $"{Environment.NewLine}{result.Output}");
    }
}
