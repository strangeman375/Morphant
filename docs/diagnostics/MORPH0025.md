# MORPH0025: Duplicate IncludeBase call

## Cause

The same mapping includes the same base source/destination pair more than once.
The imported settings and member rules would otherwise be applied repeatedly.

## Fix

Keep one `IncludeBase<TSource, TDestination>()` call for that pair. If different
base mappings are required, include each distinct pair once.

See [Configuration inheritance](../configuration-inheritance.md).

[All diagnostics](../diagnostics.md)
