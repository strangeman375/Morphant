# MORPH0014: Mappings may become identical

## Cause

Two registrations in a generic mapper can produce the same
`ITypeMapper<TSource, TDestination>` for some type arguments.

For example, `Map<T, T>()` and `Map<string, string>()` overlap when `T` is
`string`.

## Fix

Remove one overlapping registration or move it to another mapper. Every
possible construction of a generic mapper must produce unique
source/destination pairs.

[All diagnostics](../diagnostics.md)
