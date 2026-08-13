# MORPH0038: Previous destination is unavailable

## Cause

`Construct` or `Resolve` uses `previous` on a reachable path where no existing
destination is available. This includes Create and an Update that is allowed to
create a replacement for a null destination.

## Fix

Check `previous.HasValue` or `TryGetValue` before using its value and provide a
new destination or throw on the empty path. Alternatively, restrict the mapping
to operations where a destination is guaranteed and use
`NullDestinationHandling.Throw` for null Update destinations.

See [Declarative mapping](../declarative-mapping.md).

[All diagnostics](../diagnostics.md)
