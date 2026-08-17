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

The default root is `Generated/Morphant`. Override it only when the repository
uses another layout:

```xml
<MorphantGitSnapshotPath>Generated/Mapping</MorphantGitSnapshotPath>
```

Morphant creates one subdirectory per target framework, for example
`Generated/Morphant/net10.0`, and writes a `Morphant.Generated.manifest` next
to the generated files. Commit the complete root together with the mapping
configuration. Generated code then participates in code review and Git
history, making mapping changes easy to inspect and revert.

The package automatically enables Roslyn's file emission into an isolated
directory under `obj`, excludes the checked-in snapshot from `Compile`, and
publishes only `Morphant.Generated.*.g.cs` after compilation succeeds. A
failed or skipped compilation therefore cannot replace the last successful
Git snapshot. Target-framework isolation also makes ordinary parallel
multi-target builds safe.

Cleanup is enabled by default. After a successful build, and also after an
up-to-date build, Morphant removes snapshot files that are absent from the
manifest. This is necessary because Roslyn itself leaves files behind when a
generated hint disappears, for example after removing a `Map` call. Files not
owned by Morphant are preserved.

Do not edit generated files directly. Change the mapping configuration or
mapped types, run a build, and commit both the source change and the complete
snapshot. Set `MorphantCleanCompilerGeneratedFiles` to `false` only when
another build step deliberately preserves historical Morphant files. Turning
cleanup back on removes those stale files without requiring a forced rebuild.

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
