using System.Text;

namespace Morphant.Build.Tasks;

internal static class GitSnapshotStorage
{
    internal const string OwnerFileName = ".morphant";

    public static IDisposable Acquire(string root, string owner, string code,
        CancellationToken cancellationToken, Action<string>? waiting = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PhysicalDirectory.EnsureNoLinks(root, root, "Morphant storage");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, OwnerFileName);
        var reportedWait = false;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (PhysicalDirectory.IsLink(path))
                throw new SnapshotException("MORPHANTMSB016", $"Morphant ownership file '{path}' is a link.");
            if (Directory.Exists(path))
                throw new SnapshotException("MORPHANTMSB015", $"Morphant ownership file '{path}' names a directory.");

            FileStream stream;
            try { stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException exception) when (IsSharingViolation(exception))
            {
                if (!reportedWait)
                {
                    waiting?.Invoke(root);
                    reportedWait = true;
                }
                cancellationToken.WaitHandle.WaitOne(50);
                continue;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (stream.Length == 0)
                {
                    var bytes = Encoding.UTF8.GetBytes(owner);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush();
                }
                else
                {
                    if (stream.Length > 65536)
                        throw Conflict();
                    using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: true);
                    if (!string.Equals(reader.ReadToEnd(), owner, PhysicalDirectory.Comparison))
                        throw Conflict();
                }
                return stream;
            }
            catch { stream.Dispose(); throw; }
        }

        SnapshotException Conflict() => new(code,
            $"Morphant directory '{root}' belongs to another project or compilation. Use a separate directory; " +
            $"its ownership is recorded in '{OwnerFileName}'.");
    }

    private static bool IsSharingViolation(IOException exception) =>
        (exception.HResult & 0xffff) is 11 or 32 or 33 or 35;
}
