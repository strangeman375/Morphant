# Generated code

Morphant generates source files while the consumer project is built. They
contain the fluent mapping API and mapper implementation.

## Inspect generated files

Enable compiler-generated file output in the consumer project:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

After a build, Morphant files appear below the configured intermediate output
directory. Keep that directory under `obj`. The compiler regenerates these
files, so edits would be overwritten and committed copies would only duplicate
build output that can change between Morphant versions.

## File kinds

| Kind | Contains |
|---|---|
| `Construction` | Destination constructor configuration types |
| `Member` | Destination member configuration types |
| `MappingExtension` | Mapping-specific `Construct`, `Resolve` and `Convert` methods |
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
input. Their contents may change between Morphant versions and should not be
referenced from application code.
