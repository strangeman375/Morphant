namespace Morphant.Build.Tasks;

internal static class GitSnapshotLifecycle
{
    private const string GeneratedPattern = "Morphant.Generated.*.g.cs";
    private const string MapperPattern =
        "Morphant.Generated.TypeMapper.*.g.cs";

    public static void Prepare(GitSnapshotContext context, CancellationToken cancellationToken = default)
    {
        context.CheckSnapshotOwner(cancellationToken);
        context.EnsureSafeCompilerOutput();
        var files = Directory.Exists(context.CompilerGeneratedDirectory)
            ? FileSet(CompilerGeneratedFiles(context.CompilerGeneratedDirectory, GeneratedPattern))
            : new Dictionary<string, string>();
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(context.CompilerGeneratedDirectory);
        foreach (var file in files.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(file);
        }
    }

    public static (int Updated, int Removed, int Unchanged) Publish(
        GitSnapshotContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        context.EnsureSafeCompilerOutput();
        context.EnsureSafeSnapshotPath(context.SliceDirectory, "Morphant snapshot slice");
        var currentFiles = FileSet(
            Directory.Exists(context.CompilerGeneratedDirectory)
                ? CompilerGeneratedFiles(
                    context.CompilerGeneratedDirectory,
                    context.SnapshotDetail == GitSnapshotDetail.Full
                        ? GeneratedPattern
                        : MapperPattern)
                : []);

        PreflightDestinations(context.SliceDirectory, currentFiles.Keys);
        context.CheckSnapshotOwner(cancellationToken, create: true);
        context.EnsureSafeSnapshotPath(
            context.SliceDirectory,
            "Morphant snapshot slice");

        var existingFiles = FileSet(
            Directory.Exists(context.SliceDirectory)
                ? Directory.GetFiles(
                    context.SliceDirectory,
                    GeneratedPattern,
                    SearchOption.TopDirectoryOnly)
                : []);

        if (currentFiles.Count > 0)
        {
            Directory.CreateDirectory(context.SliceDirectory);
        }

        var updated = 0;
        var removed = 0;
        foreach (var currentFile in currentFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destination = Path.Combine(
                context.SliceDirectory,
                currentFile.Key);

            var changed = false;
            if (existingFiles.TryGetValue(currentFile.Key, out var existingPath) &&
                !string.Equals(Path.GetFileName(existingPath), currentFile.Key, StringComparison.Ordinal))
            {
                // Preserve the compiler's exact casing on both case-sensitive
                // and case-insensitive filesystems without retaining an alias.
                File.Move(existingPath, destination);
                changed = true;
            }

            if (!File.Exists(destination) ||
                !FilesEqual(currentFile.Value, destination))
            {
                File.Copy(currentFile.Value, destination, overwrite: true);
                changed = true;
            }
            if (changed) updated++;
        }

        foreach (var existingFile in existingFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!currentFiles.ContainsKey(existingFile.Key))
            {
                File.Delete(existingFile.Value);
                removed++;
            }
        }

        DeleteIfEmpty(context.SliceDirectory);
        return (updated, removed, currentFiles.Count - updated);
    }

    private static IReadOnlyCollection<string> CompilerGeneratedFiles(
        string root,
        string pattern)
    {
        var files = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();

            foreach (var child in Directory.GetDirectories(
                         directory,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                if ((File.GetAttributes(child) &
                     FileAttributes.ReparsePoint) != 0)
                {
                    var relative = PhysicalDirectory.Relative(root, child);
                    if (relative.Split('/').Contains("Morphant.Generator", StringComparer.OrdinalIgnoreCase))
                        throw new SnapshotException("MORPHANTMSB016",
                            $"CompilerGeneratedFilesOutputPath contains a link at managed path '{child}'.");
                    continue;
                }

                pending.Push(child);
            }

            files.AddRange(Directory.GetFiles(
                directory,
                pattern,
                SearchOption.TopDirectoryOnly));
        }

        return files;
    }

    private static IReadOnlyDictionary<string, string> FileSet(
        IEnumerable<string> paths)
    {
        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            var fileName = Path.GetFileName(path);

            if (!PortablePath.IsSafeComponent(fileName) ||
                result.ContainsKey(fileName))
            {
                throw new SnapshotException(
                    "MORPHANTMSB008",
                    "Morphant generated file names must be portable and " +
                    $"unique ignoring case. Invalid name: '{fileName}'.");
            }

            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new SnapshotException(
                    "MORPHANTMSB016",
                    $"Morphant generated file '{path}' is a symbolic link or " +
                    "reparse point.");
            }

            result.Add(fileName, path);
        }

        return result;
    }

    private static void PreflightDestinations(
        string sliceDirectory,
        IEnumerable<string> currentNames)
    {
        if (File.Exists(sliceDirectory))
        {
            throw new SnapshotException(
                "MORPHANTMSB015",
                $"Morphant snapshot slice '{sliceDirectory}' names a file, " +
                "not a directory.");
        }

        if (!Directory.Exists(sliceDirectory))
        {
            return;
        }

        foreach (var fileName in currentNames)
        {
            var path = Path.Combine(sliceDirectory, fileName);

            if (Directory.Exists(path))
            {
                throw new SnapshotException(
                    "MORPHANTMSB015",
                    $"Reserved Morphant snapshot path '{path}' names a " +
                "directory, not a generated file.");
            }
        }

        var reservedDirectory = Directory.GetDirectories(
                sliceDirectory,
                GeneratedPattern,
                SearchOption.TopDirectoryOnly)
            .FirstOrDefault();

        if (reservedDirectory is not null)
        {
            throw new SnapshotException(
                "MORPHANTMSB015",
                $"Reserved Morphant snapshot path '{reservedDirectory}' " +
                "names a directory, not a generated file.");
        }
    }

    private static bool FilesEqual(string left, string right)
    {
        if (new FileInfo(left).Length != new FileInfo(right).Length)
        {
            return false;
        }

        using var leftStream = File.OpenRead(left);
        using var rightStream = File.OpenRead(right);
        var leftBuffer = new byte[81920];
        var rightBuffer = new byte[leftBuffer.Length];

        while (true)
        {
            var count = leftStream.Read(leftBuffer, 0, leftBuffer.Length);

            if (count != rightStream.Read(rightBuffer, 0, rightBuffer.Length))
            {
                return false;
            }

            if (count == 0)
            {
                return true;
            }

            for (var index = 0; index < count; index++)
            {
                if (leftBuffer[index] != rightBuffer[index])
                {
                    return false;
                }
            }
        }
    }

    private static void DeleteIfEmpty(string path)
    {
        if (Directory.Exists(path) &&
            !Directory.EnumerateFileSystemEntries(path).Any())
        {
            Directory.Delete(path);
        }
    }
}
