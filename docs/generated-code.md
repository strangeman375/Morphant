# Generated code

Morphant generates source files while the consumer project is built. They
contain the fluent mapping API and mapper implementation.

## Store generated files in Git

Write generated files to a stable directory in the consumer project:

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
current compilation. Do not edit generated files directly. Change the mapping
configuration or mapped types, rebuild, and commit both the source change and
the updated generated files.

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
