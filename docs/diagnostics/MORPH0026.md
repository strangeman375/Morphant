# MORPH0026: Included mapping pair not found

## Cause

`IncludeBase<TSource, TDestination>()` names a mapping that Morphant cannot
find in the current mapper or its connected base configuration.

## Fix

Check the type arguments and declare the requested pair. When the pair belongs
to a base mapper, call `base.Configure(builder)` so that configuration is
connected before including it. Declaration order does not matter.

See [Configuration inheritance](../configuration-inheritance.md).

[All diagnostics](../diagnostics.md)
