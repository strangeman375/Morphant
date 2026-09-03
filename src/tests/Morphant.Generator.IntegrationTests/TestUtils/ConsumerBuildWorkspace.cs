namespace Morphant.Generator.IntegrationTests.TestUtils;

internal sealed class ConsumerBuildWorkspace : IDisposable
{
    private readonly string _repositoryRoot =
        IntegrationTestEnvironment.RepositoryRoot;
    private readonly string _configuration =
        IntegrationTestEnvironment.BuildConfiguration;
    private readonly string _testDirectory;

    public ConsumerBuildWorkspace()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "Morphant.ConsumerBuildWorkspace",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
        Directory.CreateDirectory(PackageFeed);
    }

    public string PackageFeed => Path.Combine(_testDirectory, "packages");

    public async Task<ConsumerBuild> BuildConsumer(
        string fixtureName,
        string? runtimeCandidatePath = null)
    {
        var paths = CreateBuildPaths(fixtureName);
        var arguments = BuildArguments(
            "build",
            GetFixtureProject(fixtureName),
            paths);

        arguments.Add($"-p:MorphantGeneratorPath={GetGeneratorAssemblyPath()}");
        arguments.Add($"-p:MorphantRuntimePath={GetRuntimeAssemblyPath()}");
        arguments.Add($"-p:MorphantTestAssetsPath={GetTestAssetsAssemblyPath()}");
        arguments.Add($"-p:RestoreSources={PackageFeed}");

        if (runtimeCandidatePath is not null)
        {
            arguments.Add($"-p:RuntimeCandidatePath={runtimeCandidatePath}");
        }

        var process = await DotNetCli.Run(_repositoryRoot, arguments);
        return new ConsumerBuild(process, paths.GeneratedDirectory);
    }

    public async Task<RuntimeCandidateBuild> BuildRuntimeCandidate(
        string fixtureName,
        string assemblyName)
    {
        var paths = CreateBuildPaths(fixtureName);
        var process = await DotNetCli.Run(
            _repositoryRoot,
            BuildArguments(
                "build",
                GetFixtureProject(fixtureName),
                paths));
        var assemblyPath = Path.Combine(
            paths.OutputDirectory,
            _configuration,
            "net10.0",
            assemblyName + ".dll");

        return new RuntimeCandidateBuild(process, assemblyPath);
    }

    public Task<ProcessResult> PackMorphant(string packageVersion)
    {
        return DotNetCli.Run(
            _repositoryRoot,
            [
                "pack",
                Path.Combine(
                    _repositoryRoot,
                    "src",
                    "Morphant",
                    "Morphant.csproj"),
                "--configuration",
                _configuration,
                "-m:1",
                "-nodeReuse:false",
                "--no-build",
                "--no-restore",
                "--output",
                PackageFeed,
                $"-p:PackageVersion={packageVersion}",
                "-p:NuGetAudit=false"
            ]);
    }

    public Task<ProcessResult> RunPackageConsumer(string packageVersion)
    {
        var paths = CreateBuildPaths("PackageConsumer");
        var arguments = BuildArguments(
            "run",
            GetFixtureProject("PackageConsumer"),
            paths);

        arguments.Add($"-p:MorphantTestPackageVersion={packageVersion}");
        arguments.Add($"-p:RestoreSources={PackageFeed}");

        return DotNetCli.Run(_repositoryRoot, arguments);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private List<string> BuildArguments(
        string command,
        string projectPath,
        BuildPaths paths)
    {
        var arguments = command == "run"
            ? new List<string> { command, "--project", projectPath }
            : [command, projectPath, "-m:1", "-nodeReuse:false", "--nologo"];

        arguments.AddRange(
        [
            "--configuration",
            _configuration,
            "--verbosity",
            "minimal",
            $"-p:BaseOutputPath={paths.OutputDirectory}",
            $"-p:BaseIntermediateOutputPath={paths.IntermediateDirectory}",
            $"-p:MorphantGeneratedFilesPath={paths.GeneratedDirectory}",
            "-p:RestoreIgnoreFailedSources=true",
            "-p:NuGetAudit=false"
        ]);

        return arguments;
    }

    private BuildPaths CreateBuildPaths(string fixtureName)
    {
        var root = Path.Combine(_testDirectory, fixtureName);

        return new BuildPaths(
            EnsureTrailingSeparator(Path.Combine(root, "bin")),
            EnsureTrailingSeparator(Path.Combine(root, "obj")),
            Path.Combine(root, "generated"));
    }

    private string GetFixtureProject(string fixtureName)
    {
        return Path.Combine(
            _repositoryRoot,
            "src",
            "tests",
            "Morphant.Generator.CompatibilityFixtures",
            fixtureName,
            fixtureName + ".csproj");
    }

    private string GetGeneratorAssemblyPath()
    {
        return Path.Combine(
            _repositoryRoot,
            "src",
            "Morphant.Generator",
            "bin",
            _configuration,
            "netstandard2.0",
            "Morphant.Generator.dll");
    }

    private static string GetRuntimeAssemblyPath()
    {
        return typeof(TypeMapper<>).Assembly.Location;
    }

    private string GetTestAssetsAssemblyPath()
    {
        return Path.Combine(
            _repositoryRoot,
            "src",
            "tests",
            "Morphant.Generator.UnitTests.TestAssets",
            "bin",
            _configuration,
            "netstandard2.0",
            "Morphant.Generator.UnitTests.TestAssets.dll");
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path + Path.DirectorySeparatorChar;
    }

    private sealed record BuildPaths(
        string OutputDirectory,
        string IntermediateDirectory,
        string GeneratedDirectory);
}

internal sealed record ConsumerBuild(
    ProcessResult Process,
    string GeneratedDirectory)
{
    public string[] GetGeneratedFiles(
        string searchPattern = "Morphant.Generated.*.cs")
    {
        return Directory.Exists(GeneratedDirectory)
            ? Directory.GetFiles(
                GeneratedDirectory,
                searchPattern,
                SearchOption.AllDirectories)
            : [];
    }
}

internal sealed record RuntimeCandidateBuild(
    ProcessResult Process,
    string AssemblyPath);
