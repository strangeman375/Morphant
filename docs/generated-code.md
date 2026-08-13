# Generated code

Morphant generates source files in the consumer compilation. They contain the
fluent mapping surface and the executable mapper implementation.

## Inspect generated files

Enable compiler-generated file output in the consumer project:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

After a build, Morphant files appear below the configured intermediate output
directory. Keep that directory under `obj`; generated files should not be
edited or committed.

## File kinds

| Kind | Contains |
|---|---|
| `Construction` | Destination constructor configuration types |
| `Member` | Destination member configuration types |
| `MappingExtension` | Pair-specific result-policy and `Convert` methods |
| `MemberExtension` | Pair-specific `Members` methods |
| `TypeMapper` | The generated mapper implementation |

Hint names follow this readable form:

```text
Morphant.Generated.<ArtifactKind>.<Identity>.g.cs
```

The set of files depends on the features used by a mapping pair. For
example, a destination without a supported constructor does not receive a
structured construction API, while a manual `Convert` pair still receives an
executable mapper.

Generated files enable nullable annotations and are deterministic for the same
input. Their contents may change between Morphant versions and are not a
public API to reference directly.
