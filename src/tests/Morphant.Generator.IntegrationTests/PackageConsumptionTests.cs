using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class PackageConsumptionTests
{
    [Test]
    public async Task Packs_complete_assets_and_imports_buildTransitive_settings()
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
        var packageVersion =
            $"0.0.0-package-consumption.{Guid.NewGuid():N}";
        var configuration = IntegrationTestEnvironment.BuildConfiguration;

        Directory.CreateDirectory(packageFeed);

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

            var run = await DotNetCli.Run(
                repositoryRoot,
                [
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
                    "-p:NuGetAudit=false"
                ]);
            AssertSucceeded(run);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

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
            Assert.That(Value("copyright"), Does.Contain("2026"));
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
