# MORPH0043: Members returned no plan

## Cause

A reachable path through `Members` returns `null` or `default`. Morphant cannot
obtain a member plan from that path.

## Fix

Return a member initializer on every reachable path, even when it is empty, or
throw when the path must not continue:

```csharp
.Members(source => source.Skip
    ? new()
    : new() { Value = source.Value })
```

An omitted `Members` call and an empty `new()` are both valid.

[All diagnostics](../diagnostics.md)
