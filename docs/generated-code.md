# Generated code

## Configuration surfaces

Morphant ships `Map` and common settings as instance APIs. Callback extensions
are generated for each mapper or reusable mapper family. Independent mappers
can configure the same types with different tuple names, nullable annotations,
or `dynamic`/`object` declarations, including in assemblies that expose their
internals to one another.

A mapper identifies its configuration by deriving from `TypeMapper<TMapper>`
with itself as `TMapper`. A reusable base passes the final mapper type through
its generic hierarchy; see [Configuration inheritance](configuration-inheritance.md).

For a tuple-containing pair, one effective mapper, including its connected
base configuration, must use one presentation. Conflicting declarations
produce [`MORPH0056`](diagnostics/MORPH0056.md).

## Mapper accessibility

Configuration extensions are emitted as namespace-level generated code. A
type marked with `[MorphantMapper]` and every type containing it must therefore
be accessible from namespace-level code in the same assembly. `public`,
`internal`, and `protected internal` declarations satisfy this requirement;
`private`, `protected`, and `private protected` declarations do not and produce
[`MORPH0059`](diagnostics/MORPH0059.md).

Accessibility is cumulative. For example, a public mapper nested in a private
container is not accessible to generated code.

## View generated code in Rider

Open **Dependencies | Source Generators** in Solution Explorer. This is the
current generated output and requires no project settings.

`Generated/Morphant` is a Git snapshot, not the live Rider view. If Rider shows
stale output, run **Restart Roslyn Analyzers and Source Generators** and reopen
the generated file.

If Morphant catches an internal exception, the **Problems** window reports
[`MORPH0057`](diagnostics/MORPH0057.md) with the failed stage and exception.
Open the named `Morphant.Generated.GeneratorFailure.*.g.cs` file under **Source
Generators** to read the complete stack trace without searching IDE logs.

## Save generated code to Git

Enable the snapshot in the consumer project:

```xml
<PropertyGroup>
  <MorphantGitSnapshot>true</MorphantGitSnapshot>
</PropertyGroup>
```

Morphant enables `EmitCompilerGeneratedFiles` automatically, overriding an
ordinary project value of `false`. A global
`-p:EmitCompilerGeneratedFiles=false` prevents this and produces a build error.

After a successful compilation, files appear under
`Generated/Morphant/<tfm>`. Failed compilations leave the previous snapshot intact,
and snapshot files are excluded from compilation.

The optional settings are:

| Setting | Default | Purpose |
|---|---|---|
| `MorphantGitSnapshotDetail` | `Mappers` | Use `Full` to include all Morphant-generated files. Values are case-insensitive. |
| `MorphantGitSnapshotTargetFrameworks` | Last declared TFM | Semicolon-separated preferred TFMs; use `$(TargetFrameworks)` to select all. |
| `MorphantGitSnapshotPath` | `Generated/Morphant` | Dedicated snapshot directory, inside or outside the project. |

Compiler output is staged separately from the Git snapshot. Morphant defaults
`CompilerGeneratedFilesOutputPath` to
`$(IntermediateOutputPath)/Morphant.CompilerGenerated` when no effective path
is set. Existing SDK and custom paths are preserved, including external paths.
The compiler output and snapshot directories must not overlap. For example:

```bash
dotnet build -c Release -t:Rebuild -p:CompilerGeneratedFilesOutputPath=obj/Release/net10.0/MyGenerated
```

Use the actual configuration and TFM of your project in the path. When setting
the path in the project file, also set `EmitCompilerGeneratedFiles` to `true`
so the SDK retains it. Command-line properties take precedence. Neither path
needs to be inside `BaseIntermediateOutputPath` or `IntermediateOutputPath`.
Use distinct compiler directories for projects, configurations, TFMs and RIDs;
several compilations cannot share the same compiler directory.

Each snapshot directory belongs to one project. A repository-wide layout can
use `snapshots/App` and `snapshots/Library`. Morphant records ownership in a
small `.morphant` file; keep that file with the snapshot. Its project path is
relative when both locations are on the same filesystem root. Moving the whole
checkout together with its snapshots preserves ownership. To transfer a directory
to another project, remove its old snapshot and ownership file deliberately.
Compiler directories also contain an ownership file and can be reset by removing
the dedicated compiler directory.
An existing, initialized ownership file only needs read access; it can remain
read-only while writable generated files are updated.

Links in the configured paths are resolved before checking overlaps and ownership.
Morphant preserves unrelated links during cleanup and rejects links in generated
files or directories it must modify. Paths must be valid on the current OS;
MSBuild special characters in property values still require normal MSBuild escaping.

In multi-target projects, list `TargetFrameworks` from oldest to newest. For
example, `net8.0;net10.0` selects only `net10.0` by default. An explicit selection
uses the TFMs declared by both the setting and the project. If none match,
Morphant uses the project's last declared TFM and logs the fallback. Thus a
global `net10.0` selection still updates a `netstandard2.0` dependency's snapshot.
Only TFMs actually compiled by the build can update their snapshots.

Existing `TargetsTriggeredByCompilation` hooks are retained, including global
command-line values; Morphant appends its publication target locally.

Example with all generated files for every TFM:

```xml
<PropertyGroup>
  <MorphantGitSnapshot>true</MorphantGitSnapshot>
  <MorphantGitSnapshotDetail>Full</MorphantGitSnapshotDetail>
  <MorphantGitSnapshotTargetFrameworks>
    $(TargetFrameworks)
  </MorphantGitSnapshotTargetFrameworks>
</PropertyGroup>
```

## Keep the snapshot current

After enabling the feature, changing its settings, or manually deleting or
editing a snapshot file, run:

```bash
dotnet build -c Release -t:Rebuild
```

An up-to-date build may skip compilation and therefore may not repair the
snapshot. Change mappings or models instead of editing generated files.

Debug and Release update the same snapshot; if their output differs, the last
successful build wins. Publications wait for the same directory's lock and stop
waiting when the build is cancelled. Build the intended configuration before committing.
Changing `MorphantGitSnapshotPath` does not delete the old directory.

For stable line endings across platforms, add:

```gitattributes
**/Morphant.Generated.*.g.cs text eol=crlf
```

Morphant removes obsolete generated files after a successful compilation and
preserves unrelated files in the snapshot directory.

See [Testing mappings](testing.md) for generated-diff checks.

## Build errors

Git snapshot errors use `MORPHANTMSB` codes. They are MSBuild errors and are
not controlled by C# pragmas or `dotnet_diagnostic` severity settings.

| Code | Cause and action |
|---|---|
| `MORPHANTMSB001` | Unknown snapshot task operation. Use the package's imported targets. |
| `MORPHANTMSB002` | Generated-file output is disabled. Remove the override of `EmitCompilerGeneratedFiles`. |
| `MORPHANTMSB003` | Compiler output and snapshot paths overlap, possibly through a link. Use separate directories. |
| `MORPHANTMSB004` | Compiler storage is a project root/ancestor or belongs to another project, configuration, TFM or RID. Use a dedicated compilation directory. |
| `MORPHANTMSB005` | Snapshot storage is a project root/ancestor or belongs to another project. Choose a dedicated directory for this project. |
| `MORPHANTMSB006` | A path is empty or invalid on the current operating system. Supply one valid path. |
| `MORPHANTMSB007` | A framework name cannot form a portable directory name. Correct the indicated framework property. |
| `MORPHANTMSB008` | Generated filenames are nonportable or collide ignoring case. Check the reported name and generator inputs. |
| `MORPHANTMSB015` | A file occupies a required directory, or a directory occupies a generated filename. Move the conflicting item. |
| `MORPHANTMSB016` | A link is broken/cyclic or occupies a managed file or directory that Morphant must modify. Correct the indicated path. |
| `MORPHANTMSB020` | `MorphantGitSnapshotDetail` must be `Mappers` or `Full`. |
| `MORPHANTMSB021` | The explicit framework list contains no names. Supply at least one TFM or leave the setting unset. |
| `MORPHANTMSB022` | `MorphantGitSnapshot` must be `true` or `false`. |
| `MORPHANTMSB999` | Unexpected snapshot failure. Use the included exception details to investigate and report a reproducible failure. |
