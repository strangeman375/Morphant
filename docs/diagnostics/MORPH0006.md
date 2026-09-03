# MORPH0006: Mapper must be partial

## Cause

A mapper marked with `[MorphantMapper]` is not declared `partial`. Morphant
cannot add the generated mapper implementation to it.

## Fix

Add `partial` to the mapper declaration. If the type has several declarations,
make every declaration partial.

```csharp
[MorphantMapper]
public sealed partial class ApplicationMapper : TypeMapper<ApplicationMapper>
{
}
```

[All diagnostics](../diagnostics.md)
