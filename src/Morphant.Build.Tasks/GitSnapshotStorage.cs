using System.Text;

namespace Morphant.Build.Tasks;

internal static class GitSnapshotStorage
{
    internal const string OwnerDirectoryName = ".morphant";
    internal const string OwnerFileName = "owner";

    public static void CheckOwner(string root, string owner, bool create, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PhysicalDirectory.EnsureNoLinks(root, root, "Morphant snapshot storage");
        var directory = Path.Combine(root, OwnerDirectoryName);
        var path = Path.Combine(directory, OwnerFileName);
        EnsureOwnerDirectory();
        if (Directory.Exists(directory))
        {
            Validate();
            return;
        }
        if (!create)
            return;

        Directory.CreateDirectory(root);
        var temporary = Path.Combine(root, OwnerDirectoryName + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            Directory.CreateDirectory(temporary);
            File.WriteAllText(Path.Combine(temporary, OwnerFileName), owner, new UTF8Encoding(false));
            cancellationToken.ThrowIfCancellationRequested();
            try { Directory.Move(temporary, directory); }
            catch (IOException) when (Directory.Exists(directory))
            {
                // A prepared nonempty directory cannot replace another nonempty
                // directory. The winner's complete owner record stays immutable.
                EnsureOwnerDirectory();
                Validate();
            }
        }
        finally
        {
            if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
        }

        void EnsureOwnerDirectory()
        {
            if (PhysicalDirectory.IsLink(directory))
                throw new SnapshotException("MORPHANTMSB016", $"Morphant ownership directory '{directory}' is a link.");
            if (File.Exists(directory))
                throw new SnapshotException("MORPHANTMSB015",
                    $"Morphant ownership path '{directory}' is a file. Remove the previous-format ownership file " +
                    "and rebuild to create '.morphant/owner'.");
            PhysicalDirectory.EnsureNoLinks(root, directory, "Morphant ownership directory");
        }

        void Validate()
        {
            if (PhysicalDirectory.IsLink(path))
                throw new SnapshotException("MORPHANTMSB016", $"Morphant ownership file '{path}' is a link.");
            if (Directory.Exists(path))
                throw new SnapshotException("MORPHANTMSB015", $"Morphant ownership file '{path}' names a directory.");
            if (!File.Exists(path))
                throw InvalidRecord();
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            if (stream.Length > 65536)
                throw InvalidRecord();
            var actual = Normalize(reader.ReadToEnd());
            var expected = Normalize(owner);
            var recordedProject = ProjectName(actual);
            if (recordedProject is null)
                throw InvalidRecord();
            if (!string.Equals(actual, expected, PhysicalDirectory.Comparison))
                throw new SnapshotException("MORPHANTMSB005",
                    $"Morphant snapshot directory '{root}' records project '{recordedProject}', " +
                    $"but the current project is '{ProjectName(expected)}'. Use a separate snapshot directory. " +
                    $"If this project was renamed or moved, remove '{directory}' and rebuild.");
        }

        SnapshotException InvalidRecord() => new("MORPHANTMSB005",
            $"Morphant ownership record '{path}' is missing or invalid. Restore it from Git, or remove " +
            $"'{directory}' and rebuild if this snapshot belongs to the current project '{ProjectName(Normalize(owner))}'.");
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n");

    private static string? ProjectName(string record)
    {
        var lines = record.Split('\n');
        return lines.Length == 3 && lines[0] == "Morphant Git snapshot 1" &&
            lines[1].Length > 0 && !lines[1].Any(char.IsControl) && lines[2].Length == 0 ? lines[1] : null;
    }
}
