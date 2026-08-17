# Generated code

Morphant generates source files while the consumer project is built. They
contain the fluent mapping API and mapper implementation.

## Inspect live output in Rider

For the current design-time result, use **Dependencies | Source Generators**
in Solution Explorer. This is Rider's live compiler view and does not require
`EmitCompilerGeneratedFiles`.

Do not use a file under `Generated/Morphant` as a live view. That directory is
an on-disk Git snapshot: it changes only after a successful compiler run, while
Rider keeps a separate design-time document. An editor tab opened from one view
does not prove that the other view has refreshed.

After changing `Map`, `Members`, `ConstructUsing`, or `Convert`, save the
configuration and inspect the file under **Source Generators**. If a normal
`dotnet build` has the new behavior but Rider still shows an old document,
update Rider first. Older Rider releases have had source-generator refresh
bugs. As a temporary IDE recovery, invoke **Restart Roslyn Analyzers and Source
Generators** and reopen the generated document.

## Store generated files in Git

Enable Morphant's supported Git snapshot when generated files must participate
in review or Git history:

```xml
<PropertyGroup>
  <MorphantGitSnapshot>true</MorphantGitSnapshot>
</PropertyGroup>
```

The default root is `Generated/Morphant`. The root must be one literal,
dedicated subdirectory of the consumer project. Project roots, parent or
external/shared directories, wildcards, and item lists are rejected before
Morphant deletes or publishes anything. Override the root only when the
repository uses another project-owned layout:

```xml
<MorphantGitSnapshotPath>Generated/Mapping</MorphantGitSnapshotPath>
```

Morphant creates one slice per target framework, for example
`Generated/Morphant/net10.0`. Debug and Release publish to that same slice.
When their generated output is identical, switching configurations does not
rewrite the snapshot. If conditional source, `DefineConstants`, or conditional
Morphant properties make the output differ, the last successful build wins.
Before committing, build the configuration whose output the repository treats
as canonical (normally Release) and review the generated diff. Do not run
different configurations concurrently when they can intentionally generate
different results.

Each slice contains a versioned `Morphant.Generated.manifest` with the project
identity, target framework, sorted file names, and SHA-256 hashes. The manifest
does not record the build configuration, so equivalent Debug and Release builds
do not create metadata-only Git changes. A root ownership manifest prevents two
projects from sharing one root. Generated `.g.cs` files use deterministic UTF-8
without BOM and CRLF; both manifests use UTF-8 without BOM and LF.

The package automatically enables Roslyn's file emission into a validated
private directory under `obj`. A command-line or global override of
`EmitCompilerGeneratedFiles` or `CompilerGeneratedFilesOutputPath` that breaks
this boundary fails the build before cleanup. The effective
`IntermediateOutputPath` must remain inside `BaseIntermediateOutputPath`, and
an override of `TargetsTriggeredByCompilation` must retain Morphant's
post-compile publication target. Morphant-owned file names are excluded from
`Compile` regardless of whether snapshot publication is enabled or which root
is currently configured, so disabling the feature or changing the path cannot
introduce duplicate generated declarations.

After compilation succeeds, a specialized MSBuild task stages the complete
new slice and replaces the old slice, ownership index, and trusted state as
one rollback-protected transaction. A failed compiler or publication step
therefore preserves the previous snapshot instead of leaving a partially
updated directory. Parallel builds coordinate through a cross-process lock;
for different configurations that lock prevents corruption but deliberately
does not change the last-successful-build-wins policy.

Cleanup is enabled by default. Morphant mirrors both the current slice manifest
and the shared root ownership index under `obj`; it verifies those trusted
bytes and every generated-file hash before deleting stale files. A missing,
edited, or malformed current-slice manifest or `.g.cs` forces compilation and
restores the current snapshot. Path traversal, duplicates, foreign project
ownership, and an invalid or untrusted root index fail safely without deletion.
Removed target-framework slices are removed from the project-owned root, while
files not owned by Morphant are preserved.

Do not edit generated files directly. Change the mapping configuration or
mapped types, run a build, and commit both the source change and the complete
snapshot. Set `MorphantCleanCompilerGeneratedFiles` to `false` only when
another build step deliberately preserves historical Morphant files. Turning
cleanup back on removes those stale files without requiring a forced rebuild.

Changing `MorphantGitSnapshotPath` does not delete the old committed root.
Reserved Morphant files there remain excluded from compilation, so the build
is safe; remove the old root explicitly after reviewing the new snapshot.

`EmitCompilerGeneratedFiles` and `CompilerGeneratedFilesOutputPath` remain
standard Roslyn diagnostics switches, but they do not provide the supported
Git snapshot lifecycle on their own. Use `MorphantGitSnapshot` for a
reviewable, self-cleaning snapshot.

## File kinds

| Kind | Contains |
|---|---|
| `Construction` | Destination constructor configuration types |
| `Member` | Destination member configuration types |
| `MappingExtension` | Mapping-specific destination selection and `Convert` methods |
| `MemberExtension` | Mapping-specific `Members` methods |
| `TypeMapper` | The generated mapper implementation |

Hint names follow this readable form:

```text
Morphant.Generated.<ArtifactKind>.<Identity>.g.cs
```

The set of files depends on the configured source and destination types. For
example, a destination without a supported constructor does not receive
`Construct` or `Resolve` overloads. A mapping configured with `Convert` still
receives an `ITypeMapper<TSource, TDestination>` implementation.

Generated files enable nullable annotations and are deterministic for the same
input. Their contents may change between Morphant versions. Do not depend on
file names or implementation details; name a generated plan type only in a
documented configuration form such as read-only member Update.

See [Testing mappings](testing.md) for behavior and generated-diff checks.
