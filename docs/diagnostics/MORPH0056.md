# MORPH0056: Mapping pair presentations conflict

## Cause

Two registrations in one assembly use the same CLR types with different tuple
element names, nested nullable annotations, or uses of `dynamic` and `object`.
These differences affect the callbacks and member names available for that pair.

## Fix

Use matching names and annotations throughout the assembly. For example,
register `(int Id, string Name)` with those names in each mapper, or introduce
a separate DTO for a different meaning. Top-level reference annotations can
differ. See [result nullability](../settings/null-handling.md#result-nullability).

[All diagnostics](../diagnostics.md)
