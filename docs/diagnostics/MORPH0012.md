# MORPH0012: Unsupported mapping type

## Cause

The top-level source or destination passed to `Map` is a type parameter.
Morphant needs a named root type to generate a stable mapping contract.

```csharp
builder.Map<T, Destination>(); // MORPH0012
```

## Fix

Register a concrete source/destination pair, or place the type parameter inside
a named generic type:

```csharp
builder.Map<Envelope<T>, Destination>();
```

[All diagnostics](../diagnostics.md)
