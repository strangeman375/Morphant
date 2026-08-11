using System.Diagnostics;
using System.Security;

namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class CompatibilityDiagnosticsTests
{
    private const string ContractMetadata =
        "Morphant.GeneratorContractVersion";

    [Test]
    public async Task Normal_package_runtime_and_bundled_generator_build_and_run()
    {
        await WithTestDirectory(async testDirectory =>
        {
            var repositoryRoot = FindRepositoryRoot();
            var packageFeed = Path.Combine(testDirectory, "packages");
            var packageVersion =
                $"0.0.0-compatibility.{Guid.NewGuid():N}";
            var consumerDirectory = Path.Combine(testDirectory, "consumer");

            Directory.CreateDirectory(packageFeed);
            Directory.CreateDirectory(consumerDirectory);
            WriteFile(
                Path.Combine(consumerDirectory, "Consumer.csproj"),
                CreatePackageConsumerProject(packageVersion));
            WriteFile(
                Path.Combine(consumerDirectory, "Program.cs"),
                SuccessfulMapperProgram);

            var pack = await RunDotNet(
                repositoryRoot,
                "pack",
                Path.Combine(repositoryRoot, "src", "Morphant", "Morphant.csproj"),
                "--configuration",
                GetBuildConfiguration(),
                "--no-build",
                "--no-restore",
                "--output",
                packageFeed,
                $"-p:PackageVersion={packageVersion}",
                "-p:NuGetAudit=false");
            AssertSucceeded(pack);

            var run = await RunDotNet(
                repositoryRoot,
                "run",
                "--project",
                Path.Combine(consumerDirectory, "Consumer.csproj"),
                "--configuration",
                GetBuildConfiguration(),
                $"-p:RestoreSources={packageFeed}",
                "-p:RestoreIgnoreFailedSources=true",
                "-p:NuGetAudit=false");

            AssertSucceeded(run);
            Assert.That(run.Output, Does.Not.Contain("MORPH000"));
        });
    }

    [Test]
    public async Task Analyzer_only_consumer_reports_MORPH0002_and_generates_nothing()
    {
        await WithTestDirectory(async testDirectory =>
        {
            var project = CreateDirectConsumer(
                testDirectory,
                "AnalyzerOnly",
                languageVersion: "9.0",
                runtimePaths: [],
                CompatibilityPlaceholderSource);
            var result = await Build(project.ProjectPath);

            AssertFailedWith(
                result,
                "MORPH0002",
                "Morphant generator requires a reference to a compatible Morphant runtime library.");
            AssertNoMorphantGeneratedFiles(project.GeneratedDirectory);
        });
    }

    [Test]
    public async Task Mismatched_runtime_revision_reports_exact_MORPH0004()
    {
        await WithTestDirectory(async testDirectory =>
        {
            var mismatchedRuntime = await BuildContractCandidate(
                testDirectory,
                "MismatchedRuntime",
                revision: "2");
            var project = CreateDirectConsumer(
                testDirectory,
                "MismatchedConsumer",
                languageVersion: "9.0",
                runtimePaths: [mismatchedRuntime],
                CompatibilityPlaceholderSource);
            var result = await Build(project.ProjectPath);

            AssertFailedWith(
                result,
                "MORPH0004",
                "The referenced Morphant runtime contract is incompatible " +
                "with this generator: contract revision '2' is not " +
                "supported; expected '1'.");
            AssertNoMorphantGeneratedFiles(project.GeneratedDirectory);
        });
    }

    [Test]
    public async Task Duplicate_runtime_candidates_report_single_MORPH0003()
    {
        await WithTestDirectory(async testDirectory =>
        {
            var duplicateCandidate = await BuildContractCandidate(
                testDirectory,
                "DuplicateRuntime",
                revision: "1");
            var project = CreateDirectConsumer(
                testDirectory,
                "DuplicateConsumer",
                languageVersion: "9.0",
                runtimePaths:
                [
                    GetRuntimeAssemblyPath(),
                    duplicateCandidate
                ],
                CompatibilityPlaceholderSource);
            var result = await Build(project.ProjectPath);

            AssertFailedWith(
                result,
                "MORPH0003",
                "Multiple Morphant runtime contracts were found. Reference " +
                "exactly one compatible Morphant runtime library.");
            var compilerSection = result.Output.Split(
                "Build FAILED.",
                StringSplitOptions.None)[0];
            Assert.That(
                CountOccurrences(compilerSection, "error MORPH0003"),
                Is.EqualTo(1));
            AssertNoMorphantGeneratedFiles(project.GeneratedDirectory);
        });
    }

    [Test]
    public async Task CSharp8_consumer_reports_exact_MORPH0001_and_generates_nothing()
    {
        await WithTestDirectory(async testDirectory =>
        {
            var project = CreateDirectConsumer(
                testDirectory,
                "CSharp8Consumer",
                languageVersion: "8.0",
                runtimePaths: [GetRuntimeAssemblyPath()],
                MapperDeclarationSource);
            var result = await Build(project.ProjectPath);

            AssertFailedWith(
                result,
                "MORPH0001",
                "Morphant requires C# 9.0 or later, but this compilation " +
                "uses C# 8.0.");
            AssertNoMorphantGeneratedFiles(project.GeneratedDirectory);
        });
    }

    [Test]
    public async Task Analyzer_config_changes_presentation_without_resuming_generation()
    {
        await WithTestDirectory(async testDirectory =>
        {
            var warningProject = CreateDirectConsumer(
                testDirectory,
                "WarningConsumer",
                languageVersion: "8.0",
                runtimePaths: [GetRuntimeAssemblyPath()],
                MapperDeclarationSource,
                diagnosticSeverity: "warning");
            var warning = await Build(warningProject.ProjectPath);

            AssertSucceeded(warning);
            Assert.That(warning.Output, Does.Contain("warning MORPH0001"));
            AssertNoMorphantGeneratedFiles(warningProject.GeneratedDirectory);

            var suppressedProject = CreateDirectConsumer(
                testDirectory,
                "SuppressedConsumer",
                languageVersion: "8.0",
                runtimePaths: [GetRuntimeAssemblyPath()],
                MapperDeclarationSource,
                diagnosticSeverity: "none");
            var suppressed = await Build(suppressedProject.ProjectPath);

            AssertSucceeded(suppressed);
            Assert.That(suppressed.Output, Does.Not.Contain("MORPH0001"));
            AssertNoMorphantGeneratedFiles(
                suppressedProject.GeneratedDirectory);
        });
    }

    private static DirectConsumerProject CreateDirectConsumer(
        string testDirectory,
        string name,
        string languageVersion,
        IReadOnlyList<string> runtimePaths,
        string source,
        string? diagnosticSeverity = null)
    {
        var projectDirectory = Path.Combine(testDirectory, name);
        var generatedDirectory = Path.Combine(projectDirectory, "generated");
        var projectPath = Path.Combine(projectDirectory, name + ".csproj");

        Directory.CreateDirectory(projectDirectory);
        WriteFile(
            projectPath,
            CreateDirectConsumerProject(
                languageVersion,
                generatedDirectory,
                runtimePaths));
        WriteFile(Path.Combine(projectDirectory, "Consumer.cs"), source);

        if (diagnosticSeverity is not null)
        {
            WriteFile(
                Path.Combine(projectDirectory, ".globalconfig"),
                $"is_global = true{Environment.NewLine}" +
                $"global_level = 100{Environment.NewLine}" +
                $"dotnet_diagnostic.MORPH0001.severity = " +
                diagnosticSeverity + Environment.NewLine);
        }

        return new DirectConsumerProject(projectPath, generatedDirectory);
    }

    private static string CreateDirectConsumerProject(
        string languageVersion,
        string generatedDirectory,
        IReadOnlyList<string> runtimePaths)
    {
        var references = string.Join(
            Environment.NewLine,
            runtimePaths.Select((path, index) =>
                "        <Reference Include=\"Runtime" + index + "\">" +
                Environment.NewLine +
                "            <HintPath>" + Escape(path) + "</HintPath>" +
                Environment.NewLine +
                "            <Private>false</Private>" +
                Environment.NewLine +
                "        </Reference>"));

        return
$"""
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <LangVersion>{languageVersion}</LangVersion>
        <Nullable>enable</Nullable>
        <ImplicitUsings>disable</ImplicitUsings>
        <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
        <NoWarn>$(NoWarn);1591</NoWarn>
        <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
        <CompilerGeneratedFilesOutputPath>{Escape(generatedDirectory)}</CompilerGeneratedFilesOutputPath>
    </PropertyGroup>
    <ItemGroup>
{references}
        <Analyzer Include="{Escape(GetGeneratorAssemblyPath())}" />
    </ItemGroup>
</Project>
""";
    }

    private static string CreatePackageConsumerProject(string packageVersion)
    {
        return
$"""
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net10.0</TargetFramework>
        <LangVersion>9.0</LangVersion>
        <Nullable>enable</Nullable>
        <ImplicitUsings>disable</ImplicitUsings>
        <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
        <NoWarn>$(NoWarn);1591</NoWarn>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Morphant" Version="{packageVersion}" />
    </ItemGroup>
</Project>
""";
    }

    private static async Task<string> BuildContractCandidate(
        string testDirectory,
        string assemblyName,
        string revision)
    {
        var projectDirectory = Path.Combine(testDirectory, assemblyName);
        var projectPath = Path.Combine(
            projectDirectory,
            assemblyName + ".csproj");

        Directory.CreateDirectory(projectDirectory);
        WriteFile(
            projectPath,
$"""
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <AssemblyName>{assemblyName}</AssemblyName>
        <Nullable>enable</Nullable>
    </PropertyGroup>
    <ItemGroup>
        <AssemblyMetadata Include="{ContractMetadata}" Value="{revision}" />
    </ItemGroup>
</Project>
""");
        WriteFile(
            Path.Combine(projectDirectory, "Candidate.cs"),
            "namespace Candidate; public sealed class Placeholder;");

        var build = await Build(projectPath);
        AssertSucceeded(build);

        return Path.Combine(
            projectDirectory,
            "bin",
            GetBuildConfiguration(),
            "net10.0",
            assemblyName + ".dll");
    }

    private static async Task<ProcessResult> Build(string projectPath)
    {
        return await RunDotNet(
            FindRepositoryRoot(),
            "build",
            projectPath,
            "--configuration",
            GetBuildConfiguration(),
            "-m:1",
            "-nodeReuse:false",
            "--nologo",
            "--verbosity",
            "minimal",
            "-p:RestoreIgnoreFailedSources=true",
            "-p:NuGetAudit=false");
    }

    private static async Task<ProcessResult> RunDotNet(
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

        return new ProcessResult(
            process.ExitCode,
            await standardOutput + await standardError,
            arguments);
    }

    private static void AssertSucceeded(ProcessResult result)
    {
        Assert.That(
            result.ExitCode,
            Is.EqualTo(0),
            $"dotnet {string.Join(' ', result.Arguments)} failed." +
            Environment.NewLine +
            result.Output);
    }

    private static void AssertFailedWith(
        ProcessResult result,
        string id,
        string message)
    {
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.Not.EqualTo(0), result.Output);
            Assert.That(result.Output, Does.Contain("error " + id));
            Assert.That(result.Output, Does.Contain(message));
        });
    }

    private static void AssertNoMorphantGeneratedFiles(string directory)
    {
        var files = Directory.Exists(directory)
            ? Directory.GetFiles(
                directory,
                "Morphant.Generated.*",
                SearchOption.AllDirectories)
            : [];

        Assert.That(files, Is.Empty);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;

        while ((index = source.IndexOf(
                   value,
                   index,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static async Task WithTestDirectory(
        Func<string, Task> action)
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "Morphant.CompatibilityDiagnosticsTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(testDirectory);

        try
        {
            await action(testDirectory);
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

    private static string GetRuntimeAssemblyPath()
    {
        return typeof(TypeMapper).Assembly.Location;
    }

    private static string GetGeneratorAssemblyPath()
    {
        return Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Morphant.Generator",
            "bin",
            GetBuildConfiguration(),
            "netstandard2.0",
            "Morphant.Generator.dll");
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

    private static string Escape(string value)
    {
        return SecurityElement.Escape(value) ?? value;
    }

    private static void WriteFile(string path, string content)
    {
        File.WriteAllText(path, content);
    }

    private const string CompatibilityPlaceholderSource =
"""
#nullable enable
#pragma warning disable CS1591

namespace CompatibilityConsumer
{
    public sealed class Placeholder
    {
    }
}
""";

    private const string MapperDeclarationSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace CompatibilityConsumer
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
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
        }
    }
}
""";

    private const string SuccessfulMapperProgram =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace CompatibilityConsumer
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
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
        }
    }

    internal static class Program
    {
        public static void Main()
        {
            var mapper = (ITypeMapper<Source, Destination>)new TestMapper();
            var result = mapper.Create(new Source { Value = 17 });

            if (result.Value != 17)
            {
                throw new InvalidOperationException("Unexpected mapping result.");
            }
        }
    }
}
""";

    private sealed record DirectConsumerProject(
        string ProjectPath,
        string GeneratedDirectory);

    private sealed record ProcessResult(
        int ExitCode,
        string Output,
        IReadOnlyList<string> Arguments);
}
