# MORPH0039: Construct or Resolve returned no destination

## Cause

A reachable `Construct` or `Resolve` path returns `null` or `default` when the
operation requires an actual destination to continue. The message lists the
affected Create or Update cases.

## Fix

Return a destination on every affected path, reuse a non-empty `previous`, or
throw when mapping cannot continue. Do not use `null` or `default` as a
placeholder for an unhandled branch.

See [Declarative mapping](../declarative-mapping.md).

[All diagnostics](../diagnostics.md)
