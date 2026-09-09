using System.Diagnostics;
using System.Text;

namespace Morphant.Generator.IntegrationTests.TestUtils;

internal static class DotNetCli
{
    public static async Task<ProcessResult> Run(
        string workingDirectory,
        IReadOnlyCollection<string> arguments,
        IReadOnlyDictionary<string, string>? environment = null,
        Action<string>? outputReceived = null)
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

        var standardOutput = outputReceived is null
            ? process.StandardOutput.ReadToEndAsync()
            : ReadOutput(process.StandardOutput, outputReceived);
        var standardError = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return new ProcessResult(
            process.ExitCode,
            await standardOutput + await standardError,
            arguments.ToArray());
    }

    private static async Task<string> ReadOutput(StreamReader reader, Action<string> outputReceived)
    {
        var output = new StringBuilder();
        while (await reader.ReadLineAsync() is { } line)
        {
            output.AppendLine(line);
            outputReceived(line);
        }
        return output.ToString();
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
