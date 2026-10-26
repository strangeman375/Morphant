using System.Globalization;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace Morphant.Generator.IntegrationTests.PackageConsumptionTests;

internal sealed partial class PackageConsumptionTests
{
    [Test]
    public void Package_contains_complete_assets_and_metadata()
    {
        const string expectedPublicKeyToken = "ba27fb6be8f80649";
        var productVersion = XDocument.Load(Path.Combine(
                RepositoryRoot,
                "src",
                "Morphant.Product.props"))
            .Descendants("Version")
            .Single()
            .Value;
        var packagePath = Path.Combine(
            PackageFeed,
            $"Morphant.{PackageVersion}.nupkg");

        using var package = ZipFile.OpenRead(packagePath);

        Assert.Multiple(() =>
        {
            AssertPackagePayload(package);
            AssertPackagedRepositoryFiles(package, RepositoryRoot);
            AssertPackageMetadata(
                package,
                PackageVersion,
                productVersion);
            AssertBuildTransitiveProperties(package);
            AssertStrongName(
                package,
                "lib/netstandard2.0/Morphant.dll",
                expectedPublicKeyToken);
            AssertStrongName(
                package,
                "analyzers/dotnet/cs/Morphant.Generator.dll",
                expectedPublicKeyToken);
            AssertStrongName(
                package,
                "buildTransitive/Morphant.Build.Tasks.dll",
                expectedPublicKeyToken);
            AssertSymbolPackage(PackageFeed, PackageVersion);
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
                "buildTransitive/Morphant.Build.Tasks.dll",
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
        string packageVersion,
        string productVersion)
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
            Assert.That(
                Value("releaseNotes"),
                Does.Contain($"blob/v{productVersion}/CHANGELOG.md"));
            Assert.That(
                Value("releaseNotes"),
                Does.Contain($"blob/v{productVersion}/docs/limitations.md"));
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

        Assert.Multiple(() =>
        {
            Assert.That(
                properties,
                Is.EqualTo(new[]
                {
                    "MorphantMappingMode",
                    "MorphantNullSourceHandling",
                    "MorphantNullDestinationHandling",
                    "MorphantUnknownDerivedTypeHandling",
                    "MorphantConstructorSelection",
                    "MorphantMemberSelection",
                    "MorphantFlattening",
                    "MorphantUnmappedMemberValidation"
                }));
            Assert.That(
                document.Descendants("DefaultItemExcludes").Single().Value,
                Is.EqualTo(
                    "$(DefaultItemExcludes);" +
                    "**/Morphant.Generated.*.g.cs"));
        });
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
}
