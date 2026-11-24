# MORPH0065: Mapping extensions are already visible from a friend assembly

## Cause

A `Map` registration can see configuration methods for the same pair from a
referenced assembly that grants `InternalsVisibleTo` access.

## Fix

Configure the pair in one assembly, remove unneeded friend access, or use a
distinct DTO. Check which namespaces and static classes are in scope at the
registration, including imports through `extern alias`. If another mapper
needs a local extension for the same pair, the compiler can also report `CS0121` at the
importing call. The same ambiguity can arise between several friend assemblies.

[All diagnostics](../diagnostics.md)
