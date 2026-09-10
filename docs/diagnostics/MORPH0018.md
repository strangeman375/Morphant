# MORPH0018: Mapping configuration cannot be analyzed

## Cause

Morphant found a `Map` registration but cannot determine one fixed chain of
settings and mapping rules for it. This can happen when the mapping builder is
stored, passed elsewhere, or configured conditionally.

It also occurs when a callback such as `Convert` matches a competing extension
method or a base mapper's overload instead of the declared mapping.

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
chain. In a reusable mapper family, also check that the callback uses the types
and tuple element names declared by its own `Map` registration.

[All diagnostics](../diagnostics.md)
