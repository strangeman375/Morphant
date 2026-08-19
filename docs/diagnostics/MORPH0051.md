# MORPH0051: Flattened source path is ambiguous

## Cause

Two or more compatible nested source paths produce the same destination or
constructor-parameter name. Morphant does not choose a path by declaration
order.

## Fix

Configure the target with an explicit `Members` or constructor rule, rename a
source member, or disable flattening for the mapping. If
`MemberSelection.Explicit` is already in use, remove `Auto()` for that target
and provide its value explicitly.

See [Flatten nested source members](../flattening.md).

[All diagnostics](../diagnostics.md)
