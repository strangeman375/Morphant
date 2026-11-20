# MORPH0065: Mapping extensions are already visible from a friend assembly

## Cause

A referenced assembly grants this assembly `InternalsVisibleTo` access and
supplies mapping configuration methods for the same pair.

## Fix

Configure the pair in one assembly, remove unneeded friend access, or use a
distinct DTO. An extension imported through `extern alias` is visible where
its namespace or static class is imported. If another mapper needs a local
extension for the same pair, the compiler can also report `CS0121` at the
importing call. The same ambiguity can arise between several friend assemblies.

[All diagnostics](../diagnostics.md)
