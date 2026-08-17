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

    private static readonly string[] PrimaryGeneratedFiles =
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

    private static readonly string[] BothGeneratedFiles =
    [
        .. PrimaryGeneratedFiles,
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
        var consumerGenerated = Path.Combine(testDirectory, "generated");
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
                "Morphant.Generator",
                "Morphant.Generator.MorphantGenerator");
            var staleMorphantFile = Path.Combine(
                morphantGeneratedDirectory,
                "Morphant.Generated.Removed.g.cs");
            var unrelatedGeneratedFile = Path.Combine(
                consumerGenerated,
                "Other.Generator",
                "OtherGenerator",
                "Unrelated.g.cs");

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
                "run",
                "--project",
                consumerProject,
                "--configuration",
                configuration,
                $"-p:MorphantTestPackageVersion={packageVersion}",
                $"-p:RestoreSources={packageFeed}",
                $"-p:BaseOutputPath={consumerOutput}",
                $"-p:BaseIntermediateOutputPath={consumerIntermediate}",
                "-p:EmitCompilerGeneratedFiles=true",
                $"-p:CompilerGeneratedFilesOutputPath={consumerGenerated}",
                "-p:NuGetAudit=false"
            ];
            var run = await DotNetCli.Run(
                repositoryRoot,
                consumerArguments);
            AssertSucceeded(run);

            Assert.Multiple(() =>
            {
                Assert.That(
                    File.Exists(staleMorphantFile),
                    Is.False,
                    "The package must remove stale Morphant generated " +
                    "files before compilation.");
                Assert.That(
                    File.Exists(unrelatedGeneratedFile),
                    Is.True,
                    "Morphant cleanup must preserve other generators' " +
                    "output.");
            });
            AssertGeneratedFileSet(
                morphantGeneratedDirectory,
                PrimaryGeneratedFiles);

            await File.WriteAllTextAsync(
                consumerSource,
                SecondMappingConsumerSource);
            var addedMappingRun = await DotNetCli.Run(
                repositoryRoot,
                consumerArguments);
            AssertSucceeded(addedMappingRun);
            AssertGeneratedFileSet(
                morphantGeneratedDirectory,
                BothGeneratedFiles);

            await File.WriteAllTextAsync(
                consumerSource,
                originalConsumerSource);
            var removedMappingRun = await DotNetCli.Run(
                repositoryRoot,
                consumerArguments);
            AssertSucceeded(removedMappingRun);
            AssertGeneratedFileSet(
                morphantGeneratedDirectory,
                PrimaryGeneratedFiles);

            var currentMorphantFiles = Directory.GetFiles(
                morphantGeneratedDirectory,
                "Morphant.Generated.*.g.cs",
                SearchOption.TopDirectoryOnly);

            var noOpRun = await DotNetCli.Run(
                repositoryRoot,
                consumerArguments);
            AssertSucceeded(noOpRun);

            Assert.That(
                currentMorphantFiles.All(File.Exists),
                Is.True,
                "An up-to-date build must preserve current generated files.");

            await File.WriteAllTextAsync(
                staleMorphantFile,
                "// stale Morphant output");
            await File.WriteAllTextAsync(
                consumerSource,
                SecondMappingConsumerSource);
            var optOutRun = await DotNetCli.Run(
                repositoryRoot,
                [
                    .. consumerArguments,
                    "-p:MorphantCleanCompilerGeneratedFiles=false"
                ]);
            AssertSucceeded(optOutRun);
            Assert.That(
                File.Exists(staleMorphantFile),
                Is.True,
                "The cleanup opt-out must preserve existing Morphant " +
                "generated files.");

            await File.WriteAllTextAsync(
                consumerSource,
                originalConsumerSource);
            var cleanupRestoredRun = await DotNetCli.Run(
                repositoryRoot,
                consumerArguments);
            AssertSucceeded(cleanupRestoredRun);
            AssertGeneratedFileSet(
                morphantGeneratedDirectory,
                PrimaryGeneratedFiles);

            await File.WriteAllTextAsync(
                staleMorphantFile,
                "// stale Morphant output");
            await File.WriteAllTextAsync(
                consumerSource,
                SecondMappingConsumerSource);
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
                    "-p:EmitCompilerGeneratedFiles=true",
                    $"-p:CompilerGeneratedFilesOutputPath={consumerGenerated}",
                    "-p:DesignTimeBuild=true",
                    "-p:NuGetAudit=false"
                ]);
            AssertSucceeded(designTimeRun);
            Assert.That(
                File.Exists(staleMorphantFile),
                Is.True,
                "A design-time build must not clean generated files used " +
                "by the editor.");

            await File.WriteAllTextAsync(
                consumerSource,
                originalConsumerSource);
            var finalRun = await DotNetCli.Run(
                repositoryRoot,
                consumerArguments);
            AssertSucceeded(finalRun);
            AssertGeneratedFileSet(
                morphantGeneratedDirectory,
                PrimaryGeneratedFiles);

            await AssertMultiTargetGeneratedFilesAreIsolated(
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
    }

    private static async Task AssertMultiTargetGeneratedFilesAreIsolated(
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
            MultiTargetConsumerProject.Replace(
                "__PACKAGE_VERSION__",
                packageVersion));
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "Mapping.cs"),
            MultiTargetConsumerSource);

        var build = await DotNetCli.Run(
            repositoryRoot,
            [
                "build",
                projectPath,
                "--configuration",
                configuration,
                "-m:1",
                "-nodeReuse:false",
                "--nologo",
                $"-p:RestoreSources={packageFeed}",
                $"-p:BaseOutputPath={outputDirectory}",
                $"-p:BaseIntermediateOutputPath={intermediateDirectory}",
                "-p:NuGetAudit=false"
            ]);
        AssertSucceeded(build);

        string[] expected =
        [
            "Morphant.Generated.Construction." +
            "MultiTarget_Destination.g.cs",
            "Morphant.Generated.MappingExtension." +
            "MultiTarget_Source__MultiTarget_Destination.g.cs",
            "Morphant.Generated.Member.MultiTarget_Destination.g.cs",
            "Morphant.Generated.MemberExtension." +
            "MultiTarget_Source__MultiTarget_Destination.g.cs",
            "Morphant.Generated.TypeMapper.MultiTarget_TestMapper.g.cs"
        ];

        foreach (var targetFramework in new[] { "netstandard2.0", "net10.0" })
        {
            AssertGeneratedFileSet(
                Path.Combine(
                    intermediateDirectory,
                    "generated",
                    targetFramework,
                    "Morphant.Generator",
                    "Morphant.Generator.MorphantGenerator"),
                expected);
        }
    }

    // lang=xml
    private const string MultiTargetConsumerProject =
"""
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>netstandard2.0;net10.0</TargetFrameworks>
    <LangVersion>9.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)generated/$(TargetFramework)</CompilerGeneratedFilesOutputPath>
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
    }

    public sealed class Destination
    {
        public int Value { get; set; } = 41;
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
            builder.Map<Source, Destination>();
            builder.Map<SecondSource, SecondDestination>();
        }
    }

    internal static class Program
    {
        public static void Main()
        {
            var mapper = new TestMapper();
            var primary =
                ((ITypeMapper<Source, Destination>)mapper).Create(
                    new Source { Value = 17 },
                    default(MappingContext));
            var second =
                ((ITypeMapper<SecondSource, SecondDestination>)mapper).Create(
                    new SecondSource { Value = 29 },
                    default(MappingContext));

            if (primary.Value != 41 || second.Value != 59)
            {
                throw new InvalidOperationException(
                    "The packaged generator did not actualize both " +
                    "mapping contracts.");
            }
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
}
