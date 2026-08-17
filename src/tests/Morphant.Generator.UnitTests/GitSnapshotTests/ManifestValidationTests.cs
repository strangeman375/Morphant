using System.Text;
using Morphant.Build.Tasks;

namespace Morphant.Generator.UnitTests.GitSnapshotTests;

[TestFixture]
internal sealed class ManifestValidationTests
{
    [TestCase("Morphant.Generated../Outside.g.cs")]
    [TestCase("Morphant.Generated..\\Outside.g.cs")]
    [TestCase("Morphant.Generated.Aux/../Outside.g.cs")]
    public void Rejects_non_portable_or_traversing_generated_file_names(
        string fileName)
    {
        var manifest = ManifestWithFileRecords(
            FileRecord(fileName));

        var exception = Assert.Throws<SnapshotException>(() =>
            SnapshotManifestFormat.Parse(
                Encoding.UTF8.GetBytes(manifest),
                "test manifest"));

        Assert.That(exception!.Code, Is.EqualTo("MORPHANTMSB008"));
    }

    [Test]
    public void Rejects_duplicate_generated_file_names()
    {
        const string fileName = "Morphant.Generated.TypeMapper.Test.g.cs";
        var record = FileRecord(fileName);
        var manifest = ManifestWithFileRecords(record, record);

        var exception = Assert.Throws<SnapshotException>(() =>
            SnapshotManifestFormat.Parse(
                Encoding.UTF8.GetBytes(manifest),
                "test manifest"));

        Assert.That(exception!.Code, Is.EqualTo("MORPHANTMSB008"));
    }

    [Test]
    public void Rejects_generated_file_names_that_only_differ_by_case()
    {
        var manifest = ManifestWithFileRecords(
            FileRecord("Morphant.Generated.TypeMapper.Test.g.cs"),
            FileRecord("Morphant.Generated.TypeMapper.test.g.cs"));

        var exception = Assert.Throws<SnapshotException>(() =>
            SnapshotManifestFormat.Parse(
                Encoding.UTF8.GetBytes(manifest),
                "test manifest"));

        Assert.That(exception!.Code, Is.EqualTo("MORPHANTMSB008"));
    }

    [Test]
    public void Rejects_invalid_utf8_in_root_manifest_with_a_stable_diagnostic()
    {
        var exception = Assert.Throws<SnapshotException>(() =>
            SnapshotRootManifestFormat.Parse(
                [0xff, 0xfe],
                "test root manifest"));

        Assert.That(exception!.Code, Is.EqualTo("MORPHANTMSB009"));
    }

    private static string ManifestWithFileRecords(params string[] records)
    {
        string[] lines =
        [
            "MorphantGitSnapshotManifest/1",
            "project\t" + Encode("../Consumer.csproj"),
            "target-framework\t" + Encode("net10.0"),
            .. records,
            string.Empty
        ];

        return string.Join('\n', lines);
    }

    private static string FileRecord(string fileName) =>
        "file\t" + Encode(fileName) + "\t" + new string('0', 64);

    private static string Encode(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
}
