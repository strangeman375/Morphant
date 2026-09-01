# Generated code

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

After a successful compilation, files appear under
`Generated/Morphant/<tfm>`. Failed builds leave the previous snapshot intact,
and snapshot files are excluded from compilation.

The optional settings are:

| Setting | Default | Purpose |
|---|---|---|
| `MorphantGitSnapshotDetail` | `Mappers` | Use `Full` to include all Morphant-generated files. |
| `MorphantGitSnapshotTargetFrameworks` | Last declared TFM | Semicolon-separated subset of the project's TFMs; use `$(TargetFrameworks)` to select all. |
| `MorphantGitSnapshotPath` | `Generated/Morphant` | Dedicated snapshot directory inside the project. |

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
