# MORPH0028: Inherited mapping expression is inaccessible

## Cause

An inherited callback references a member that is inaccessible or whose
original binding cannot be preserved in the derived mapper. This includes
private helpers, explicit `base` access, and protected virtual members hidden
by a derived declaration. It also includes a constrained interface access
whose concrete struct implementation would require boxing instead of accessing
the original receiver.

## Fix

Make a private helper accessible, for example `protected` or `internal`.
For a hiding conflict, rename the hiding member or move the call into an
accessible helper with a distinct name on the declaring base mapper.
For constrained struct access, put the access in an accessible generic helper
that preserves the receiver semantics.
Alternatively, replace the inherited rule with a local rule.

See [Configuration inheritance](../configuration-inheritance.md).

[All diagnostics](../diagnostics.md)
