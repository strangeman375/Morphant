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
    public void Bounds_identifiers_and_preserves_complete_unicode_scalars()
    {
        var exactLimit = new string('A', 480);
        var overflow = new string('A', 481);
        var splitSurrogate =
            new string('A', 461) + "😀" + new string('B', 20);

        var boundedOverflow = HintNameHelper.LimitWithStableHash(
            overflow,
            overflow,
            maxLength: 480);
        var boundedSurrogate = HintNameHelper.LimitWithStableHash(
            splitSurrogate,
            splitSurrogate,
            maxLength: 480);

        Assert.Multiple(() =>
        {
            Assert.That(
                HintNameHelper.LimitWithStableHash(
                    exactLimit,
                    exactLimit,
                    maxLength: 480),
                Is.EqualTo(exactLimit));
            Assert.That(
                boundedOverflow,
                Is.EqualTo(
                    new string('A', 462) +
                    "__6deb43529b448a0c"));
            Assert.That(boundedOverflow, Has.Length.EqualTo(480));
            Assert.That(
                boundedSurrogate,
                Is.EqualTo(
                    new string('A', 461) +
                    "__ef7a1431162de585"));
            Assert.That(boundedSurrogate, Has.Length.EqualTo(479));
            Assert.That(
                boundedSurrogate.Any(char.IsSurrogate),
                Is.False);
        });
    }

    [Test]
    public void Bounds_hint_names_by_utf8_bytes_and_keeps_short_names_stable()
    {
        const string prefix = "Morphant.Generated.Member.";
        const string extension = ".g.cs";
        var exactIdentity = new string('A', 189);
        var overflowIdentity = new string('A', 190);
        var unicodeIdentity =
            "Tuple_" +
            new string('Ж', 82) +
            "😀" +
            new string('Z', 20);

        var exactHint = GeneratedSourceHintName.Create(
            "Member",
            exactIdentity);
        var overflowHint = GeneratedSourceHintName.Create(
            "Member",
            overflowIdentity);
        var unicodeHint = GeneratedSourceHintName.Create(
            "Member",
            unicodeIdentity);

        Assert.Multiple(() =>
        {
            Assert.That(
                exactHint,
                Is.EqualTo(prefix + exactIdentity + extension));
            Assert.That(
                Encoding.UTF8.GetByteCount(exactHint),
                Is.EqualTo(220));
            Assert.That(
                overflowHint,
                Is.EqualTo(
                    prefix +
                    new string('A', 171) +
                    "__8aa1c15210409ca2" +
                    extension));
            Assert.That(
                Encoding.UTF8.GetByteCount(overflowHint),
                Is.EqualTo(220));
            Assert.That(
                unicodeHint,
                Is.EqualTo(
                    prefix +
                    "Tuple_" +
                    new string('Ж', 82) +
                    "__b5d01df70aab29a6" +
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
            new string('Ж', 82) +
            "😀" +
            new string('Z', 20));
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
