# MORPH0005: Mapper must derive from TypeMapper

## Cause

A type marked with `[MorphantMapper]` does not derive from `TypeMapper`.

## Fix

Add the required base type, or remove `[MorphantMapper]` if the type is not a
Morphant mapper:

```csharp
[MorphantMapper]
public sealed partial class ApplicationMapper : TypeMapper
{
}
```

[All diagnostics](../diagnostics.md)
