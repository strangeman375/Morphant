# Generated code

[View in Rider](#view-generated-code-in-rider) · [Save to Git](#save-generated-code-to-git) ·
[Choose builds](#choose-which-builds-publish) · [Refresh](#keep-the-snapshot-current) ·
[CI](#ci-and-parallel-builds) ·
[Storage](#storage-and-recovery) · [Errors](#build-errors)

## View generated code in Rider

Open **Dependencies | Source Generators** in Solution Explorer. This is the
current generated output and requires no project settings.

`Generated/Morphant` is a Git snapshot, not the live Rider view. If Rider shows
stale output, run **Restart Roslyn Analyzers and Source Generators** and reopen
the generated file.

For [`MORPH0057`](diagnostics/MORPH0057.md), open the generated failure file
named in the diagnostic to see the full exception and stack trace.

## Save generated code to Git

Enable the snapshot in the consumer project:

```xml
<PropertyGroup>
  <MorphantGitSnapshot>true</MorphantGitSnapshot>
</PropertyGroup>
```

Morphant enables `EmitCompilerGeneratedFiles` automatically, overriding an
ordinary project value of `false`. When snapshots are enabled, a global
`-p:EmitCompilerGeneratedFiles=false` prevents this and produces a build error.

After a successful compilation, files appear under
`Generated/Morphant/<tfm>`. Failed compilations leave the previous snapshot intact,
and snapshot files are excluded from compilation.

The optional settings are:

| Setting | Default | Purpose |
|---|---|---|
| `MorphantGitSnapshotDetail` | `Mappers` | Use `Full` to include all Morphant-generated files. Values are case-insensitive. |
| `MorphantGitSnapshotPath` | `Generated/Morphant` | Dedicated snapshot directory, inside or outside the project. |

## Choose which builds publish

Each successful compilation updates the snapshot for its current TFM. For
`net8.0;net10.0`, `dotnet build` updates both snapshots, while `dotnet build -f net8.0`
updates only `net8.0`. The order in `TargetFrameworks` does not matter. Referenced
projects, including `netstandard2.0` libraries, update their own snapshots when
the feature is enabled for their compilations.

Use ordinary MSBuild conditions to control publication. This example saves all
Morphant-generated files for every TFM except `net8.0`:

```xml
<PropertyGroup>
  <MorphantGitSnapshot>true</MorphantGitSnapshot>
  <MorphantGitSnapshot Condition="'$(TargetFramework)' == 'net8.0'">false</MorphantGitSnapshot>
  <MorphantGitSnapshotDetail>Full</MorphantGitSnapshotDetail>
</PropertyGroup>
```

The condition works for both full and single-TFM builds and can also use
`Configuration` or other MSBuild properties. Disabled compilations preserve
existing snapshots. A global `-p:MorphantGitSnapshot=true` overrides project values
and enables publication for every compiled TFM.

## Keep the snapshot current

After enabling the feature, changing its settings, or manually deleting or
editing a snapshot file, run:

```bash
dotnet build -c Release -t:Rebuild
```

An up-to-date build may skip compilation and therefore may not repair the
snapshot. Change mappings or models instead of editing generated files.
Morphant reports the destination and counts of updated, removed and unchanged
files after publication. If compilation is skipped, it reports that the snapshot
was not updated and suggests a rebuild. Design-time and `--no-build` operations
do not publish snapshots.

## CI and parallel builds

Debug and Release update the same snapshot; when built sequentially, the last
successful build wins. Morphant does not lock directories or coordinate builds.
Each TFM snapshot directory must have one publisher at a time. Parallel CI jobs
that publish the same TFM need separate checkouts or `MorphantGitSnapshotPath`
values; alternatively, enable publication in only one job. Different TFM slices
of the same project's snapshot can be published independently.
Build the intended configuration before committing.
Changing `MorphantGitSnapshotPath` does not delete the old directory.

To check a committed snapshot in CI, enable snapshots in the project and run this
Bash example from its directory. Ensure that snapshot files and `.morphant/owner` are tracked
and not ignored. The Git check includes new, untracked files:

```bash
set -e
dotnet build -c Release -t:Rebuild
snapshot_changes=$(git status --porcelain --untracked-files=all -- Generated/Morphant)
test -z "$snapshot_changes"
```

Use the intended TFM, configuration and snapshot path for your project.
Existing `TargetsTriggeredByCompilation` hooks remain active. See
[Testing mappings](testing.md) for behavioral checks.

## Storage and recovery

### Compiler output

Compiler output is staged separately from the Git snapshot. Morphant defaults
`CompilerGeneratedFilesOutputPath` to
`$(IntermediateOutputPath)/Morphant.CompilerGenerated` when no effective path
is set. Existing SDK and custom paths, including external paths, are preserved.
The compiler output and snapshot directories must not overlap. For example:

```bash
dotnet build -c Release -t:Rebuild -p:CompilerGeneratedFilesOutputPath=obj/Release/net10.0/MyGenerated
```

Use the actual configuration and TFM of your project in the path. When setting
the path in the project file, also set `EmitCompilerGeneratedFiles` to `true`
so the SDK retains it. Command-line properties take precedence. Neither path
needs to be inside `BaseIntermediateOutputPath` or `IntermediateOutputPath`.
Concurrent compilations need distinct compiler directories for projects,
configurations, TFMs and RIDs. Sequential compilations can reuse a compiler
directory; Morphant does not bind it to a project or compilation.

### Project ownership

Each snapshot directory belongs to one project. A repository-wide layout can
use `snapshots/App` and `snapshots/Library`. Morphant records ownership in
`.morphant/owner`; keep this small directory with the snapshot. Its project path is
relative when both locations are on the same filesystem root. Moving the whole
checkout together with its snapshots preserves ownership. To transfer a directory
to another project, remove its old snapshot and ownership directory deliberately.
After renaming or moving the project relative to its snapshot, remove `.morphant`
and rebuild to record the new project path.
An existing snapshot ownership record only needs read access; it can remain
read-only while writable generated files are updated. Compiler directories
do not require ownership files.

### Paths and cleanup

Links in the configured paths are resolved before checking overlaps and ownership.
Morphant preserves unrelated links during cleanup and rejects links in generated
files or directories it must modify. Paths must be valid on the current OS;
MSBuild special characters in property values still require normal MSBuild escaping.

For stable line endings across platforms, add:

```gitattributes
**/Morphant.Generated.*.g.cs text eol=crlf
```

Morphant removes obsolete generated files only from the current TFM slice after
a successful compilation and preserves unrelated files. Other TFM slices remain
intact, even if a framework is disabled or removed. Delete unwanted framework
directories explicitly.

Publication updates files individually. An I/O failure or cancellation during
publication may leave a partially updated snapshot; correct the cause and run a
rebuild to refresh it.

## Build errors

Git snapshot errors use `MORPHANTMSB` codes. They are MSBuild errors and are
not controlled by C# pragmas or `dotnet_diagnostic` severity settings.

| Code | Cause and action |
|---|---|
| `MORPHANTMSB001` | Unknown snapshot task operation. Use the package's imported targets. |
| `MORPHANTMSB002` | Generated-file output is disabled. Remove the override of `EmitCompilerGeneratedFiles`. |
| `MORPHANTMSB003` | Compiler output and snapshot paths overlap, possibly through a link. Use separate directories. |
| `MORPHANTMSB004` | Compiler storage is a project root or ancestor. Use a dedicated compilation directory. |
| `MORPHANTMSB005` | Snapshot storage is a project root/ancestor, belongs to another project, or has an invalid ownership record. Follow the reported project paths and recovery instructions. |
| `MORPHANTMSB006` | A path is empty or invalid on the current operating system. Supply one valid path. |
| `MORPHANTMSB007` | `TargetFramework` cannot form a portable directory name. Correct the reported value. |
| `MORPHANTMSB008` | Generated filenames are nonportable or collide ignoring case. Check the reported name and generator inputs. |
| `MORPHANTMSB015` | A file occupies a required directory, or a directory occupies a generated filename. Move the conflicting item. |
| `MORPHANTMSB016` | A link is broken/cyclic or occupies a managed file or directory that Morphant must modify. Correct the indicated path. |
| `MORPHANTMSB020` | `MorphantGitSnapshotDetail` must be `Mappers` or `Full`. |
| `MORPHANTMSB022` | `MorphantGitSnapshot` must be `true` or `false`. |
| `MORPHANTMSB999` | Snapshot I/O or unexpected failure. Check the reported operation, paths and access permissions, then rebuild. For I/O failures, use `-v:diagnostic` to include the full exception details. |
