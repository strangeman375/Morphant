# MORPH0049: IncludeMembers selector is invalid

## Cause

An `IncludeMembers` selection is not an inline property or field path rooted
in the mapping source. The diagnostic is also reported for an unreadable path,
a selected type with no readable members, or the same included path configured
twice.

## Fix

Pass one unique inline path, or an anonymous object containing several unique
paths:

```csharp
.IncludeMembers(source => source.Customer)
.IncludeMembers(source => new
{
    Audit = source.Envelope?.Audit,
    source.Metadata
})
```

Move computed values, method calls and indexed access into an explicit
`Members` rule instead.

See [Include nested source members](../include-members.md).

[All diagnostics](../diagnostics.md)
