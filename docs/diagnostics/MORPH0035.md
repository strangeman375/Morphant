# MORPH0035: Destination construction is not configured

## Cause

A reachable operation needs a new destination, but the mapping has neither a
usable convention constructor nor an explicit creation rule. The message lists
the affected Create or Update cases.

## Fix

Provide a usable constructor, configure `Construct`, `ConstructUsing`,
`Resolve`, or `ResolveUsing`, or use `Convert` for a manual mapping. For an
Update-only mapping that must never replace a null destination, set
`NullDestinationHandling.Throw` instead of `Create`.

See [Create and Update](../create-and-update.md) and
[Constructor selection](../settings/constructor-selection.md).

[All diagnostics](../diagnostics.md)
