# MORPH0017: Configure cannot be analyzed

## Cause

Morphant cannot determine a fixed sequence of mapper-level configuration calls.
Typical causes are aliasing or passing the `MapperBuilder`, or placing mapping
registrations inside conditions, loops, `switch`, or `try` statements.

## Fix

Call mapper settings and `Map` directly from `Configure` in an unconditional
sequence. Do not store, return, or pass the builder to another method.

```csharp
protected override void Configure(MapperBuilder builder)
{
    builder.MemberSelection(MemberSelection.Auto);
    builder.Map<Source, Destination>();
}
```

[All diagnostics](../diagnostics.md)
