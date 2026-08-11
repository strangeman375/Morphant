using System.Diagnostics;

namespace Morphant.Generator.IntegrationTests;

internal sealed class CompatibilityTestWorkspace : IDisposable
{
    private readonly string _repositoryRoot = FindRepositoryRoot();
    private readonly string _configuration = GetBuildConfiguration();
    private readonly string _testDirectory;

    public CompatibilityTestWorkspace()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "Morphant.CompatibilityDiagnosticsTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
        Directory.CreateDirectory(PackageFeed);
    }

    public string PackageFeed => Path.Combine(_testDirectory, "packages");

    public async Task<CompatibilityBuild> BuildConsumer(
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

        if (runtimeCandidatePath is not null)
        {
            arguments.Add($"-p:RuntimeCandidatePath={runtimeCandidatePath}");
        }

        var process = await RunDotNet(arguments);
        return new CompatibilityBuild(process, paths.GeneratedDirectory);
    }

    public async Task<RuntimeCandidateBuild> BuildRuntimeCandidate(
        string fixtureName,
        string assemblyName)
    {
        var paths = CreateBuildPaths(fixtureName);
        var process = await RunDotNet(BuildArguments(
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
        return RunDotNet(
        [
            "pack",
            Path.Combine(
                _repositoryRoot,
                "src",
                "Morphant",
                "Morphant.csproj"),
            "--configuration",
            _configuration,
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

        return RunDotNet(arguments);
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

    private async Task<ProcessResult> RunDotNet(
        IReadOnlyCollection<string> arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = GetDotNetHostPath(),
                WorkingDirectory = _repositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        process.StartInfo.Environment["DOTNET_NOLOGO"] = "1";

        if (!process.Start())
        {
            throw new InvalidOperationException(
                "The dotnet process could not be started.");
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return new ProcessResult(
            process.ExitCode,
            await standardOutput + await standardError,
            arguments);
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
        return typeof(TypeMapper).Assembly.Location;
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(
                 TestContext.CurrentContext.TestDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "MAPPING_API_IMPLEMENTATION_PLAN.md")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException(
            "Could not locate the Morphant repository root.");
    }

    private static string GetBuildConfiguration()
    {
        var targetFrameworkDirectory = Directory.GetParent(
            typeof(TypeMapper).Assembly.Location) ??
            throw new InvalidOperationException(
                "Could not locate the Morphant target framework directory.");
        var configurationDirectory = targetFrameworkDirectory.Parent ??
            throw new InvalidOperationException(
                "Could not locate the Morphant configuration directory.");

        return configurationDirectory.Name;
    }

    private static string GetDotNetHostPath()
    {
        var configuredHost =
            Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");

        if (!string.IsNullOrWhiteSpace(configuredHost))
        {
            return configuredHost;
        }

        var currentProcess = Environment.ProcessPath;

        return currentProcess is not null &&
               Path.GetFileNameWithoutExtension(currentProcess).Equals(
                   "dotnet",
                   StringComparison.OrdinalIgnoreCase)
            ? currentProcess
            : "dotnet";
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

internal sealed record CompatibilityBuild(
    ProcessResult Process,
    string GeneratedDirectory);

internal sealed record RuntimeCandidateBuild(
    ProcessResult Process,
    string AssemblyPath);

internal sealed record ProcessResult(
    int ExitCode,
    string Output,
    IReadOnlyCollection<string> Arguments);
