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

By default the snapshot contains only generated mapper implementations
(`TypeMapper` files). These are normally the files that matter during code
review. To include the complete generated surface, including configuration API
and template types, select full detail:

```xml
<PropertyGroup>
  <MorphantGitSnapshot>true</MorphantGitSnapshot>
  <MorphantGitSnapshotDetail>Full</MorphantGitSnapshotDetail>
</PropertyGroup>
```

`MorphantGitSnapshotDetail` accepts `Mappers` (the default) or `Full`.
Changing from `Full` to `Mappers` removes the no-longer-selected Morphant
artifacts after the next successful compiler run; unrelated files remain
untouched.

The default root is `Generated/Morphant`. The root must be one literal,
dedicated subdirectory of the consumer project. Project roots, parent or
external/shared directories, wildcards, and item lists are rejected before
Morphant deletes or publishes anything. Do not share one snapshot root between
projects. Override the root only when the repository uses another
project-owned layout:

```xml
<MorphantGitSnapshotPath>Generated/Mapping</MorphantGitSnapshotPath>
```

For a multi-target project, Morphant publishes only the last framework declared
in `TargetFrameworks` by default. Keep the list ordered from oldest to newest;
for example, `net8.0;net10.0` publishes only
`Generated/Morphant/net10.0`. Morphant follows the declared order rather than
trying to rank framework names or custom aliases.

Use `MorphantGitSnapshotTargetFrameworks` to publish a different subset. Every
value must also occur in the project's `TargetFramework` or `TargetFrameworks`:

```xml
<MorphantGitSnapshotTargetFrameworks>
  net8.0;net10.0
</MorphantGitSnapshotTargetFrameworks>
```

To publish every declared framework, use:

```xml
<MorphantGitSnapshotTargetFrameworks>
  $(TargetFrameworks)
</MorphantGitSnapshotTargetFrameworks>
```

Whitespace and duplicate entries are ignored. An undeclared value fails the
build with an actionable error. A build of a non-selected framework does not
prepare, publish, or clean the snapshot. After the next successful compilation
of a selected framework, Morphant removes its generated files from slices that
are no longer selected; unrelated files remain untouched.

Debug and Release publish to the same slice for each selected framework. When
their generated output is identical, switching configurations does not rewrite
the snapshot. If conditional source, `DefineConstants`, or conditional Morphant
properties make the output differ, the last successful build wins. Before
committing, rebuild the configuration whose output the repository treats as
canonical, normally Release, and review the generated diff:

```bash
dotnet build -c Release -t:Rebuild
```

The package keeps Roslyn's generated-file emission pointed at a validated
private directory under `obj` for both normal and design-time compiler command
lines. This keeps IDE source-generator documents discoverable; the IDE still
uses its live compiler view, and design-time builds never publish or clean the
Git snapshot. Only after a normal `Csc` succeeds does a small MSBuild task copy
the selected Morphant files into the Git snapshot. It compares file contents,
leaves identical files and timestamps untouched, removes stale or filtered-out
Morphant files, and preserves unrelated files. Morphant files in removed or
no-longer-selected target-framework slices are cleaned on the next successful
compilation of a selected framework. Debug, Release, and parallel builds of
selected frameworks coordinate through a short cross-process lock.

A compiler error does not publish private staging, so the previous Git snapshot
remains intact. A command-line or global override of
`EmitCompilerGeneratedFiles`, `CompilerGeneratedFilesOutputPath`, or
`TargetsTriggeredByCompilation` that breaks the staging contract fails before
cleanup. Morphant-owned file names are added to the SDK's early
`DefaultItemExcludes` and defensively removed from `Compile` again, so Rider and
MSBuild treat snapshots as review artifacts rather than source inputs even when
snapshot publication is disabled or its root changes.

The snapshot deliberately has no manifest or trusted state. Consequently an
up-to-date build, where `Csc` does not run, cannot discover a deleted or manually
edited snapshot file. Run `Rebuild` to restore the complete current set. A rare
file-system error during publication can leave a partially updated working
tree; the build fails, and the next `Rebuild` restores it. Git remains the
recovery point for the last committed snapshot.

Do not edit generated files directly. Change the mapping configuration or
mapped types, rebuild, and commit both the source change and the complete
snapshot. Generated `.g.cs` files retain Roslyn's UTF-8 encoding and Morphant's
deterministic CRLF line endings.

Keep those bytes stable after Git checkout by adding this rule to the consumer
repository's `.gitattributes`:

```gitattributes
**/Morphant.Generated.*.g.cs text eol=crlf
```

Without the rule, a repository-wide LF policy can make an otherwise unchanged
snapshot get rewritten during each real compilation.

Changing `MorphantGitSnapshotPath` does not delete the old committed root.
Reserved Morphant files there remain excluded from compilation, so the build
is safe; remove the old root explicitly after reviewing the new snapshot.

Enabling the snapshot or changing its path, detail, or target-framework
selection does not itself make `CoreCompile` out of date. Run `Rebuild` once
after any of these changes.

`EmitCompilerGeneratedFiles` and `CompilerGeneratedFilesOutputPath` remain
standard Roslyn diagnostics switches, but they do not provide the supported
Git snapshot lifecycle on their own. Use `MorphantGitSnapshot` for a
reviewable snapshot synchronized after successful compilation.

## File kinds

| Kind | Contains |
|---|---|
| `Construction` | Destination constructor configuration types |
| `Member` | Destination member configuration types |
| `MappingExtension` | Mapping-specific destination selection and `Convert` methods |
| `MemberExtension` | Mapping-specific `Members` methods |
| `TypeMapper` | The generated mapper implementation |

`Mappers` publishes only `TypeMapper`. `Full` publishes every kind in this
table and automatically includes any new Morphant artifact kinds introduced in
future versions.

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
