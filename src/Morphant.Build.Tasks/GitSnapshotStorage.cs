using System.Text;

namespace Morphant.Build.Tasks;

internal static class GitSnapshotStorage
{
    internal const string OwnerFileName = ".morphant";

    public static void CheckOwner(string root, string owner, bool create, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PhysicalDirectory.EnsureNoLinks(root, root, "Morphant snapshot storage");
        var path = Path.Combine(root, OwnerFileName);
        EnsureOwnerFile();
        if (File.Exists(path))
        {
            Validate();
            return;
        }
        if (!create)
            return;

        Directory.CreateDirectory(root);
        var temporary = Path.Combine(root, OwnerFileName + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, owner, new UTF8Encoding(false));
            cancellationToken.ThrowIfCancellationRequested();
            try { File.Move(temporary, path); }
            catch (IOException) when (File.Exists(path))
            {
                // Independent TFM publications may initialize the shared project
                // record together. Only a complete record becomes visible.
                EnsureOwnerFile();
                Validate();
            }
        }
        finally { File.Delete(temporary); }

        void EnsureOwnerFile()
        {
            if (PhysicalDirectory.IsLink(path))
                throw new SnapshotException("MORPHANTMSB016", $"Morphant ownership file '{path}' is a link.");
            if (Directory.Exists(path))
                throw new SnapshotException("MORPHANTMSB015", $"Morphant ownership file '{path}' names a directory.");
        }

        void Validate()
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            if (stream.Length > 65536 || !string.Equals(Normalize(reader.ReadToEnd()), Normalize(owner), PhysicalDirectory.Comparison))
                throw new SnapshotException("MORPHANTMSB005",
                    $"Morphant directory '{root}' belongs to another project. Use a separate directory; " +
                    $"its ownership is recorded in '{OwnerFileName}'.");
        }
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n");
}
