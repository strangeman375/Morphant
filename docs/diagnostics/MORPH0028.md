# MORPH0028: Inherited mapping expression is inaccessible

## Cause

An inherited callback references a member that is inaccessible or whose
original binding cannot be preserved in the derived mapper. This includes
private helpers, explicit `base` access, and protected virtual members hidden
by a derived declaration.

## Fix

Make a private helper accessible, for example `protected` or `internal`.
For a hiding conflict, rename the hiding member or move the call into an
accessible helper with a distinct name on the declaring base mapper.
Alternatively, replace the inherited rule with a local rule.

See [Configuration inheritance](../configuration-inheritance.md).

[All diagnostics](../diagnostics.md)
