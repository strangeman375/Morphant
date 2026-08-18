namespace Morphant.Build.Tasks;

internal static class GitSnapshotLifecycle
{
    private const string GeneratedPattern = "Morphant.Generated.*.g.cs";
    private const string MapperPattern =
        "Morphant.Generated.TypeMapper.*.g.cs";

    public static void Prepare(GitSnapshotContext context)
    {
        context.EnsureSafeCompilerOutput();

        if (!Directory.Exists(context.CompilerGeneratedDirectory))
        {
            return;
        }

        foreach (var file in CompilerGeneratedFiles(
                     context.CompilerGeneratedDirectory,
                     GeneratedPattern))
        {
            File.Delete(file);
        }
    }

    public static void Publish(GitSnapshotContext context)
    {
        context.EnsureSafeCompilerOutput();
        var currentFiles = FileSet(
            Directory.Exists(context.CompilerGeneratedDirectory)
                ? CompilerGeneratedFiles(
                    context.CompilerGeneratedDirectory,
                    context.SnapshotDetail == GitSnapshotDetail.Full
                        ? GeneratedPattern
                        : MapperPattern)
                : []);

        using var snapshotLock = context.AcquireRootLock();
        context.EnsureSafeSnapshotPath(
            context.SliceDirectory,
            "Morphant snapshot slice");
        var obsoleteSlices = ObsoleteSlices(context);

        var existingFiles = FileSet(
            Directory.Exists(context.SliceDirectory)
                ? Directory.GetFiles(
                    context.SliceDirectory,
                    GeneratedPattern,
                    SearchOption.TopDirectoryOnly)
                : []);

        PreflightDestinations(context.SliceDirectory, currentFiles.Keys);

        if (currentFiles.Count > 0)
        {
            Directory.CreateDirectory(context.SliceDirectory);
        }

        foreach (var currentFile in currentFiles)
        {
            var destination = Path.Combine(
                context.SliceDirectory,
                currentFile.Key);

            if (!File.Exists(destination) ||
                !FilesEqual(currentFile.Value, destination))
            {
                File.Copy(currentFile.Value, destination, overwrite: true);
            }
        }

        foreach (var existingFile in existingFiles)
        {
            if (!currentFiles.ContainsKey(existingFile.Key))
            {
                File.Delete(existingFile.Value);
            }
        }

        CleanObsoleteSlices(obsoleteSlices);
        DeleteIfEmpty(context.SliceDirectory);
        DeleteIfEmpty(context.SnapshotRoot);
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
                    throw new SnapshotException(
                        "MORPHANTMSB016",
                        "CompilerGeneratedFilesOutputPath contains symbolic " +
                        $"link or reparse point directory '{child}'.");
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

    private static IReadOnlyCollection<string> ObsoleteSlices(
        GitSnapshotContext context)
    {
        if (!Directory.Exists(context.SnapshotRoot))
        {
            return [];
        }

        var result = new List<string>();

        foreach (var directory in Directory.GetDirectories(
                     context.SnapshotRoot,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            if (context.IsExpectedTargetFramework(Path.GetFileName(directory)))
            {
                continue;
            }

            context.EnsureSafeSnapshotPath(
                directory,
                "obsolete Morphant snapshot slice");

            result.Add(directory);
        }

        return result;
    }

    private static void CleanObsoleteSlices(
        IEnumerable<string> obsoleteSlices)
    {
        foreach (var directory in obsoleteSlices)
        {
            foreach (var file in Directory.GetFiles(
                         directory,
                         GeneratedPattern,
                         SearchOption.TopDirectoryOnly))
            {
                File.Delete(file);
            }

            DeleteIfEmpty(directory);
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
