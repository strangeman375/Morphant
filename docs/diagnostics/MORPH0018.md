# MORPH0018: Mapping configuration cannot be analyzed

## Cause

Morphant found a `Map` registration but cannot determine one fixed chain of
settings and mapping rules for it. This can happen when the mapping builder is
stored, passed elsewhere, or configured conditionally.

## Fix

Keep the mapping configuration on the fluent chain that starts with `Map`:

```csharp
builder.Map<Source, Destination>()
    .Construct(source => new(source.Id))
    .Members((source, _) => new() { Name = source.Name });
```

Configuration describes generated code and must not depend on a runtime
branch.

[All diagnostics](../diagnostics.md)
