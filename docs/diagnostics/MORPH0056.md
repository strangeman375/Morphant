# MORPH0056: Tuple presentation is conflicting

## Cause

Two registrations in the same compilation have the same underlying source and
destination types but use different tuple element names on at least one side.
C# treats tuple types that differ only by element names as the same runtime
type, but the names remain available to Morphant configuration and name-based
conventions.

## Fix

Use one consistent set of source and destination tuple element names for that
mapping pair. If the same underlying pair needs different meanings, introduce
distinct wrapper types.

[All diagnostics](../diagnostics.md)
