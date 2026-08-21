# MORPH0053: Polymorphic source branch is duplicated

## Cause

The same effective runtime source type appears in more than one `ForDerived`
call on one mapping pair. Nullable annotations do not create distinct runtime
types.

## Fix

Keep one link for that source type. Branch priority cannot be expressed by
declaration order; use one separately registered destination pair.

[All diagnostics](../diagnostics.md)
