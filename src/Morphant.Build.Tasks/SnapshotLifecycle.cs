using System.Text;

namespace Morphant.Build.Tasks;

internal sealed record SnapshotPreparation(
    bool ForcedCompilation,
    IReadOnlyList<string> SnapshotFiles);

internal interface ISnapshotPublicationObserver
{
    void Reached(string checkpoint);
}

internal sealed class NullSnapshotPublicationObserver :
    ISnapshotPublicationObserver
{
    public static NullSnapshotPublicationObserver Instance { get; } = new();

    public void Reached(string checkpoint)
    {
    }
}

internal static class SnapshotLifecycle
{
    public static SnapshotPreparation Prepare(
        SnapshotPath path,
        bool clean)
    {
        path.EnsureSafeStateDirectory();
        path.EnsureSafeRootStateDirectory();
        Directory.CreateDirectory(path.StateDirectory);
        Directory.CreateDirectory(path.RootStateDirectory);
        EnsureForceCompileStamp(path.ForceCompileStamp);
        CleanCompilerStaging(path.CompilerGeneratedDirectory);

        using var snapshotLock = path.AcquireRootLock();
        path.EnsureSafeSnapshotDescendant(
            path.SliceDirectory,
            "Morphant snapshot slice");
        var validation = ValidateCurrentSnapshot(path);

        if (!validation.IsValid)
        {
            File.SetLastWriteTimeUtc(
                path.ForceCompileStamp,
                DateTime.UtcNow);
            return new SnapshotPreparation(true, []);
        }

        if (clean)
        {
            CleanUnlistedGeneratedFiles(path, validation.Manifest!);
        }

        return new SnapshotPreparation(
            false,
            validation.Manifest!.Files
                .Select(file => Path.Combine(path.SliceDirectory, file.Name))
                .Append(path.SnapshotManifest)
                .ToArray());
    }

    public static void Publish(
        SnapshotPath path,
        bool clean,
        ISnapshotPublicationObserver? observer = null)
    {
        observer ??= NullSnapshotPublicationObserver.Instance;
        path.EnsureSafeStateDirectory();
        path.EnsureSafeRootStateDirectory();
        Directory.CreateDirectory(path.RootStateDirectory);
        var generatedFiles = CollectCompilerGeneratedFiles(
            path.CompilerGeneratedDirectory);
        var manifest = new SnapshotManifest(
            path.ProjectIdentity,
            path.TargetFramework,
            generatedFiles);
        var manifestBytes = manifest.Serialize();
        var outputsBytes = CreateOutputsProject(path, manifest);

        using var snapshotLock = path.AcquireRootLock();
        Directory.CreateDirectory(path.SnapshotRoot);
        path.EnsureSafeSnapshotDescendant(
            path.SliceDirectory,
            "Morphant snapshot slice");

        var rootManifest = ReadRootManifestForPublication(path);
        var newRootManifest = new SnapshotRootManifest(
            path.ProjectIdentity,
            rootManifest.Entries
                .Where(entry => !string.Equals(
                    entry.SliceRelativePath,
                    path.SliceRelativePath,
                    StringComparison.OrdinalIgnoreCase))
                .Append(new SnapshotRootEntry(
                    path.SliceRelativePath,
                    SnapshotManifestFormat.Hash(manifestBytes)))
                .OrderBy(
                    static entry => entry.SliceRelativePath,
                    StringComparer.Ordinal)
                .ToArray());
        var rootBytes = newRootManifest.Serialize();

        if (SnapshotMatches(path, manifest, manifestBytes, clean) &&
            FileContentsEqual(path.RootManifest, rootBytes))
        {
            RepairPrivateStateTransactionally(
                path,
                manifestBytes,
                rootBytes,
                outputsBytes);
            return;
        }

        PublishTransactionally(
            path,
            manifest,
            manifestBytes,
            rootBytes,
            rootBytes,
            outputsBytes,
            clean,
            observer);
    }

    public static void CleanObsoleteSlices(SnapshotPath path)
    {
        using var snapshotLock = path.AcquireRootLock();

        if (!File.Exists(path.RootManifest))
        {
            return;
        }

        EnsureFileIsNotReparsePoint(
            path.RootManifest,
            "Morphant snapshot-root manifest");
        var rootBytes = File.ReadAllBytes(path.RootManifest);

        path.EnsureSafeRootStateDirectory();

        if (!File.Exists(path.TrustedRootManifest) ||
            !FileContentsEqual(path.TrustedRootManifest, rootBytes))
        {
            throw new SnapshotException(
                "MORPHANTMSB018",
                "The Morphant snapshot-root manifest does not match trusted " +
                "state under BaseIntermediateOutputPath. Morphant will not " +
                "delete obsolete slices; run a successful build to " +
                "re-establish ownership metadata.");
        }

        var root = SnapshotRootManifestFormat.Parse(
            rootBytes,
            "Morphant snapshot-root manifest");
        EnsureProjectOwnership(path, root);

        var obsolete = root.Entries
            .Where(entry =>
                !path.IsExpectedTargetFramework(entry.SliceRelativePath))
            .ToArray();

        if (obsolete.Length == 0)
        {
            return;
        }

        foreach (var entry in obsolete)
        {
            ValidateOwnedSlice(path, entry);
        }

        var updated = new SnapshotRootManifest(
            root.ProjectIdentity,
            root.Entries.Except(obsolete).ToArray());
        var updatedBytes = updated.Serialize();
        var token = Guid.NewGuid().ToString("N");
        var rootBackup = path.RootManifest + ".backup." + token;
        var rootTemporary = path.RootManifest + ".temporary." + token;
        var trustedRootBackup =
            path.TrustedRootManifest + ".backup." + token;
        var trustedRootTemporary =
            path.TrustedRootManifest + ".temporary." + token;
        var slices = obsolete.Select(entry => CreateObsoleteSliceTransaction(
                path,
                entry,
                token))
            .ToArray();
        var rootMoved = false;
        var trustedRootMoved = false;
        var committed = false;

        try
        {
            WriteAllBytes(rootTemporary, updatedBytes);
            WriteAllBytes(trustedRootTemporary, updatedBytes);

            foreach (var slice in slices)
            {
                StageUnrelatedSlice(slice);
                Directory.Move(slice.Source, slice.Backup);
                slice.BackupCreated = true;

                if (slice.HasReplacement)
                {
                    Directory.Move(slice.Replacement, slice.Source);
                    slice.ReplacementInstalled = true;
                }
            }

            ReplaceFile(path.RootManifest, rootTemporary, rootBackup);
            rootMoved = true;
            ReplaceFile(
                path.TrustedRootManifest,
                trustedRootTemporary,
                trustedRootBackup);
            trustedRootMoved = true;
            committed = true;
        }
        catch
        {
            if (trustedRootMoved)
            {
                RestoreFile(
                    path.TrustedRootManifest,
                    trustedRootTemporary,
                    trustedRootBackup);
            }

            if (rootMoved)
            {
                RestoreFile(path.RootManifest, rootTemporary, rootBackup);
            }

            foreach (var slice in slices.AsEnumerable().Reverse())
            {
                if (slice.ReplacementInstalled)
                {
                    DeleteDirectoryIfExists(slice.Source);
                }

                if (slice.BackupCreated &&
                    !Directory.Exists(slice.Source) &&
                    Directory.Exists(slice.Backup))
                {
                    Directory.Move(slice.Backup, slice.Source);
                }
            }

            throw;
        }
        finally
        {
            TryDeleteFile(rootTemporary);
            TryDeleteFile(trustedRootTemporary);

            foreach (var slice in slices)
            {
                TryDeleteDirectory(slice.Replacement);

                if (committed)
                {
                    TryDeleteDirectory(slice.Backup);
                }
            }

            if (committed)
            {
                TryDeleteFile(rootBackup);
                TryDeleteFile(trustedRootBackup);
            }
        }
    }

    private static SnapshotValidation ValidateCurrentSnapshot(SnapshotPath path)
    {
        if (!File.Exists(path.RootManifest) ||
            !File.Exists(path.SnapshotManifest) ||
            !File.Exists(path.TrustedManifest) ||
            !File.Exists(path.TrustedRootManifest))
        {
            return SnapshotValidation.Invalid;
        }

        SnapshotRootManifest root;

        try
        {
            EnsureFileIsNotReparsePoint(
                path.RootManifest,
                "Morphant snapshot-root manifest");
            EnsureFileIsNotReparsePoint(
                path.TrustedRootManifest,
                "trusted Morphant snapshot-root manifest");
            var rootBytes = File.ReadAllBytes(path.RootManifest);

            if (!FileContentsEqual(path.TrustedRootManifest, rootBytes))
            {
                return SnapshotValidation.Invalid;
            }

            root = SnapshotRootManifestFormat.Parse(
                rootBytes,
                "Morphant snapshot-root manifest");
        }
        catch (SnapshotException)
        {
            throw;
        }

        EnsureProjectOwnership(path, root);

        try
        {
            EnsureFileIsNotReparsePoint(
                path.SnapshotManifest,
                "Morphant Git snapshot manifest");
            EnsureFileIsNotReparsePoint(
                path.TrustedManifest,
                "trusted Morphant Git snapshot manifest");
            var snapshotBytes = File.ReadAllBytes(path.SnapshotManifest);
            var trustedBytes = File.ReadAllBytes(path.TrustedManifest);

            if (!snapshotBytes.SequenceEqual(trustedBytes))
            {
                return SnapshotValidation.Invalid;
            }

            var manifest = SnapshotManifestFormat.Parse(
                snapshotBytes,
                "Morphant Git snapshot manifest");

            if (!ManifestIdentityMatches(path, manifest))
            {
                return SnapshotValidation.Invalid;
            }

            var entry = root.Entries.SingleOrDefault(candidate =>
                string.Equals(
                    candidate.SliceRelativePath,
                    path.SliceRelativePath,
                    StringComparison.OrdinalIgnoreCase));

            if (entry is null || entry.ManifestHash !=
                SnapshotManifestFormat.Hash(snapshotBytes))
            {
                return SnapshotValidation.Invalid;
            }

            foreach (var file in manifest.Files)
            {
                var filePath = Path.Combine(path.SliceDirectory, file.Name);

                if (!File.Exists(filePath) ||
                    IsReparsePoint(filePath) ||
                    SnapshotManifestFormat.Hash(
                        SnapshotManifestFormat.CanonicalSourceBytes(filePath)) !=
                    file.Hash)
                {
                    return SnapshotValidation.Invalid;
                }
            }

            return new SnapshotValidation(true, manifest);
        }
        catch (SnapshotException)
        {
            return SnapshotValidation.Invalid;
        }
        catch (IOException)
        {
            return SnapshotValidation.Invalid;
        }
        catch (UnauthorizedAccessException)
        {
            return SnapshotValidation.Invalid;
        }
        catch (DecoderFallbackException)
        {
            return SnapshotValidation.Invalid;
        }
    }

    private static void CleanUnlistedGeneratedFiles(
        SnapshotPath path,
        SnapshotManifest manifest)
    {
        if (!Directory.Exists(path.SliceDirectory))
        {
            return;
        }

        var expected = new HashSet<string>(
            manifest.Files.Select(static file => file.Name),
            StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(
                     path.SliceDirectory,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file);

            if (SnapshotManifestFormat.IsGeneratedFileName(name) &&
                !expected.Contains(name))
            {
                File.Delete(file);
            }
        }
    }

    private static void CleanCompilerStaging(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var file in EnumerateFilesWithoutLinks(directory))
        {
            if (SnapshotManifestFormat.IsGeneratedFileName(
                    Path.GetFileName(file)))
            {
                File.Delete(file);
            }
        }
    }

    private static IReadOnlyList<SnapshotFile> CollectCompilerGeneratedFiles(
        string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var result = new List<SnapshotFile>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in EnumerateFilesWithoutLinks(directory))
        {
            var name = Path.GetFileName(path);

            if (!SnapshotManifestFormat.IsGeneratedFileName(name))
            {
                continue;
            }

            if (!names.Add(name))
            {
                throw new SnapshotException(
                    "MORPHANTMSB010",
                    $"More than one generated file has the name '{name}'. " +
                    "Morphant cannot flatten ambiguous compiler output into " +
                    "a Git snapshot.");
            }

            var contents = SnapshotManifestFormat.CanonicalSourceBytes(path);
            result.Add(new SnapshotFile(
                name,
                SnapshotManifestFormat.Hash(contents),
                contents));
        }

        return result
            .OrderBy(static file => file.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static SnapshotRootManifest ReadRootManifestForPublication(
        SnapshotPath path)
    {
        if (!File.Exists(path.RootManifest))
        {
            return new SnapshotRootManifest(path.ProjectIdentity, []);
        }

        EnsureFileIsNotReparsePoint(
            path.RootManifest,
            "Morphant snapshot-root manifest");
        var rootBytes = File.ReadAllBytes(path.RootManifest);
        var result = SnapshotRootManifestFormat.Parse(
            rootBytes,
            "Morphant snapshot-root manifest");
        EnsureProjectOwnership(path, result);

        if (!FileContentsEqual(path.TrustedRootManifest, rootBytes))
        {
            foreach (var entry in result.Entries.Where(entry =>
                         !string.Equals(
                             entry.SliceRelativePath,
                             path.SliceRelativePath,
                             StringComparison.OrdinalIgnoreCase)))
            {
                ValidateOwnedSlice(path, entry);
            }
        }

        return result;
    }

    private static void EnsureProjectOwnership(
        SnapshotPath path,
        SnapshotRootManifest root)
    {
        if (root.ProjectIdentity != path.ProjectIdentity)
        {
            throw new SnapshotException(
                "MORPHANTMSB011",
                $"MorphantGitSnapshotPath '{path.SnapshotRoot}' is owned by " +
                $"project '{root.ProjectIdentity}', not " +
                $"'{path.ProjectIdentity}'. Each project must use a separate " +
                "snapshot root.");
        }
    }

    private static bool SnapshotMatches(
        SnapshotPath path,
        SnapshotManifest expected,
        byte[] expectedManifestBytes,
        bool clean)
    {
        if (!FileContentsEqual(path.SnapshotManifest, expectedManifestBytes))
        {
            return false;
        }

        foreach (var file in expected.Files)
        {
            var target = Path.Combine(path.SliceDirectory, file.Name);

            if (!File.Exists(target))
            {
                return false;
            }

            try
            {
                if (!SnapshotManifestFormat.CanonicalSourceBytes(target)
                        .SequenceEqual(file.Contents))
                {
                    return false;
                }
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }

        return !clean || Directory.Exists(path.SliceDirectory) &&
               Directory.EnumerateFiles(
                       path.SliceDirectory,
                       "*",
                       SearchOption.TopDirectoryOnly)
                   .Select(Path.GetFileName)
                   .Where(static name => name is not null &&
                       SnapshotManifestFormat.IsGeneratedFileName(name))
                   .OrderBy(static name => name, StringComparer.Ordinal)
                   .SequenceEqual(expected.Files.Select(static file => file.Name));
    }

    private static void PublishTransactionally(
        SnapshotPath path,
        SnapshotManifest manifest,
        byte[] manifestBytes,
        byte[] rootBytes,
        byte[] trustedRootBytes,
        byte[] outputsBytes,
        bool clean,
        ISnapshotPublicationObserver observer)
    {
        var token = Guid.NewGuid().ToString("N");
        var sliceTemporary = path.SliceDirectory + ".temporary." + token;
        var sliceBackup = path.SliceDirectory + ".backup." + token;
        var rootTemporary = path.RootManifest + ".temporary." + token;
        var rootBackup = path.RootManifest + ".backup." + token;
        var trustedTemporary = path.TrustedManifest + ".temporary." + token;
        var trustedBackup = path.TrustedManifest + ".backup." + token;
        var trustedRootTemporary =
            path.TrustedRootManifest + ".temporary." + token;
        var trustedRootBackup =
            path.TrustedRootManifest + ".backup." + token;
        var outputsTemporary = path.OutputsProject + ".temporary." + token;
        var outputsBackup = path.OutputsProject + ".backup." + token;
        var sliceMoved = false;
        var rootMoved = false;
        var trustedMoved = false;
        var trustedRootMoved = false;
        var outputsMoved = false;
        var sliceBackupCreated = false;
        var committed = false;

        try
        {
            StageSlice(
                path,
                manifest,
                manifestBytes,
                sliceTemporary,
                clean);
            WriteAllBytes(rootTemporary, rootBytes);
            WriteAllBytes(trustedTemporary, manifestBytes);
            WriteAllBytes(trustedRootTemporary, trustedRootBytes);
            WriteAllBytes(outputsTemporary, outputsBytes);
            observer.Reached("staged");

            Directory.CreateDirectory(Path.GetDirectoryName(
                path.SliceDirectory)!);

            if (Directory.Exists(path.SliceDirectory))
            {
                Directory.Move(path.SliceDirectory, sliceBackup);
                sliceBackupCreated = true;
                observer.Reached("slice-backed-up");
            }

            try
            {
                Directory.Move(sliceTemporary, path.SliceDirectory);
            }
            catch
            {
                if (sliceBackupCreated &&
                    !Directory.Exists(path.SliceDirectory) &&
                    Directory.Exists(sliceBackup))
                {
                    Directory.Move(sliceBackup, path.SliceDirectory);
                    sliceBackupCreated = false;
                }

                throw;
            }

            sliceMoved = true;
            observer.Reached("slice-replaced");

            ReplaceFile(path.RootManifest, rootTemporary, rootBackup);
            rootMoved = true;
            observer.Reached("root-manifest-replaced");

            ReplaceFile(
                path.TrustedRootManifest,
                trustedRootTemporary,
                trustedRootBackup);
            trustedRootMoved = true;
            observer.Reached("trusted-root-state-replaced");

            ReplaceFile(path.TrustedManifest, trustedTemporary, trustedBackup);
            trustedMoved = true;
            observer.Reached("trusted-state-replaced");

            ReplaceFile(path.OutputsProject, outputsTemporary, outputsBackup);
            outputsMoved = true;
            observer.Reached("outputs-project-replaced");
            committed = true;
        }
        catch
        {
            if (outputsMoved)
            {
                RestoreFile(path.OutputsProject, outputsTemporary, outputsBackup);
            }

            if (trustedMoved)
            {
                RestoreFile(path.TrustedManifest, trustedTemporary, trustedBackup);
            }

            if (trustedRootMoved)
            {
                RestoreFile(
                    path.TrustedRootManifest,
                    trustedRootTemporary,
                    trustedRootBackup);
            }

            if (rootMoved)
            {
                RestoreFile(path.RootManifest, rootTemporary, rootBackup);
            }

            if (sliceMoved)
            {
                DeleteDirectoryIfExists(path.SliceDirectory);
            }

            if (sliceBackupCreated &&
                !Directory.Exists(path.SliceDirectory) &&
                Directory.Exists(sliceBackup))
            {
                Directory.Move(sliceBackup, path.SliceDirectory);
            }

            throw;
        }
        finally
        {
            TryDeleteDirectory(sliceTemporary);
            TryDeleteFile(rootTemporary);
            TryDeleteFile(trustedTemporary);
            TryDeleteFile(trustedRootTemporary);
            TryDeleteFile(outputsTemporary);

            if (committed)
            {
                TryDeleteDirectory(sliceBackup);
                TryDeleteFile(rootBackup);
                TryDeleteFile(trustedBackup);
                TryDeleteFile(trustedRootBackup);
                TryDeleteFile(outputsBackup);
            }
        }
    }

    private static void StageSlice(
        SnapshotPath path,
        SnapshotManifest manifest,
        byte[] manifestBytes,
        string temporaryDirectory,
        bool clean)
    {
        Directory.CreateDirectory(temporaryDirectory);

        if (Directory.Exists(path.SliceDirectory))
        {
            CopyUnrelatedEntries(
                path.SliceDirectory,
                temporaryDirectory,
                clean);
        }

        foreach (var file in manifest.Files)
        {
            WriteAllBytes(
                Path.Combine(temporaryDirectory, file.Name),
                file.Contents);
        }

        WriteAllBytes(
            Path.Combine(temporaryDirectory, SnapshotManifestFormat.FileName),
            manifestBytes);
    }

    private static void CopyUnrelatedEntries(
        string source,
        string destination,
        bool clean)
    {
        foreach (var file in Directory.EnumerateFiles(
                     source,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file);

            if (name == SnapshotManifestFormat.FileName ||
                clean && SnapshotManifestFormat.IsGeneratedFileName(name))
            {
                continue;
            }

            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
            {
                throw new SnapshotException(
                    "MORPHANTMSB012",
                    $"Snapshot file '{file}' is a symbolic link or reparse " +
                    "point. Morphant refuses to follow it during a " +
                    "transactional publication.");
            }

            File.Copy(file, Path.Combine(destination, name));
        }

        foreach (var directory in Directory.EnumerateDirectories(
                     source,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            {
                throw new SnapshotException(
                    "MORPHANTMSB012",
                    $"Snapshot directory '{directory}' is a symbolic link or " +
                    "reparse point. Morphant refuses to follow it during a " +
                    "transactional publication.");
            }

            CopyDirectory(
                directory,
                Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.EnumerateFiles(
                     source,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
            {
                throw new SnapshotException(
                    "MORPHANTMSB012",
                    $"Snapshot file '{file}' is a symbolic link or reparse " +
                    "point.");
            }

            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.EnumerateDirectories(
                     source,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            {
                throw new SnapshotException(
                    "MORPHANTMSB012",
                    $"Snapshot directory '{directory}' is a symbolic link or " +
                    "reparse point.");
            }

            CopyDirectory(
                directory,
                Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static byte[] CreateOutputsProject(
        SnapshotPath path,
        SnapshotManifest manifest)
    {
        var sourceFiles = manifest.Files
            .Select(file => Path.Combine(path.SliceDirectory, file.Name))
            .Append(path.SnapshotManifest)
            .Append(path.RootManifest)
            .OrderBy(static file => file, StringComparer.Ordinal)
            .ToArray();
        var outputFiles = sourceFiles
            .Append(path.TrustedRootManifest)
            .OrderBy(static file => file, StringComparer.Ordinal)
            .ToArray();
        var result = new StringBuilder()
            .AppendLine("<Project>")
            .AppendLine("  <ItemGroup>");

        foreach (var file in sourceFiles)
        {
            var escaped = EscapeXml(file);
            result.Append("    <UpToDateCheckInput Include=\"")
                .Append(escaped)
                .AppendLine("\" />");
        }

        foreach (var file in outputFiles)
        {
            var escaped = EscapeXml(file);
            result.Append("    <UpToDateCheckOutput Include=\"")
                .Append(escaped)
                .AppendLine("\" />");
            result.Append("    <CustomAdditionalCompileOutputs Include=\"")
                .Append(escaped)
                .AppendLine("\" />");
        }

        result
            .AppendLine("  </ItemGroup>")
            .AppendLine("</Project>");

        return SnapshotManifestFormat.Utf8.GetBytes(
            result.ToString().Replace("\r\n", "\n"));
    }

    private static void ValidateOwnedSlice(
        SnapshotPath path,
        SnapshotRootEntry entry)
    {
        var directory = SlicePath(path, entry.SliceRelativePath);
        path.EnsureSafeSnapshotDescendant(
            directory,
            $"snapshot slice '{entry.SliceRelativePath}'");
        var manifestPath = Path.Combine(
            directory,
            SnapshotManifestFormat.FileName);

        if (!File.Exists(manifestPath))
        {
            throw new SnapshotException(
                "MORPHANTMSB013",
                $"Owned snapshot slice '{entry.SliceRelativePath}' has no " +
                "manifest. Morphant will not delete it.");
        }

        EnsureFileIsNotReparsePoint(
            manifestPath,
            $"manifest for snapshot slice '{entry.SliceRelativePath}'");
        var bytes = File.ReadAllBytes(manifestPath);

        if (SnapshotManifestFormat.Hash(bytes) != entry.ManifestHash)
        {
            throw new SnapshotException(
                "MORPHANTMSB013",
                $"Owned snapshot slice '{entry.SliceRelativePath}' does not " +
                "match the root ownership index. Morphant will not delete it.");
        }

        var manifest = SnapshotManifestFormat.Parse(
            bytes,
            $"manifest for snapshot slice '{entry.SliceRelativePath}'");

        if (manifest.ProjectIdentity != path.ProjectIdentity)
        {
            throw new SnapshotException(
                "MORPHANTMSB013",
                $"Snapshot slice '{entry.SliceRelativePath}' belongs to " +
                "another project. Morphant will not delete it.");
        }

        if (entry.SliceRelativePath != manifest.TargetFramework)
        {
            throw new SnapshotException(
                "MORPHANTMSB013",
                $"Snapshot slice '{entry.SliceRelativePath}' does not " +
                "match the target framework recorded by its manifest. " +
                "Morphant will not delete it.");
        }

        foreach (var file in manifest.Files)
        {
            var filePath = Path.Combine(directory, file.Name);

            if (!File.Exists(filePath) ||
                IsReparsePoint(filePath) ||
                SnapshotManifestFormat.Hash(
                    SnapshotManifestFormat.CanonicalSourceBytes(filePath)) !=
                file.Hash)
            {
                throw new SnapshotException(
                    "MORPHANTMSB013",
                    $"Snapshot slice '{entry.SliceRelativePath}' has a " +
                    $"missing, linked, or modified file '{file.Name}'. " +
                    "Morphant will not delete it.");
            }
        }
    }

    private static ObsoleteSliceTransaction CreateObsoleteSliceTransaction(
        SnapshotPath path,
        SnapshotRootEntry entry,
        string token)
    {
        var source = SlicePath(path, entry.SliceRelativePath);
        return new ObsoleteSliceTransaction(
            source,
            source + ".obsolete." + token,
            source + ".retained." + token);
    }

    private static void StageUnrelatedSlice(ObsoleteSliceTransaction slice)
    {
        Directory.CreateDirectory(slice.Replacement);
        CopyUnrelatedEntries(slice.Source, slice.Replacement, clean: true);
        slice.HasReplacement = Directory
            .EnumerateFileSystemEntries(slice.Replacement)
            .Any();

        if (!slice.HasReplacement)
        {
            Directory.Delete(slice.Replacement);
        }
    }

    private static string SlicePath(SnapshotPath path, string relativePath) =>
        Path.Combine(
            path.SnapshotRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static bool ManifestIdentityMatches(
        SnapshotPath path,
        SnapshotManifest manifest) =>
        manifest.ProjectIdentity == path.ProjectIdentity &&
        manifest.TargetFramework == path.TargetFramework;

    private static void EnsureForceCompileStamp(string path)
    {
        if (File.Exists(path))
        {
            EnsureFileIsNotReparsePoint(
                path,
                "Morphant force-compilation stamp");
            return;
        }

        WriteAllBytes(path, []);
    }

    private static bool FileContentsEqual(string path, byte[] expected) =>
        File.Exists(path) &&
        !IsReparsePoint(path) &&
        File.ReadAllBytes(path).SequenceEqual(expected);

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static void EnsureFileIsNotReparsePoint(
        string path,
        string description)
    {
        if (IsReparsePoint(path))
        {
            throw new SnapshotException(
                "MORPHANTMSB012",
                $"The {description} '{path}' is a symbolic link or reparse " +
                "point. Morphant refuses to trust or replace it.");
        }
    }

    private static void RepairPrivateStateTransactionally(
        SnapshotPath path,
        byte[] manifestBytes,
        byte[] rootBytes,
        byte[] outputsBytes)
    {
        var replaceTrusted = !FileContentsEqual(
            path.TrustedManifest,
            manifestBytes);
        var replaceOutputs = !FileContentsEqual(
            path.OutputsProject,
            outputsBytes);
        var replaceTrustedRoot = !FileContentsEqual(
            path.TrustedRootManifest,
            rootBytes);

        if (!replaceTrusted && !replaceOutputs && !replaceTrustedRoot)
        {
            return;
        }

        var token = Guid.NewGuid().ToString("N");
        var trustedTemporary = path.TrustedManifest + ".temporary." + token;
        var trustedBackup = path.TrustedManifest + ".backup." + token;
        var outputsTemporary = path.OutputsProject + ".temporary." + token;
        var outputsBackup = path.OutputsProject + ".backup." + token;
        var trustedRootTemporary =
            path.TrustedRootManifest + ".temporary." + token;
        var trustedRootBackup =
            path.TrustedRootManifest + ".backup." + token;
        var trustedMoved = false;
        var outputsMoved = false;
        var trustedRootMoved = false;
        var committed = false;

        try
        {
            if (replaceTrusted)
            {
                WriteAllBytes(trustedTemporary, manifestBytes);
            }

            if (replaceOutputs)
            {
                WriteAllBytes(outputsTemporary, outputsBytes);
            }

            if (replaceTrustedRoot)
            {
                WriteAllBytes(trustedRootTemporary, rootBytes);
            }

            if (replaceOutputs)
            {
                ReplaceFile(
                    path.OutputsProject,
                    outputsTemporary,
                    outputsBackup);
                outputsMoved = true;
            }

            if (replaceTrusted)
            {
                ReplaceFile(
                    path.TrustedManifest,
                    trustedTemporary,
                    trustedBackup);
                trustedMoved = true;
            }

            if (replaceTrustedRoot)
            {
                ReplaceFile(
                    path.TrustedRootManifest,
                    trustedRootTemporary,
                    trustedRootBackup);
                trustedRootMoved = true;
            }

            committed = true;
        }
        catch
        {
            if (trustedRootMoved)
            {
                RestoreFile(
                    path.TrustedRootManifest,
                    trustedRootTemporary,
                    trustedRootBackup);
            }

            if (trustedMoved)
            {
                RestoreFile(
                    path.TrustedManifest,
                    trustedTemporary,
                    trustedBackup);
            }

            if (outputsMoved)
            {
                RestoreFile(
                    path.OutputsProject,
                    outputsTemporary,
                    outputsBackup);
            }

            throw;
        }
        finally
        {
            TryDeleteFile(trustedTemporary);
            TryDeleteFile(outputsTemporary);
            TryDeleteFile(trustedRootTemporary);

            if (committed)
            {
                TryDeleteFile(trustedBackup);
                TryDeleteFile(outputsBackup);
                TryDeleteFile(trustedRootBackup);
            }
        }
    }

    private static void WriteAllBytes(string path, byte[] contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, contents);
    }

    private static void ReplaceFile(
        string destination,
        string temporary,
        string backup)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        var movedExisting = false;

        try
        {
            if (File.Exists(destination))
            {
                File.Move(destination, backup);
                movedExisting = true;
            }

            File.Move(temporary, destination);
        }
        catch
        {
            if (movedExisting &&
                !File.Exists(destination) &&
                File.Exists(backup))
            {
                File.Move(backup, destination);
            }

            throw;
        }
    }

    private static void RestoreFile(
        string destination,
        string temporary,
        string backup)
    {
        DeleteFileIfExists(destination);

        if (File.Exists(backup))
        {
            File.Move(backup, destination);
        }

        DeleteFileIfExists(temporary);
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static IEnumerable<string> EnumerateFilesWithoutLinks(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count != 0)
        {
            var current = pending.Pop();

            foreach (var directory in Directory.EnumerateDirectories(current))
            {
                if ((File.GetAttributes(directory) &
                     FileAttributes.ReparsePoint) != 0)
                {
                    throw new SnapshotException(
                        "MORPHANTMSB014",
                        $"Private compiler staging directory '{directory}' " +
                        "is a symbolic link or reparse point. Morphant " +
                        "refuses to follow it during cleanup.");
                }

                pending.Push(directory);
            }

            foreach (var file in Directory.EnumerateFiles(current))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) !=
                    0)
                {
                    throw new SnapshotException(
                        "MORPHANTMSB014",
                        $"Private compiler staging file '{file}' is a " +
                        "symbolic link or reparse point. Morphant refuses to " +
                        "follow it during cleanup.");
                }

                yield return file;
            }
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            DeleteFileIfExists(path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            DeleteDirectoryIfExists(path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string EscapeXml(string value) =>
        value.Replace("&", "&amp;")
            .Replace("\"", "&quot;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("'", "&apos;");

    private sealed record SnapshotValidation(
        bool IsValid,
        SnapshotManifest? Manifest)
    {
        public static SnapshotValidation Invalid { get; } = new(false, null);
    }

    private sealed class ObsoleteSliceTransaction(
        string source,
        string backup,
        string replacement)
    {
        public string Source { get; } = source;

        public string Backup { get; } = backup;

        public string Replacement { get; } = replacement;

        public bool HasReplacement { get; set; }

        public bool BackupCreated { get; set; }

        public bool ReplacementInstalled { get; set; }
    }
}
