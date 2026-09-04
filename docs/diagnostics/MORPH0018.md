# MORPH0018: Mapping configuration cannot be analyzed

## Cause

Morphant found a `Map` registration but cannot determine one fixed chain of
settings and mapping rules for it. This can happen when the mapping builder is
stored, passed elsewhere, or configured conditionally.

Morphant also reports this diagnostic when a callback call such as `Convert`
binds to a user-defined or otherwise competing extension method instead of the
generated Morphant method. Such a call cannot be transferred safely into the
mapper implementation.

## Fix

Keep the mapping configuration on the fluent chain that starts with `Map`:

```csharp
builder.Map<Source, Destination>()
    .Construct(source => new(source.Id))
    .Members((source, _) => new() { Name = source.Name });
```

Configuration describes generated code and must not depend on a runtime
branch.

If the diagnostic points to a fluent callback method, remove or rename the
competing extension overload, or call it outside the Morphant configuration
chain.

[All diagnostics](../diagnostics.md)
