using System.IO.Compression;
using System.Xml.Linq;

namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class PackageBuildTests
{
    [TestCase("Artifacts", "Release")]
    [TestCase("BaseOutputPath", "Debug")]
    public async Task Packs_current_tool_assemblies_from_the_selected_build_output(string layout, string configuration)
    {
        var root = Path.Combine(Path.GetTempPath(), nameof(PackageBuildTests), Guid.NewGuid().ToString("N"));
        var version = $"0.0.0-package-build.{Guid.NewGuid():N}";
        var feed = Path.Combine(root, "feed");
        var outputs = Path.Combine(root, "outputs");
        Directory.CreateDirectory(feed);

        try
        {
            CopyProductSources(root);
            // Old default outputs must never win over the current output path.
            foreach (var project in new[] { "Morphant.Generator", "Morphant.Build.Tasks" })
            {
                var oldOutput = Path.Combine(root, "src", project, "bin", configuration, "netstandard2.0");
                Directory.CreateDirectory(oldOutput);
                File.WriteAllText(Path.Combine(oldOutput, project + ".dll"), "stale output");
            }

            string[] outputArguments = [];
            if (layout == "Artifacts")
            {
                outputArguments = ["--artifacts-path", outputs];
            }
            else
            {
                var propsPath = Path.Combine(root, "src", "Directory.Build.props");
                var props = XDocument.Load(propsPath);
                props.Root!.Add(new XElement("PropertyGroup", new XElement("BaseOutputPath",
                    "$(MSBuildThisFileDirectory)../outputs/$(MSBuildProjectName)/")));
                props.Save(propsPath);
            }

            string[] packArguments =
            [
                "pack", Path.Combine(root, "src", "Morphant", "Morphant.csproj"),
                "--configuration", configuration, "--output", feed,
                "-m:1", "-nodeReuse:false", "-p:UseSharedCompilation=false",
                $"-p:PackageVersion={version}", $"-p:RestoreSources={feed}",
                "-p:NuGetAudit=false", .. outputArguments
            ];
            AssertSucceeded(await DotNetCli.Run(root, packArguments));
            var packagePath = Path.Combine(feed, $"Morphant.{version}.nupkg");
            AssertCurrentAssemblies(packagePath, outputs, layout);

            // A later packaging-only CI step must use those same assemblies.
            foreach (var project in new[] { "Morphant.Generator", "Morphant.Build.Tasks" })
                File.WriteAllText(Path.Combine(root, "src", project, "PackagingOnly.cs"),
                    "#error Packaging must not rebuild the tool projects.");
            File.Delete(packagePath);
            AssertSucceeded(await DotNetCli.Run(root, [.. packArguments, "--no-build", "--no-restore"]));
            AssertCurrentAssemblies(packagePath, outputs, layout);

            var consumerDirectory = Path.Combine(root, "consumer");
            Directory.CreateDirectory(consumerDirectory);
            File.WriteAllText(Path.Combine(consumerDirectory, "Consumer.csproj"),
                ConsumerProject.Replace("__VERSION__", version, StringComparison.Ordinal));
            File.WriteAllText(Path.Combine(consumerDirectory, "Program.cs"), ConsumerSource);
            AssertSucceeded(await DotNetCli.Run(root,
            [
                "run", "--project", Path.Combine(consumerDirectory, "Consumer.csproj"),
                "--configuration", configuration, $"-p:RestoreSources={feed}",
                "-m:1", "-nodeReuse:false",
                "-p:UseSharedCompilation=false", "-p:NuGetAudit=false"
            ]));
            var snapshots = Directory.GetFiles(Path.Combine(consumerDirectory, "Generated", "Morphant", "net10.0"),
                "*.g.cs");
            Assert.That(snapshots.Select(Path.GetFileName), Is.EqualTo(new[]
            {
                "Morphant.Generated.TypeMapper.PackageConsumer_TestMapper.g.cs"
            }));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void AssertCurrentAssemblies(string packagePath, string outputs, string layout)
    {
        using var package = ZipFile.OpenRead(packagePath);
        var outputDirectory = layout == "Artifacts" ? Path.Combine(outputs, "bin") : outputs;
        foreach (var (assembly, entry) in new[]
        {
            ("Morphant", "lib/netstandard2.0/Morphant.dll"),
            ("Morphant.Generator", "analyzers/dotnet/cs/Morphant.Generator.dll"),
            ("Morphant.Build.Tasks", "buildTransitive/Morphant.Build.Tasks.dll")
        })
        {
            var compiled = Directory.GetFiles(outputDirectory, assembly + ".dll", SearchOption.AllDirectories).Single();
            var packaged = package.GetEntry(entry);
            Assert.That(packaged, Is.Not.Null, entry);
            using var stream = packaged!.Open();
            using var contents = new MemoryStream();
            stream.CopyTo(contents);
            Assert.That(contents.ToArray(), Is.EqualTo(File.ReadAllBytes(compiled)),
                $"{entry} must contain the current assembly from {compiled}.");
        }
    }

    private static void CopyProductSources(string root)
    {
        var repository = IntegrationTestEnvironment.RepositoryRoot;
        foreach (var project in new[] { "Morphant", "Morphant.Generator", "Morphant.Build.Tasks" })
        {
            var sourceDirectory = Path.Combine(repository, "src", project);
            foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceDirectory, file);
                if (relative.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj")) continue;
                var destination = Path.Combine(root, "src", project, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }
        }

        foreach (var path in new[]
        {
            "global.json", "README.md", "LICENSE", "logo.png",
            "src/Directory.Build.props", "src/Morphant.Product.props", "src/Morphant.snk"
        })
        {
            File.Copy(Path.Combine(repository, path), Path.Combine(root, path));
        }
    }

    private static void AssertSucceeded(ProcessResult result) => Assert.That(result.ExitCode, Is.Zero,
        result.Command + Environment.NewLine + result.Output);

    // lang=xml
    private const string ConsumerProject =
"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <MorphantGitSnapshot>true</MorphantGitSnapshot>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Morphant" Version="__VERSION__" />
  </ItemGroup>
</Project>
""";

    // lang=c#
    private const string ConsumerSource =
"""
#nullable enable
using Morphant;
namespace PackageConsumer
{
    public sealed class Source { public int Value { get; set; } }
    public sealed class Destination { public int Value { get; set; } }
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((source, _) => new() { Value = source.Value + 10 });
    }
    public static class Program
    {
        public static void Main()
        {
            var mapper = (ITypeMapper<Source, Destination>)new TestMapper();
            var existing = new Destination();
            var created = mapper.Create(new Source { Value = 7 });
            var updated = mapper.Update(new Source { Value = 13 }, existing);
            if (created.Value != 17 || updated.Value != 23 || !object.ReferenceEquals(existing, updated))
                throw new System.InvalidOperationException("The packaged mapper did not apply the configured mappings.");
        }
    }
}
""";
}
