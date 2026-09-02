# MORPH0056: Tuple presentation is conflicting

## Cause

Two registrations in the same compilation have the same underlying source and
destination types but present a tuple differently on at least one side. A
presentation includes recursive element names, nullable annotations and the
choice between `dynamic` and `object`.

C# erases these distinctions from runtime type identity, while they still
affect Morphant configuration, conventions and callback typing.

## Fix

Use one consistent source and destination tuple presentation for that mapping
pair. If the same underlying pair needs different meanings or nullable
contracts, introduce distinct wrapper types.

[All diagnostics](../diagnostics.md)
