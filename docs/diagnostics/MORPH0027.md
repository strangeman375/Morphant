# MORPH0027: Included mapping type is incompatible

## Cause

The current mapping cannot safely reuse the included mapping. The current
source must be assignable to the included source, and the current destination
must be assignable to the included destination.

## Fix

Choose an `IncludeBase` pair that represents actual base source and destination
types, or correct the type inheritance. User-defined conversions do not replace
the required assignability relationship.

See [Configuration inheritance](../configuration-inheritance.md).

[All diagnostics](../diagnostics.md)
