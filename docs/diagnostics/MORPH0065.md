# MORPH0065: Mapping extensions are already visible from a friend assembly

## Cause

A referenced assembly grants this assembly `InternalsVisibleTo` access and
supplies mapping configuration methods for the same pair.

## Fix

Configure the pair in one assembly, remove unneeded friend access, or use a
distinct DTO. When several referenced assemblies supply the same methods,
the compiler can also report `CS0121` on the configuration call.

[All diagnostics](../diagnostics.md)
