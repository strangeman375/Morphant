using System.Security.Cryptography;
using System.Text;

namespace Morphant.Build.Tasks;

internal sealed class SnapshotPath
{
    private static readonly char[] AmbiguousPathCharacters =
        ['*', '?', ';', '[', ']'];

    private SnapshotPath(
        string projectFile,
        string projectDirectory,
        string snapshotRoot,
        string targetFramework,
        IReadOnlyCollection<string> expectedTargetFrameworks,
        string baseIntermediateDirectory,
        string intermediateDirectory,
        string compilerGeneratedDirectory)
    {
        ProjectFile = projectFile;
        ProjectDirectory = projectDirectory;
        SnapshotRoot = snapshotRoot;
        TargetFramework = targetFramework;
        ExpectedTargetFrameworks = expectedTargetFrameworks;
        BaseIntermediateDirectory = baseIntermediateDirectory;
        IntermediateDirectory = intermediateDirectory;
        CompilerGeneratedDirectory = compilerGeneratedDirectory;
        SliceRelativePath = targetFramework;
        SliceDirectory = Path.Combine(snapshotRoot, targetFramework);
        SnapshotManifest = Path.Combine(
            SliceDirectory,
            SnapshotManifestFormat.FileName);
        RootManifest = Path.Combine(
            snapshotRoot,
            SnapshotRootManifestFormat.FileName);
        ProjectIdentity = RelativePath(snapshotRoot, projectFile)
            .Replace(Path.DirectorySeparatorChar, '/');

        StateDirectory = Path.Combine(
            intermediateDirectory,
            "Morphant.GitSnapshot");
        EnsureNoReparsePoints(
            intermediateDirectory,
            StateDirectory,
            "Morphant Git snapshot state directory");
        TrustedManifest = Path.Combine(
            StateDirectory,
            "Morphant.Generated.trusted.manifest");
        OutputsProject = Path.Combine(
            StateDirectory,
            "Morphant.Generated.outputs.props");
        ForceCompileStamp = Path.Combine(
            StateDirectory,
            "Morphant.Generated.force");

        RootStateDirectory = Path.Combine(
            baseIntermediateDirectory,
            "Morphant.GitSnapshot.Root",
            StablePathKey(projectFile + "\n" + snapshotRoot));
        EnsureNoReparsePoints(
            baseIntermediateDirectory,
            RootStateDirectory,
            "Morphant Git snapshot root-state directory");
        TrustedRootManifest = Path.Combine(
            RootStateDirectory,
            "Morphant.Generated.trusted.root.manifest");
    }

    public string ProjectFile { get; }

    public string ProjectDirectory { get; }

    public string SnapshotRoot { get; }

    public string TargetFramework { get; }

    public IReadOnlyCollection<string> ExpectedTargetFrameworks { get; }

    public string BaseIntermediateDirectory { get; }

    public string IntermediateDirectory { get; }

    public string CompilerGeneratedDirectory { get; }

    public string SliceRelativePath { get; }

    public string SliceDirectory { get; }

    public string SnapshotManifest { get; }

    public string RootManifest { get; }

    public string ProjectIdentity { get; }

    public string StateDirectory { get; }

    public string TrustedManifest { get; }

    public string OutputsProject { get; }

    public string ForceCompileStamp { get; }

    public string RootStateDirectory { get; }

    public string TrustedRootManifest { get; }

    public static SnapshotPath Create(
        string projectFile,
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
                "EmitCompilerGeneratedFiles=true. A command-line or global " +
                "MSBuild property prevented Morphant from enabling it.");
        }

        var normalizedProjectDirectory = NormalizeDirectory(
            projectDirectory,
            projectDirectory,
            "MSBuildProjectDirectory");
        var normalizedProjectFile = NormalizeFile(
            projectFile,
            normalizedProjectDirectory,
            "MSBuildProjectFullPath");
        var normalizedSnapshotRoot = NormalizeDirectory(
            snapshotRoot,
            normalizedProjectDirectory,
            "MorphantGitSnapshotPath");

        EnsureDedicatedProjectDirectory(
            normalizedSnapshotRoot,
            normalizedProjectDirectory);
        EnsureNoReparsePoints(
            normalizedProjectDirectory,
            normalizedSnapshotRoot,
            "MorphantGitSnapshotPath");

        var normalizedBaseIntermediate = NormalizeDirectory(
            baseIntermediateOutputPath,
            normalizedProjectDirectory,
            "BaseIntermediateOutputPath");
        EnsureDirectoryIsNotReparsePoint(
            normalizedBaseIntermediate,
            "BaseIntermediateOutputPath");

        if (PathsEqual(
                normalizedBaseIntermediate,
                normalizedProjectDirectory) ||
            IsInside(normalizedSnapshotRoot, normalizedBaseIntermediate) ||
            IsInside(normalizedBaseIntermediate, normalizedSnapshotRoot))
        {
            throw new SnapshotException(
                "MORPHANTMSB003",
                "BaseIntermediateOutputPath must be a dedicated directory " +
                "that does not equal the project root or overlap " +
                "MorphantGitSnapshotPath.");
        }

        if (IsInside(
                normalizedBaseIntermediate,
                normalizedProjectDirectory))
        {
            EnsureNoReparsePoints(
                normalizedProjectDirectory,
                normalizedBaseIntermediate,
                "BaseIntermediateOutputPath");
        }

        var normalizedIntermediate = NormalizeDirectory(
            intermediateOutputPath,
            normalizedProjectDirectory,
            "IntermediateOutputPath");

        EnsureDirectoryIsNotReparsePoint(
            normalizedIntermediate,
            "IntermediateOutputPath");

        if (!PathsEqual(
                normalizedIntermediate,
                normalizedBaseIntermediate) &&
            !IsInside(normalizedIntermediate, normalizedBaseIntermediate))
        {
            throw new SnapshotException(
                "MORPHANTMSB003",
                "IntermediateOutputPath must be inside " +
                "BaseIntermediateOutputPath so Morphant can keep private, " +
                "project-owned trusted state under obj.");
        }

        if (PathsEqual(normalizedIntermediate, normalizedProjectDirectory) ||
            IsInside(normalizedSnapshotRoot, normalizedIntermediate) ||
            IsInside(normalizedIntermediate, normalizedSnapshotRoot))
        {
            throw new SnapshotException(
                "MORPHANTMSB003",
                "IntermediateOutputPath must be a dedicated directory that " +
                "does not overlap MorphantGitSnapshotPath or the project root.");
        }

        if (IsInside(normalizedIntermediate, normalizedProjectDirectory))
        {
            EnsureNoReparsePoints(
                normalizedProjectDirectory,
                normalizedIntermediate,
                "IntermediateOutputPath");
        }

        var expectedCompilerGeneratedDirectory = Path.Combine(
            normalizedIntermediate,
            "Morphant.CompilerGenerated");
        EnsureNoReparsePoints(
            normalizedIntermediate,
            expectedCompilerGeneratedDirectory,
            "CompilerGeneratedFilesOutputPath");
        var effectiveCompilerGeneratedDirectory = NormalizeDirectory(
            compilerGeneratedFilesOutputPath,
            normalizedProjectDirectory,
            "CompilerGeneratedFilesOutputPath");

        if (!PathsEqual(
                expectedCompilerGeneratedDirectory,
                effectiveCompilerGeneratedDirectory))
        {
            throw new SnapshotException(
                "MORPHANTMSB004",
                "MorphantGitSnapshot requires the private compiler staging " +
                $"directory '{expectedCompilerGeneratedDirectory}', but the " +
                "effective CompilerGeneratedFilesOutputPath is " +
                $"'{effectiveCompilerGeneratedDirectory}'. Remove the " +
                "command-line or global override.");
        }

        targetFramework = string.IsNullOrWhiteSpace(targetFramework)
            ? "_default"
            : targetFramework;
        ValidatePathComponent(targetFramework, "TargetFramework");

        var expectedTargetFrameworks = ParseTargetFrameworks(
            targetFramework,
            targetFrameworks);

        return new SnapshotPath(
            normalizedProjectFile,
            normalizedProjectDirectory,
            normalizedSnapshotRoot,
            targetFramework,
            expectedTargetFrameworks,
            normalizedBaseIntermediate,
            normalizedIntermediate,
            expectedCompilerGeneratedDirectory);
    }

    public IDisposable AcquireRootLock()
    {
        var lockRoot = Path.Combine(
            Path.GetTempPath(),
            "Morphant.GitSnapshot.Locks");
        Directory.CreateDirectory(lockRoot);

        using var sha = SHA256.Create();
        var lockIdentity = Path.DirectorySeparatorChar == '\\'
            ? SnapshotRoot.ToUpperInvariant()
            : SnapshotRoot;
        var key = string.Concat(
            sha.ComputeHash(Encoding.UTF8.GetBytes(lockIdentity))
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
                    $"Timed out waiting for another build to release the " +
                    $"Morphant snapshot root '{SnapshotRoot}': " +
                    exception.Message);
            }
        }
    }

    public bool IsExpectedTargetFramework(string targetFramework) =>
        ExpectedTargetFrameworks.Contains(
            targetFramework,
            StringComparer.OrdinalIgnoreCase);

    public void EnsureSafeSnapshotDescendant(
        string candidate,
        string description) =>
        EnsureNoReparsePoints(SnapshotRoot, candidate, description);

    public void EnsureSafeStateDirectory() =>
        EnsureNoReparsePoints(
            IntermediateDirectory,
            StateDirectory,
            "Morphant Git snapshot state directory");

    public void EnsureSafeRootStateDirectory() =>
        EnsureNoReparsePoints(
            BaseIntermediateDirectory,
            RootStateDirectory,
            "Morphant Git snapshot root-state directory");

    private static IReadOnlyCollection<string> ParseTargetFrameworks(
        string current,
        string targetFrameworks)
    {
        var values = string.IsNullOrWhiteSpace(targetFrameworks)
            ? [current]
            : targetFrameworks.Split([';'], StringSplitOptions.RemoveEmptyEntries)
                .Select(static value => value.Trim())
                .ToArray();

        foreach (var value in values)
        {
            ValidatePathComponent(value, "TargetFrameworks");
        }

        return values
            .Append(current)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static void EnsureDedicatedProjectDirectory(
        string snapshotRoot,
        string projectDirectory)
    {
        if (PathsEqual(snapshotRoot, projectDirectory) ||
            !IsInside(snapshotRoot, projectDirectory))
        {
            throw new SnapshotException(
                "MORPHANTMSB005",
                "MorphantGitSnapshotPath must be a dedicated subdirectory " +
                "inside MSBuildProjectDirectory. The project root, an " +
                "ancestor, or an external/shared directory is not allowed.");
        }

        var relative = RelativePath(projectDirectory, snapshotRoot);

        var components = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        if (components.Length == 0)
        {
            throw new SnapshotException(
                "MORPHANTMSB005",
                "MorphantGitSnapshotPath must name a dedicated subdirectory.");
        }

        if (components.Any(static component =>
                !PortablePath.IsSafeComponent(component)))
        {
            throw new SnapshotException(
                "MORPHANTMSB006",
                "MorphantGitSnapshotPath must use portable literal directory " +
                "names without wildcards, item separators, reserved device " +
                "names, or unevaluated MSBuild syntax.");
        }
    }

    private static void EnsureNoReparsePoints(
        string trustedParent,
        string candidate,
        string propertyName)
    {
        if (File.Exists(candidate))
        {
            throw new SnapshotException(
                "MORPHANTMSB015",
                $"{propertyName} '{candidate}' names a file, not a dedicated " +
                "directory.");
        }

        if (!IsInside(candidate, trustedParent) &&
            !PathsEqual(candidate, trustedParent))
        {
            return;
        }

        var relative = RelativePath(trustedParent, candidate);
        var current = trustedParent;

        foreach (var component in relative.Split(
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
                    $"{propertyName} traverses file '{current}' instead of " +
                    "a dedicated directory.");
            }

            if (Directory.Exists(current) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new SnapshotException(
                    "MORPHANTMSB016",
                    $"{propertyName} traverses symbolic link or reparse " +
                    $"point '{current}'. Morphant refuses to use an aliased " +
                    "directory for cleanup or publication.");
            }
        }
    }

    private static void EnsureDirectoryIsNotReparsePoint(
        string directory,
        string propertyName)
    {
        if (File.Exists(directory))
        {
            throw new SnapshotException(
                "MORPHANTMSB015",
                $"{propertyName} '{directory}' names a file, not a dedicated " +
                "directory.");
        }

        if (Directory.Exists(directory) &&
            (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
        {
            throw new SnapshotException(
                "MORPHANTMSB016",
                $"{propertyName} '{directory}' is a symbolic link or " +
                "reparse point. Morphant refuses to use an aliased " +
                "directory for cleanup or publication.");
        }
    }

    private static string NormalizeDirectory(
        string value,
        string baseDirectory,
        string propertyName)
    {
        ValidateLiteralPath(value, propertyName);
        var path = Path.IsPathRooted(value)
            ? value
            : Path.Combine(baseDirectory, value);

        return TrimDirectorySeparator(Path.GetFullPath(path));
    }

    private static string NormalizeFile(
        string value,
        string baseDirectory,
        string propertyName)
    {
        ValidateLiteralPath(value, propertyName);
        var path = Path.IsPathRooted(value)
            ? value
            : Path.Combine(baseDirectory, value);

        return Path.GetFullPath(path);
    }

    private static void ValidateLiteralPath(string value, string propertyName)
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
    }

    private static void ValidatePathComponent(string value, string propertyName)
    {
        if (!PortablePath.IsSafeComponent(value))
        {
            throw new SnapshotException(
                "MORPHANTMSB007",
                $"{propertyName} contains a value that cannot be used as a " +
                "safe snapshot path component.");
        }
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(left, right, PathComparison);

    private static bool IsInside(string candidate, string parent)
    {
        var parentPrefix = TrimDirectorySeparator(parent) +
                           Path.DirectorySeparatorChar;
        return candidate.StartsWith(parentPrefix, PathComparison);
    }

    private static string TrimDirectorySeparator(string path)
    {
        var root = Path.GetPathRoot(path);

        while (path.Length > root?.Length &&
               (path[path.Length - 1] == Path.DirectorySeparatorChar ||
                path[path.Length - 1] == Path.AltDirectorySeparatorChar))
        {
            path = path.Substring(0, path.Length - 1);
        }

        return path;
    }

    private static string RelativePath(string fromDirectory, string toPath)
    {
        var from = new Uri(
            TrimDirectorySeparator(fromDirectory) +
            Path.DirectorySeparatorChar);
        var to = new Uri(toPath);

        return Uri.UnescapeDataString(from.MakeRelativeUri(to).ToString())
            .Replace('/', Path.DirectorySeparatorChar);
    }

    private static string StablePathKey(string value)
    {
        if (Path.DirectorySeparatorChar == '\\')
        {
            value = value.ToUpperInvariant();
        }

        using var sha = SHA256.Create();
        return string.Concat(
            sha.ComputeHash(Encoding.UTF8.GetBytes(value))
                .Select(static item => item.ToString("x2")));
    }

    private static StringComparison PathComparison =>
        Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
}
