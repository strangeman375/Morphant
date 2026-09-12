# MORPH0038: Previous destination is unavailable

## Cause

`Resolve` reads or returns the value of `previous` on a reachable path where no existing
destination is available. This includes Create and an Update that is allowed to
create a replacement for a null destination.

## Fix

Check `previous.HasValue` or `TryGetValue` before using its value and provide a
a construction expression or throw on the empty path. Put the check before
any local initializer that reads `previous.Value`. Alternatively, restrict the mapping
to operations where a destination is guaranteed and use
`NullDestinationHandling.Throw` for null Update destinations.

See [Declarative mapping](../declarative-mapping.md).

[All diagnostics](../diagnostics.md)
