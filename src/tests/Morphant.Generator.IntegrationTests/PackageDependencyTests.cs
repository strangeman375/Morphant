using System.Xml.Linq;

namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class PackageDependencyTests
{
    private readonly ConsumerBuildWorkspace packages = new();
    private readonly string version = $"0.0.0-dependency.{Guid.NewGuid():N}";

    [OneTimeSetUp]
    public async Task Pack() => AssertSucceeded(await packages.PackMorphant(version));

    [OneTimeTearDown]
    public void DisposePackages() => packages.Dispose();

    [TestCase("ProjectReference")]
    [TestCase("PackageReference")]
    public async Task Uses_transitive_runtime_and_build_assets_then_adds_a_local_mapper(string referenceKind)
    {
        var root = Path.Combine(Path.GetTempPath(), nameof(PackageDependencyTests), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var library = CreateProject("MappingLibrary", LibrarySource, LibraryProject.Replace("__VERSION__", version));
            if (referenceKind == "PackageReference")
                AssertSucceeded(await Run("pack", library, "--output", packages.PackageFeed));

            var reference = referenceKind == "ProjectReference"
                ? new XElement("ProjectReference", new XAttribute("Include", library))
                : new XElement("PackageReference", new XAttribute("Include", "MappingLibrary"), new XAttribute("Version", version));
            var consumerXml = XDocument.Parse(ConsumerProject);
            consumerXml.Root!.Add(new XElement("ItemGroup", reference));
            var consumer = CreateProject("Consumer", RuntimeConsumerSource, consumerXml.ToString());

            // Only the intermediate library references Morphant directly.
            AssertSucceeded(await Run("run", "--project", consumer));
            AssertRuntimeAssets(consumer);

            // A project declaring its own mapper explicitly installs the generator.
            consumerXml.Root.Add(new XElement("ItemGroup", new XElement("PackageReference",
                new XAttribute("Include", "Morphant"), new XAttribute("Version", version))));
            consumerXml.Save(consumer);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(consumer)!, "Program.cs"), LocalMapperSource);
            AssertSucceeded(await Run("run", "--project", consumer));
            AssertRuntimeAssets(consumer);
            var snapshot = Path.Combine(Path.GetDirectoryName(consumer)!, "Generated", "Morphant", "net10.0");
            Assert.That(Directory.GetFiles(snapshot, "*.g.cs").Select(Path.GetFileName), Is.EqualTo(new[]
            {
                "Morphant.Generated.TypeMapper.Consumer_TestMapper.g.cs"
            }));

            string CreateProject(string name, string source, string project)
            {
                var directory = Path.Combine(root, name);
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, name + ".csproj");
                File.WriteAllText(path, project);
                File.WriteAllText(Path.Combine(directory, "Program.cs"), source);
                return path;
            }

            Task<ProcessResult> Run(params string[] arguments) => DotNetCli.Run(
                IntegrationTestEnvironment.RepositoryRoot,
                [.. arguments, "--configuration", "Release", "-p:UseSharedCompilation=false",
                    "-m:1", "-nodeReuse:false",
                    $"-p:RestoreSources={packages.PackageFeed}", "-p:NuGetAudit=false"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void AssertRuntimeAssets(string project)
    {
        var output = Path.Combine(Path.GetDirectoryName(project)!, "bin", "Release", "net10.0");
        Assert.That(Directory.GetFiles(output, "Morphant*.dll").Select(Path.GetFileName),
            Is.EqualTo(new[] { "Morphant.dll" }),
            "The generator and build task must not become application runtime dependencies.");
    }

    private static void AssertSucceeded(ProcessResult result) => Assert.That(result.ExitCode, Is.Zero,
        result.Command + Environment.NewLine + result.Output);

    // lang=xml
    private const string LibraryProject =
"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <PackageVersion>__VERSION__</PackageVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Morphant" Version="__VERSION__" />
  </ItemGroup>
</Project>
""";

    // lang=xml
    private const string ConsumerProject =
"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <MorphantGitSnapshot>true</MorphantGitSnapshot>
    <MorphantMemberSelection>Explicit</MorphantMemberSelection>
  </PropertyGroup>
  <Target Name="VerifyTransitiveBuildAssets" BeforeTargets="CoreCompile">
    <ItemGroup>
      <MissingProperty Include="MorphantMemberSelection" />
      <MissingProperty Remove="@(CompilerVisibleProperty)" />
    </ItemGroup>
    <Error Condition="'@(MissingProperty)' != ''" Text="Transitive compiler-visible properties were not imported." />
    <Error Condition="'$(MorphantGitSnapshotPath)' == ''" Text="Transitive snapshot targets were not imported." />
  </Target>
</Project>
""";

    // lang=c#
    private const string LibrarySource =
"""
#nullable enable
using Morphant;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Consumer")]
namespace MappingLibrary
{
    public sealed class Source { public int Value { get; set; } public int Other { get; set; } }
    public sealed class Destination { public int Value { get; set; } public int Other { get; set; } = 43; }
    [MorphantMapper]
    public partial class LibraryMapper : TypeMapper<LibraryMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((source, _) => new() { Value = source.Value + 1 });
    }
    public static class Factory
    {
        public static ITypeMapper<Source, Destination> CreateMapper() => new LibraryMapper();
    }
}
""";

    // lang=c#
    private const string RuntimeConsumerSource =
"""
using Morphant;
using MappingLibrary;
namespace Consumer
{
    public static class Program
    {
        public static void Main()
        {
            var mapper = Factory.CreateMapper();
            if (mapper.Create(new Source { Value = 7 }).Value != 8)
                throw new System.InvalidOperationException("The transitive runtime dependency did not work.");
        }
    }
}
""";

    // lang=c#
    private const string LocalMapperSource =
"""
#nullable enable
using Morphant;
using MappingLibrary;
namespace Consumer
{
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
            var source = new Source { Value = 7, Other = 73 };
            var mapper = (ITypeMapper<Source, Destination>)new TestMapper();
            var result = mapper.Create(source);
            var existing = new Destination();
            var updated = mapper.Update(source, existing);
            if (result.Value != 17 || result.Other != 43 || updated.Value != 17 ||
                !object.ReferenceEquals(existing, updated) || Factory.CreateMapper().Create(source).Value != 8)
                throw new System.InvalidOperationException("The two packaged mapper scopes or MSBuild settings conflicted.");
        }
    }
}
""";
}
