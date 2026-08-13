# MORPH0041: Required destination member is not initialized

## Cause

A C# `required` member is left uninitialized on at least one path that creates
a destination. This can result from explicit member selection, `Ignore()`, a
missing convention source, or a constructor that does not satisfy required
members.

## Fix

Set the member through the selected constructor or a valid `Members` rule, make
a matching convention source available, or use a constructor marked
`[SetsRequiredMembers]` when it initializes the member itself. The diagnostic
lists only the operations that still leave it uninitialized.

[All diagnostics](../diagnostics.md)
