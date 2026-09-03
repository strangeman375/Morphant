# MORPH0007: Containing type must be partial

## Cause

A nested mapper is declared inside a containing type that is not `partial`.
Every containing type must allow the generated nested declaration to be added.

## Fix

Add `partial` to the reported containing type and to every other non-partial
type that contains the mapper, or move the mapper to the top level.

```csharp
public partial class Container
{
    [MorphantMapper]
    public sealed partial class ApplicationMapper :
        TypeMapper<ApplicationMapper>
    {
    }
}
```

[All diagnostics](../diagnostics.md)
