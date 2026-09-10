# Generated code

## View generated code in Rider

Open **Dependencies | Source Generators** in Solution Explorer to see the
current generated output. No project settings are required.

If Rider shows stale output, run **Restart Roslyn Analyzers and Source
Generators** and reopen the generated file. For a generator failure, follow
[`MORPH0057`](diagnostics/MORPH0057.md).

## Save generated code to Git

Enable snapshots in the project that declares your mappers:

```xml
<PropertyGroup>
  <MorphantGitSnapshot>true</MorphantGitSnapshot>
</PropertyGroup>
```

After a successful compilation, generated mapper implementations appear in
`Generated/Morphant/<tfm>` for each target framework where snapshots are enabled.
These files are for review and are excluded from compilation. Commit the entire
snapshot directory together with your mapping changes.

For a multi-targeted project, make `MorphantGitSnapshot` conditional on
`TargetFramework` to avoid saving extra copies. For example, save only `net10.0`:

```xml
<PropertyGroup>
  <MorphantGitSnapshot>false</MorphantGitSnapshot>
  <MorphantGitSnapshot Condition="'$(TargetFramework)' == 'net10.0'">true</MorphantGitSnapshot>
</PropertyGroup>
```

Two optional settings control what is saved and where:

| Setting | Default | Purpose |
|---|---|---|
| `MorphantGitSnapshotDetail` | `Mappers` | Use `Full` to include all Morphant-generated files. |
| `MorphantGitSnapshotPath` | `Generated/Morphant` | Choose a dedicated snapshot directory for this project. |

## Keep the snapshot current

Rebuild the project after enabling snapshots or changing their settings.
A build that skips compilation does not refresh the files. Failed compilations
preserve the previous snapshot.

To change the generated result, edit the mapping or model and rebuild. Avoid
editing the snapshot itself. Review its diff alongside the mapping changes;
see [Testing mappings](testing.md) for behavior checks.

## Build errors

Snapshot errors use `MORPHANTMSB` codes. See [Build diagnostics](build-diagnostics.md)
for each code's cause and how to fix it.
