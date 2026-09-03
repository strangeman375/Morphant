using System.Reflection;
using System.Security.Cryptography;
using Morphant.Generator.UnitTests.TestAssets;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class StrongNameIdentityTests
{
    private const string ExpectedPublicKeyToken = "ba27fb6be8f80649";

    [Test]
    public void Public_key_file_matches_the_private_key_and_expected_token()
    {
        var repositoryRoot = FindRepositoryRoot();
        var publicKeyPath = Path.Combine(
            repositoryRoot,
            "src",
            "Morphant.PublicKey.snk");
        var privateKeyPath = Path.Combine(
            repositoryRoot,
            "src",
            "Morphant.snk");
        var publicKey = File.ReadAllBytes(publicKeyPath);
        var privateKey = File.ReadAllBytes(privateKeyPath);
        var privateKeyPublicPart = GetStrongNamePublicKey(privateKey);

        Assert.Multiple(() =>
        {
            Assert.That(privateKeyPublicPart, Is.EqualTo(publicKey));
            Assert.That(
                GetPublicKeyToken(publicKey),
                Is.EqualTo(ExpectedPublicKeyToken));
        });
    }

    [Test]
    public void Product_and_friend_assemblies_use_the_Morphant_strong_name()
    {
        var runtime = typeof(TypeMapper<>).Assembly.GetName();
        var generator = typeof(MorphantGenerator).Assembly.GetName();
        var unitTests = typeof(StrongNameIdentityTests).Assembly.GetName();
        var testAssets = typeof(ReferencedDestination).Assembly.GetName();

        Assert.Multiple(() =>
        {
            AssertStrongName(runtime);
            AssertStrongName(generator);
            AssertStrongName(unitTests);
            AssertStrongName(testAssets);

            Assert.That(
                generator.GetPublicKey(),
                Is.EqualTo(runtime.GetPublicKey()));
            Assert.That(
                unitTests.GetPublicKey(),
                Is.EqualTo(runtime.GetPublicKey()));
            Assert.That(
                testAssets.GetPublicKey(),
                Is.EqualTo(runtime.GetPublicKey()));
        });
    }

    private static void AssertStrongName(AssemblyName assemblyName)
    {
        var publicKeyToken = assemblyName.GetPublicKeyToken();

        Assert.That(publicKeyToken, Is.Not.Null.And.Not.Empty);
        Assert.That(
            Convert.ToHexString(publicKeyToken!).ToLowerInvariant(),
            Is.EqualTo(ExpectedPublicKeyToken));
    }

    private static byte[] GetStrongNamePublicKey(byte[] privateKey)
    {
        using var rsa = new RSACryptoServiceProvider();
        rsa.ImportCspBlob(privateKey);
        var cspPublicKey = rsa.ExportCspBlob(includePrivateParameters: false);
        cspPublicKey[5] = 0x24;
        var strongNamePublicKey = new byte[12 + cspPublicKey.Length];

        strongNamePublicKey[0] = 0x00;
        strongNamePublicKey[1] = 0x24;
        strongNamePublicKey[4] = 0x04;
        strongNamePublicKey[5] = 0x80;
        WriteLittleEndian(cspPublicKey.Length, strongNamePublicKey.AsSpan(8));
        cspPublicKey.CopyTo(strongNamePublicKey, 12);

        return strongNamePublicKey;
    }

    private static string GetPublicKeyToken(byte[] publicKey)
    {
        var hash = SHA1.HashData(publicKey);

        return Convert.ToHexString(hash[^8..].Reverse().ToArray())
            .ToLowerInvariant();
    }

    private static void WriteLittleEndian(int value, Span<byte> destination)
    {
        destination[0] = (byte)value;
        destination[1] = (byte)(value >> 8);
        destination[2] = (byte)(value >> 16);
        destination[3] = (byte)(value >> 24);
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
                    "src",
                    "Morphant.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException(
            "Could not locate the Morphant repository root.");
    }
}
