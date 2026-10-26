namespace Morphant.Generator.IntegrationTests.PackageConsumptionTests;

internal sealed partial class PackageConsumptionTests
{
    [Test]
    public async Task Target_framework_snapshots_are_published_and_preserved_independently()
    {
        var projectDirectory = Path.Combine(
            _testDirectory,
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
            MultiTargetConsumerProjectText(PackageVersion));
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "Mapping.cs"),
            MultiTargetConsumerSource);

        string[] buildArguments =
        [
            "build",
            projectPath,
            "--configuration",
            Configuration,
            string.Equals(
                Environment.GetEnvironmentVariable("CI"),
                "true",
                StringComparison.OrdinalIgnoreCase)
                ? "-m"
                : "-m:1",
            "-nodeReuse:false",
            "--nologo",
            $"-p:RestoreSources={PackageFeed}",
            $"-p:BaseOutputPath={outputDirectory}",
            $"-p:BaseIntermediateOutputPath={intermediateDirectory}",
            $"-p:MorphantGitSnapshotPath={Path.Combine(projectDirectory, "generated")}",
            "-p:NuGetAudit=false"
        ];
        var build = await DotNetCli.Run(RepositoryRoot, buildArguments);
        AssertSucceeded(build);

        string[] expected =
        [
            "Morphant.Generated.TypeMapper.TestMapper__a79d3f887d0c250a61f195c37d2f4dcd.g.cs"
        ];

        foreach (var targetFramework in new[] { "netstandard2.0", "net10.0" })
        {
            AssertGeneratedFileSet(
                Path.Combine(
                    projectDirectory,
                    "generated",
                    targetFramework),
                expected);
        }

        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "generated", "net10.0",
            "Morphant.Generated.TypeMapper.Stale.g.cs"), "// Preserve this file while snapshots are disabled.");
        var net10Snapshot = SnapshotContents(Path.Combine(projectDirectory, "generated", "net10.0"));
        var netstandardSnapshot = SnapshotContents(Path.Combine(projectDirectory, "generated", "netstandard2.0"));
        await File.WriteAllTextAsync(
            projectPath,
            MultiTargetConsumerProjectText(
                PackageVersion,
                disabledFramework: "net10.0"));
        var conditionallyEnabledBuild = await DotNetCli.Run(
            RepositoryRoot,
            [.. buildArguments, "-t:Rebuild"]);
        AssertSucceeded(conditionallyEnabledBuild);
        Assert.Multiple(() =>
        {
            AssertGeneratedFileSet(
                Path.Combine(
                    projectDirectory,
                    "generated",
                    "netstandard2.0"),
                expected);
            Assert.That(
                SnapshotContents(Path.Combine(projectDirectory, "generated", "net10.0")),
                Is.EqualTo(net10Snapshot),
                "A disabled framework's snapshot must remain intact.");
        });

        await File.WriteAllTextAsync(
            projectPath,
            MultiTargetConsumerProjectText(
                PackageVersion,
                targetFrameworks: "net10.0"));
        var removedTargetFrameworkBuild = await DotNetCli.Run(
            RepositoryRoot,
            [.. buildArguments, "-t:Rebuild"]);
        AssertSucceeded(removedTargetFrameworkBuild);
        Assert.Multiple(() =>
        {
            Assert.That(
                SnapshotContents(Path.Combine(projectDirectory, "generated", "netstandard2.0")),
                Is.EqualTo(netstandardSnapshot),
                "Removing a framework must not delete another snapshot slice during publication.");
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
        string? disabledFramework = null)
    {
        var snapshotProperty = disabledFramework is null
            ? string.Empty
            : $"<MorphantGitSnapshot Condition=\"'$(TargetFramework)' == '{disabledFramework}'\">false</MorphantGitSnapshot>";

        return MultiTargetConsumerProjectTemplate
            .Replace("__PACKAGE_VERSION__", packageVersion)
            .Replace("__TARGET_FRAMEWORKS__", targetFrameworks)
            .Replace("__SNAPSHOT_CONDITION__", snapshotProperty);
    }

    // lang=xml
    private const string MultiTargetConsumerProjectTemplate =
"""
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>__TARGET_FRAMEWORKS__</TargetFrameworks>
    <MorphantGitSnapshot>true</MorphantGitSnapshot>
    __SNAPSHOT_CONDITION__
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
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";
}
