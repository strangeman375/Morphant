using System.Text;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class GeneratedNameLengthTests
{
    [Test]
    public void Creates_a_stable_lowercase_128_bit_identity_hash()
    {
        Assert.That(
            HintNameHelper.GetStableHash128("Morphant tuple identity"),
            Is.EqualTo("5f7d6028f7cede391ce9731ad53c155e"));
    }

    [Test]
    public void Bounds_hint_names_by_utf8_bytes_and_preserves_permanent_ids()
    {
        const string prefix = "Morphant.Generated.Member.";
        const string extension = ".g.cs";
        var exactIdentity = new string('A', 155);
        var overflowIdentity = new string('A', 156);
        var unicodeIdentity =
            "Tuple_" +
            new string('Ж', 74) +
            "😀" +
            new string('Z', 20);

        var exactHint = GeneratedSourceHintName.Create(
            "Member",
            exactIdentity,
            "5f7d6028f7cede391ce9731ad53c155e");
        var overflowHint = GeneratedSourceHintName.Create(
            "Member",
            overflowIdentity,
            "5f7d6028f7cede391ce9731ad53c155e");
        var unicodeHint = GeneratedSourceHintName.Create(
            "Member",
            unicodeIdentity,
            "5f7d6028f7cede391ce9731ad53c155e");

        Assert.Multiple(() =>
        {
            Assert.That(
                exactHint,
                Is.EqualTo(prefix + exactIdentity +
                    "__5f7d6028f7cede391ce9731ad53c155e" + extension));
            Assert.That(
                Encoding.UTF8.GetByteCount(exactHint),
                Is.EqualTo(220));
            Assert.That(
                overflowHint,
                Is.EqualTo(
                    prefix +
                    new string('A', 155) +
                    "__5f7d6028f7cede391ce9731ad53c155e" +
                    extension));
            Assert.That(
                Encoding.UTF8.GetByteCount(overflowHint),
                Is.EqualTo(220));
            Assert.That(
                unicodeHint,
                Is.EqualTo(
                    prefix +
                    "Tuple_" +
                    new string('Ж', 74) +
                    "__5f7d6028f7cede391ce9731ad53c155e" +
                    extension));
            Assert.That(
                Encoding.UTF8.GetByteCount(unicodeHint),
                Is.EqualTo(219));
            Assert.That(unicodeHint, Does.Not.Contain("😀"));
        });
    }

    [Test]
    public void Writes_a_bounded_unicode_hint_as_one_file_component()
    {
        var hintName = GeneratedSourceHintName.Create(
            "Member",
            "Tuple_" +
            new string('Ж', 74) +
            "😀" +
            new string('Z', 20),
            "5f7d6028f7cede391ce9731ad53c155e");
        var root = Path.Combine(
            Path.GetTempPath(),
            nameof(GeneratedNameLengthTests),
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, hintName);

            File.WriteAllText(path, "generated");

            Assert.That(File.ReadAllText(path), Is.EqualTo("generated"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
