using System.Diagnostics;

namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class PackageConsumptionTests
{
    [Test]
    public async Task Imports_buildTransitive_settings_from_the_packed_package()
    {
        var repositoryRoot = FindRepositoryRoot();
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
        var configuration = GetBuildConfiguration();

        Directory.CreateDirectory(packageFeed);

        try
        {
            await RunDotNet(
                repositoryRoot,
                "pack",
                Path.Combine(
                    repositoryRoot,
                    "src",
                    "Morphant",
                    "Morphant.csproj"),
                "--configuration",
                configuration,
                "--no-build",
                "--no-restore",
                "--output",
                packageFeed,
                $"-p:PackageVersion={packageVersion}",
                "-p:NuGetAudit=false");

            await RunDotNet(
                repositoryRoot,
                "run",
                "--project",
                Path.Combine(
                    repositoryRoot,
                    "src",
                    "tests",
                    "Morphant.Generator.PackageTests.Consumer",
                    "Morphant.Generator.PackageTests.Consumer.csproj"),
                "--configuration",
                configuration,
                $"-p:MorphantTestPackageVersion={packageVersion}",
                $"-p:RestoreSources={packageFeed}",
                $"-p:BaseOutputPath={consumerOutput}",
                $"-p:BaseIntermediateOutputPath={consumerIntermediate}",
                "-p:NuGetAudit=false");
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
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

    private static async Task RunDotNet(
        string workingDirectory,
        params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = GetDotNetHostPath(),
                WorkingDirectory = workingDirectory,
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

        var output = await standardOutput;
        var error = await standardError;

        Assert.That(
            process.ExitCode,
            Is.EqualTo(0),
            $"dotnet {string.Join(' ', arguments)} failed.{Environment.NewLine}" +
            output + error);
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
}
