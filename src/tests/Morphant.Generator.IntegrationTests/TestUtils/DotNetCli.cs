using System.Diagnostics;

namespace Morphant.Generator.IntegrationTests.TestUtils;

internal static class DotNetCli
{
    public static async Task<ProcessResult> Run(
        string workingDirectory,
        IReadOnlyCollection<string> arguments,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = GetHostPath(),
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
        if (environment is not null)
            foreach (var entry in environment)
                process.StartInfo.Environment[entry.Key] = entry.Value;

        if (!process.Start())
        {
            throw new InvalidOperationException(
                "The dotnet process could not be started.");
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();

            throw new TimeoutException(
                $"dotnet {string.Join(' ', arguments)} exceeded 5 minutes in {workingDirectory}." +
                $"{Environment.NewLine}{await standardOutput}{await standardError}");
        }

        return new ProcessResult(
            process.ExitCode,
            await standardOutput + await standardError,
            arguments.ToArray());
    }

    private static string GetHostPath()
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

internal sealed record ProcessResult(
    int ExitCode,
    string Output,
    IReadOnlyList<string> Arguments)
{
    public string Command => "dotnet " + string.Join(' ', Arguments);
}
