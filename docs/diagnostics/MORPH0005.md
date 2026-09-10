# MORPH0005: Mapper must derive from TypeMapper

## Cause

A type marked with `[MorphantMapper]` does not derive from
`TypeMapper<TMapper>` with the mapper as its self type.

## Fix

Add the required base type, or remove `[MorphantMapper]` if the type is not a
Morphant mapper:

```csharp
[MorphantMapper]
public sealed partial class ApplicationMapper : TypeMapper<ApplicationMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>();
}
```

[All diagnostics](../diagnostics.md)
