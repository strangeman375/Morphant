using System.Security.Cryptography;
using System.Text;

namespace Morphant.Build.Tasks;

internal sealed class GitSnapshotContext
{
    private static readonly char[] AmbiguousPathCharacters =
        ['*', '?', ';', '[', ']'];

    private GitSnapshotContext(
        string snapshotRoot,
        string targetFramework,
        IReadOnlyCollection<string> expectedTargetFrameworks,
        string intermediateDirectory,
        string compilerGeneratedDirectory)
    {
        SnapshotRoot = snapshotRoot;
        ExpectedTargetFrameworks = expectedTargetFrameworks;
        IntermediateDirectory = intermediateDirectory;
        CompilerGeneratedDirectory = compilerGeneratedDirectory;
        SliceDirectory = Path.Combine(snapshotRoot, targetFramework);
    }

    public string SnapshotRoot { get; }

    public IReadOnlyCollection<string> ExpectedTargetFrameworks { get; }

    public string IntermediateDirectory { get; }

    public string CompilerGeneratedDirectory { get; }

    public string SliceDirectory { get; }

    public static GitSnapshotContext Create(
        string projectDirectory,
        string snapshotRoot,
        string targetFramework,
        string targetFrameworks,
        string baseIntermediateOutputPath,
        string intermediateOutputPath,
        string compilerGeneratedFilesOutputPath,
        string emitCompilerGeneratedFiles)
    {
        if (!bool.TryParse(emitCompilerGeneratedFiles, out var emit) || !emit)
        {
            throw new SnapshotException(
                "MORPHANTMSB002",
                "MorphantGitSnapshot requires " +
                "EmitCompilerGeneratedFiles=true. Remove the command-line " +
                "or global override that prevents Morphant from enabling it.");
        }

        var project = FullPath(
            projectDirectory,
            projectDirectory,
            "MSBuildProjectDirectory");
        var snapshot = FullPath(
            snapshotRoot,
            project,
            "MorphantGitSnapshotPath");

        if (!IsInside(snapshot, project))
        {
            throw new SnapshotException(
                "MORPHANTMSB005",
                "MorphantGitSnapshotPath must be a dedicated subdirectory " +
                "inside MSBuildProjectDirectory. The project root, an " +
                "ancestor, or an external/shared directory is not allowed.");
        }

        var snapshotComponents = RelativePath(project, snapshot).Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        if (snapshotComponents.Any(static component =>
                !PortablePath.IsSafeComponent(component)))
        {
            throw new SnapshotException(
                "MORPHANTMSB006",
                "MorphantGitSnapshotPath must use portable literal directory " +
                "names without wildcards, item separators, reserved device " +
                "names, or unevaluated MSBuild syntax.");
        }

        EnsureNoLinks(project, snapshot, "MorphantGitSnapshotPath");

        var baseIntermediate = FullPath(
            baseIntermediateOutputPath,
            project,
            "BaseIntermediateOutputPath");
        var intermediate = FullPath(
            intermediateOutputPath,
            project,
            "IntermediateOutputPath");

        if ((!IsInside(intermediate, baseIntermediate) &&
             !PathsEqual(intermediate, baseIntermediate)) ||
            PathsEqual(baseIntermediate, project) ||
            PathsEqual(intermediate, project) ||
            PathsOverlap(snapshot, baseIntermediate) ||
            PathsOverlap(snapshot, intermediate))
        {
            throw new SnapshotException(
                "MORPHANTMSB003",
                "IntermediateOutputPath must remain inside a dedicated " +
                "BaseIntermediateOutputPath, and neither path may equal the " +
                "project root or overlap MorphantGitSnapshotPath.");
        }

        EnsureNoLinks(
            baseIntermediate,
            intermediate,
            "IntermediateOutputPath");

        var expectedCompilerOutput = Path.Combine(
            intermediate,
            "Morphant.CompilerGenerated");
        var compilerOutput = FullPath(
            compilerGeneratedFilesOutputPath,
            project,
            "CompilerGeneratedFilesOutputPath");

        if (!PathsEqual(expectedCompilerOutput, compilerOutput))
        {
            throw new SnapshotException(
                "MORPHANTMSB004",
                "MorphantGitSnapshot requires the private compiler staging " +
                $"directory '{expectedCompilerOutput}', but the effective " +
                $"CompilerGeneratedFilesOutputPath is '{compilerOutput}'. " +
                "Remove the command-line or global override.");
        }

        EnsureNoLinks(
            intermediate,
            compilerOutput,
            "CompilerGeneratedFilesOutputPath");

        targetFramework = string.IsNullOrWhiteSpace(targetFramework)
            ? "_default"
            : targetFramework;
        EnsureSafeComponent(targetFramework, "TargetFramework");

        var frameworks = string.IsNullOrWhiteSpace(targetFrameworks)
            ? [targetFramework]
            : targetFrameworks
                .Split([';'], StringSplitOptions.RemoveEmptyEntries)
                .Select(static value => value.Trim())
                .Append(targetFramework)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        foreach (var framework in frameworks)
        {
            EnsureSafeComponent(framework, "TargetFrameworks");
        }

        return new GitSnapshotContext(
            snapshot,
            targetFramework,
            frameworks,
            intermediate,
            compilerOutput);
    }

    public IDisposable AcquireRootLock()
    {
        var lockRoot = Path.Combine(
            Path.GetTempPath(),
            "Morphant.GitSnapshot.Locks");
        Directory.CreateDirectory(lockRoot);

        using var sha = SHA256.Create();
        var identity = Path.DirectorySeparatorChar == '\\'
            ? SnapshotRoot.ToUpperInvariant()
            : SnapshotRoot;
        var key = string.Concat(
            sha.ComputeHash(Encoding.UTF8.GetBytes(identity))
                .Select(static value => value.ToString("x2")));
        var lockPath = Path.Combine(lockRoot, key + ".lock");
        var deadline = DateTime.UtcNow.AddMinutes(2);

        while (true)
        {
            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(25);
            }
            catch (IOException exception)
            {
                throw new SnapshotException(
                    "MORPHANTMSB019",
                    "Timed out waiting for another build to release the " +
                    $"Morphant snapshot root '{SnapshotRoot}': " +
                    exception.Message);
            }
        }
    }

    public bool IsExpectedTargetFramework(string value) =>
        ExpectedTargetFrameworks.Contains(
            value,
            StringComparer.OrdinalIgnoreCase);

    public void EnsureSafeCompilerOutput() => EnsureNoLinks(
        IntermediateDirectory,
        CompilerGeneratedDirectory,
        "CompilerGeneratedFilesOutputPath");

    public void EnsureSafeSnapshotPath(string path, string description) =>
        EnsureNoLinks(SnapshotRoot, path, description);

    private static string FullPath(
        string value,
        string baseDirectory,
        string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Any(char.IsControl) ||
            value.IndexOfAny(AmbiguousPathCharacters) >= 0 ||
            value.IndexOf("$(", StringComparison.Ordinal) >= 0 ||
            value.IndexOf("@(", StringComparison.Ordinal) >= 0 ||
            value.IndexOf("%(", StringComparison.Ordinal) >= 0)
        {
            throw new SnapshotException(
                "MORPHANTMSB006",
                $"{propertyName} must be one non-empty literal path without " +
                "wildcards, item separators, or unevaluated MSBuild syntax.");
        }

        var path = Path.IsPathRooted(value)
            ? value
            : Path.Combine(baseDirectory, value);
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        return fullPath.Length == root?.Length
            ? fullPath
            : fullPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
    }

    private static void EnsureSafeComponent(string value, string propertyName)
    {
        if (!PortablePath.IsSafeComponent(value))
        {
            throw new SnapshotException(
                "MORPHANTMSB007",
                $"{propertyName} contains a value that cannot be used as a " +
                "safe snapshot path component.");
        }
    }

    private static void EnsureNoLinks(
        string parent,
        string candidate,
        string description)
    {
        if (File.Exists(parent))
        {
            throw new SnapshotException(
                "MORPHANTMSB015",
                $"{description} parent '{parent}' names a file, not a directory.");
        }

        if (Directory.Exists(parent) &&
            (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
        {
            throw new SnapshotException(
                "MORPHANTMSB016",
                $"{description} parent '{parent}' is a symbolic link or " +
                "reparse point.");
        }

        if (File.Exists(candidate))
        {
            throw new SnapshotException(
                "MORPHANTMSB015",
                $"{description} '{candidate}' names a file, not a directory.");
        }

        if (!PathsEqual(parent, candidate) && !IsInside(candidate, parent))
        {
            throw new SnapshotException(
                "MORPHANTMSB003",
                $"{description} '{candidate}' must remain inside '{parent}'.");
        }

        var current = parent;

        foreach (var component in RelativePath(parent, candidate).Split(
                     [
                         Path.DirectorySeparatorChar,
                         Path.AltDirectorySeparatorChar
                     ],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, component);

            if (File.Exists(current))
            {
                throw new SnapshotException(
                    "MORPHANTMSB015",
                    $"{description} traverses file '{current}'.");
            }

            if (Directory.Exists(current) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new SnapshotException(
                    "MORPHANTMSB016",
                    $"{description} traverses symbolic link or reparse point " +
                    $"'{current}'.");
            }
        }
    }

    private static bool PathsOverlap(string left, string right) =>
        PathsEqual(left, right) || IsInside(left, right) || IsInside(right, left);

    private static bool PathsEqual(string left, string right) =>
        string.Equals(left, right, PathComparison);

    private static bool IsInside(string candidate, string parent) =>
        candidate.StartsWith(
            parent.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar,
            PathComparison);

    private static string RelativePath(string fromDirectory, string toPath)
    {
        var from = new Uri(
            fromDirectory.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar);
        return Uri.UnescapeDataString(from.MakeRelativeUri(new Uri(toPath)).ToString())
            .Replace('/', Path.DirectorySeparatorChar);
    }

    private static StringComparison PathComparison =>
        Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
}
