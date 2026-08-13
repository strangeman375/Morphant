# MORPH0009: Mapping is already implemented

## Cause

The mapper both declares an `ITypeMapper<TSource, TDestination>` implementation
and registers the same source/destination pair with `Map`. Morphant would have
to generate a contract that the type already implements.

## Fix

Choose one implementation. Remove the explicit interface from the mapper when
Morphant should generate it, or remove the matching `Map` registration when
the interface is implemented manually.

[All diagnostics](../diagnostics.md)
