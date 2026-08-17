# Generated code

Morphant generates source files while the consumer project is built. They
contain the fluent mapping API and mapper implementation.

## Inspect live output in Rider

For the current design-time result, use **Dependencies | Source Generators**
in Solution Explorer. This is Rider's live compiler view and does not require
`EmitCompilerGeneratedFiles`.

Do not use a file under `Generated/Morphant` as a live view. That directory is
an on-disk build snapshot: it changes only when the compiler runs, while Rider
keeps a separate design-time document. An editor tab opened from one view does
not prove that the other view has refreshed.

After changing `Map`, `Members`, `ConstructUsing`, or `Convert`, save the
configuration and inspect the file under **Source Generators**. If a normal
`dotnet build` has the new behavior but Rider still shows an old document,
update Rider first. Older Rider releases have had source-generator refresh
bugs. As a temporary IDE recovery, invoke **Restart Roslyn Analyzers and Source
Generators** and reopen the generated document.

## Store generated files in Git

Use an explicit on-disk directory only when generated files must participate
in review or Git history:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>Generated/Morphant</CompilerGeneratedFilesOutputPath>
</PropertyGroup>

<ItemGroup>
  <Compile Remove="$(CompilerGeneratedFilesOutputPath)/**/*.cs" />
</ItemGroup>
```

Commit this directory together with the mapping configuration. Generated code
then participates in code review and Git history, making mapping changes easy
to inspect and revert.

The `Compile Remove` entry prevents the committed copies from being compiled a
second time: Morphant already adds the freshly generated versions to the
current compilation. During a real build, the Morphant package clears only its
own generator directory before Roslyn writes the current output set. This is
necessary because Roslyn otherwise leaves files behind when a generated hint
disappears, for example after removing a `Map` call. Other generators' output
is not touched.

Do not edit generated files directly. Change the mapping configuration or
mapped types, run a build, and commit both the source change and the updated
generated files. Set `MorphantCleanCompilerGeneratedFiles` to `false` only if
another build step owns cleanup of the Morphant output directory.

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
