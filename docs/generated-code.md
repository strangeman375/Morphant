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
`Generated/Morphant/<tfm>`. Failed builds leave the previous snapshot intact,
and snapshot files are excluded from compilation.

The optional settings are:

| Setting | Default | Purpose |
|---|---|---|
| `MorphantGitSnapshotDetail` | `Mappers` | Use `Full` to include all Morphant-generated files. Values are case-insensitive. |
| `MorphantGitSnapshotTargetFrameworks` | Last declared TFM | Semicolon-separated subset of the project's TFMs; use `$(TargetFrameworks)` to select all. |
| `MorphantGitSnapshotPath` | `Generated/Morphant` | Dedicated snapshot directory inside the project. |

Compiler output is staged separately from the Git snapshot. Morphant defaults
`CompilerGeneratedFilesOutputPath` to
`$(IntermediateOutputPath)/Morphant.CompilerGenerated` when no effective path
is set. An existing SDK or custom path is preserved if it names a subdirectory
inside the current `IntermediateOutputPath`, without symbolic links. For example:

```bash
dotnet build -c Release -t:Rebuild -p:CompilerGeneratedFilesOutputPath=obj/Release/net10.0/MyGenerated
```

Use the actual configuration and TFM of your project in the path. When setting
the path in the project file, also set `EmitCompilerGeneratedFiles` to `true`
so the SDK retains it. Command-line properties take precedence. The intermediate
directory itself and directories outside it cannot be used as staging.

In multi-target projects, list `TargetFrameworks` from oldest to newest. For
example, `net8.0;net10.0` selects only `net10.0` by default. Every explicitly
selected TFM must also be declared by the project.

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
successful build wins. Build the intended configuration before committing.
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
| `MORPHANTMSB003` | Intermediate and snapshot paths overlap or escape their required parent. Use separate directories. |
| `MORPHANTMSB004` | Compiler output is outside `IntermediateOutputPath` or equals it. Set `CompilerGeneratedFilesOutputPath` to a dedicated subdirectory inside it. |
| `MORPHANTMSB005` | The snapshot path is outside the project or equals its root. Choose a dedicated project subdirectory. |
| `MORPHANTMSB006` | A path is empty, nonportable or contains wildcards or unresolved MSBuild expressions. Supply a literal path. |
| `MORPHANTMSB007` | A framework name cannot form a portable directory name. Correct the indicated framework property. |
| `MORPHANTMSB008` | Generated filenames are nonportable or collide ignoring case. Check the reported name and generator inputs. |
| `MORPHANTMSB015` | A file occupies a required directory, or a directory occupies a generated filename. Move the conflicting item. |
| `MORPHANTMSB016` | A managed path contains a symbolic link or reparse point. Choose a directory without links. |
| `MORPHANTMSB017` | Post-compilation publication was removed. Remove the override of `TargetsTriggeredByCompilation`. |
| `MORPHANTMSB019` | Another build held the snapshot lock for two minutes. Finish or stop that build, then retry. |
| `MORPHANTMSB020` | `MorphantGitSnapshotDetail` must be `Mappers` or `Full`. |
| `MORPHANTMSB021` | The selected framework list is empty or includes an undeclared framework. Select from the project's frameworks. |
| `MORPHANTMSB022` | `MorphantGitSnapshot` must be `true` or `false`. |
| `MORPHANTMSB999` | Unexpected snapshot failure. Use the included exception details to investigate and report a reproducible failure. |
