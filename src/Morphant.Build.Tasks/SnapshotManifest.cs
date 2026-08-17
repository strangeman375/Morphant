using System.Security.Cryptography;
using System.Text;

namespace Morphant.Build.Tasks;

internal sealed record SnapshotFile(string Name, string Hash, byte[] Contents);

internal sealed record SnapshotManifest(
    string ProjectIdentity,
    string TargetFramework,
    IReadOnlyList<SnapshotFile> Files)
{
    public byte[] Serialize()
    {
        var result = new StringBuilder()
            .AppendLine(SnapshotManifestFormat.Header)
            .Append("project\t")
            .AppendLine(SnapshotManifestFormat.Encode(ProjectIdentity))
            .Append("target-framework\t")
            .AppendLine(SnapshotManifestFormat.Encode(TargetFramework));

        foreach (var file in Files.OrderBy(
                     static file => file.Name,
                     StringComparer.Ordinal))
        {
            result
                .Append("file\t")
                .Append(SnapshotManifestFormat.Encode(file.Name))
                .Append('\t')
                .AppendLine(file.Hash);
        }

        return SnapshotManifestFormat.Utf8.GetBytes(
            result.ToString().Replace("\r\n", "\n"));
    }
}

internal static class SnapshotManifestFormat
{
    public const string FileName = "Morphant.Generated.manifest";
    public const string Header = "MorphantGitSnapshotManifest/1";

    public static UTF8Encoding Utf8 { get; } = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static SnapshotManifest Parse(
        byte[] contents,
        string sourceDescription)
    {
        var lines = ReadLines(contents, sourceDescription);

        if (lines.Count < 3 || lines[0] != Header)
        {
            throw Invalid(sourceDescription, "unsupported or missing header");
        }

        var project = ReadScalar(lines[1], "project", sourceDescription);
        var targetFramework = ReadScalar(
            lines[2],
            "target-framework",
            sourceDescription);
        var files = new List<SnapshotFile>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines.Skip(3))
        {
            var fields = line.Split(['\t']);

            if (fields.Length != 3 || fields[0] != "file")
            {
                throw Invalid(sourceDescription, "malformed file record");
            }

            var name = Decode(fields[1], sourceDescription);

            if (!IsGeneratedFileName(name) ||
                !PortablePath.IsSafeComponent(name) ||
                Path.GetFileName(name) != name ||
                !names.Add(name))
            {
                throw Invalid(
                    sourceDescription,
                    "unsafe, duplicate, or non-Morphant file name");
            }

            if (!IsSha256(fields[2]))
            {
                throw Invalid(sourceDescription, "invalid SHA-256 value");
            }

            files.Add(new SnapshotFile(name, fields[2], []));
        }

        if (!files.Select(static file => file.Name)
                .SequenceEqual(
                    files.Select(static file => file.Name)
                        .OrderBy(static name => name, StringComparer.Ordinal)))
        {
            throw Invalid(sourceDescription, "file records are not sorted");
        }

        return new SnapshotManifest(
            project,
            targetFramework,
            files);
    }

    public static byte[] CanonicalSourceBytes(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var offset = bytes.Length >= 3 &&
                     bytes[0] == 0xef &&
                     bytes[1] == 0xbb &&
                     bytes[2] == 0xbf
            ? 3
            : 0;
        var text = Utf8.GetString(bytes, offset, bytes.Length - offset)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');

        return Utf8.GetBytes(text.Replace("\n", "\r\n"));
    }

    public static string Hash(byte[] contents)
    {
        using var sha = SHA256.Create();
        return string.Concat(
            sha.ComputeHash(contents)
                .Select(static value => value.ToString("x2")));
    }

    public static bool IsGeneratedFileName(string name) =>
        name.StartsWith("Morphant.Generated.", StringComparison.Ordinal) &&
        name.EndsWith(".g.cs", StringComparison.Ordinal);

    public static string Encode(string value) =>
        Convert.ToBase64String(Utf8.GetBytes(value));

    public static string Decode(string value, string sourceDescription)
    {
        try
        {
            return Utf8.GetString(Convert.FromBase64String(value));
        }
        catch (Exception exception) when (
            exception is FormatException or DecoderFallbackException)
        {
            throw Invalid(sourceDescription, "invalid encoded value");
        }
    }

    private static IReadOnlyList<string> ReadLines(
        byte[] contents,
        string sourceDescription)
    {
        string text;

        try
        {
            text = Utf8.GetString(contents);
        }
        catch (DecoderFallbackException)
        {
            throw Invalid(sourceDescription, "content is not valid UTF-8");
        }

        if (text.Length > 0 && text[0] == '\ufeff')
        {
            throw Invalid(sourceDescription, "UTF-8 BOM is not allowed");
        }

        if (text.Contains('\r'))
        {
            throw Invalid(sourceDescription, "only LF line endings are allowed");
        }

        var lines = text.Split(['\n']);
        var count = lines.Length;

        if (count > 0 && lines[count - 1].Length == 0)
        {
            count--;
        }

        return lines.Take(count).ToArray();
    }

    private static string ReadScalar(
        string line,
        string expectedName,
        string sourceDescription)
    {
        var fields = line.Split(['\t']);

        if (fields.Length != 2 || fields[0] != expectedName)
        {
            throw Invalid(
                sourceDescription,
                $"missing or malformed '{expectedName}' record");
        }

        return Decode(fields[1], sourceDescription);
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(static character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static SnapshotException Invalid(
        string sourceDescription,
        string reason) =>
        new(
            "MORPHANTMSB008",
            $"The {sourceDescription} is invalid: {reason}. Morphant will " +
            "not delete snapshot files based on untrusted state.");
}

internal sealed record SnapshotRootEntry(
    string SliceRelativePath,
    string ManifestHash);

internal sealed record SnapshotRootManifest(
    string ProjectIdentity,
    IReadOnlyList<SnapshotRootEntry> Entries)
{
    public byte[] Serialize()
    {
        var result = new StringBuilder()
            .AppendLine(SnapshotRootManifestFormat.Header)
            .Append("project\t")
            .AppendLine(SnapshotManifestFormat.Encode(ProjectIdentity));

        foreach (var entry in Entries.OrderBy(
                     static entry => entry.SliceRelativePath,
                     StringComparer.Ordinal))
        {
            result
                .Append("slice\t")
                .Append(SnapshotManifestFormat.Encode(
                    entry.SliceRelativePath))
                .Append('\t')
                .AppendLine(entry.ManifestHash);
        }

        return SnapshotManifestFormat.Utf8.GetBytes(
            result.ToString().Replace("\r\n", "\n"));
    }
}

internal static class SnapshotRootManifestFormat
{
    public const string FileName = "Morphant.Generated.root.manifest";
    public const string Header = "MorphantGitSnapshotRoot/1";

    public static SnapshotRootManifest Parse(
        byte[] contents,
        string sourceDescription)
    {
        string text;

        try
        {
            text = SnapshotManifestFormat.Utf8.GetString(contents);
        }
        catch (DecoderFallbackException)
        {
            throw Invalid(sourceDescription, "content is not valid UTF-8");
        }

        if ((text.Length > 0 && text[0] == '\ufeff') || text.Contains('\r'))
        {
            throw Invalid(sourceDescription, "non-canonical encoding");
        }

        var lines = text.Split(['\n']);
        var count = lines.Length;

        if (count > 0 && lines[count - 1].Length == 0)
        {
            count--;
        }

        if (count < 2 || lines[0] != Header)
        {
            throw Invalid(sourceDescription, "unsupported or missing header");
        }

        var projectFields = lines[1].Split(['\t']);

        if (projectFields.Length != 2 || projectFields[0] != "project")
        {
            throw Invalid(sourceDescription, "missing project identity");
        }

        var project = SnapshotManifestFormat.Decode(
            projectFields[1],
            sourceDescription);
        var entries = new List<SnapshotRootEntry>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 2; index < count; index++)
        {
            var fields = lines[index].Split(['\t']);

            if (fields.Length != 3 || fields[0] != "slice")
            {
                throw Invalid(sourceDescription, "malformed slice record");
            }

            var path = SnapshotManifestFormat.Decode(
                fields[1],
                sourceDescription);

            if (!IsSafeSlicePath(path) || !paths.Add(path) ||
                fields[2].Length != 64 ||
                !fields[2].All(static character =>
                    character is >= '0' and <= '9' or >= 'a' and <= 'f'))
            {
                throw Invalid(
                    sourceDescription,
                    "unsafe, duplicate, or invalid slice record");
            }

            entries.Add(new SnapshotRootEntry(path, fields[2]));
        }

        if (!entries.Select(static entry => entry.SliceRelativePath)
                .SequenceEqual(entries
                    .Select(static entry => entry.SliceRelativePath)
                    .OrderBy(static path => path, StringComparer.Ordinal)))
        {
            throw Invalid(sourceDescription, "slice records are not sorted");
        }

        return new SnapshotRootManifest(project, entries);
    }

    private static bool IsSafeSlicePath(string value)
    {
        var parts = value.Split(['/']);
        return parts.Length == 1 && PortablePath.IsSafeComponent(parts[0]);
    }

    private static SnapshotException Invalid(
        string sourceDescription,
        string reason) =>
        new(
            "MORPHANTMSB009",
            $"The {sourceDescription} is invalid: {reason}. Morphant will " +
            "not change a snapshot root with untrusted ownership metadata.");
}
