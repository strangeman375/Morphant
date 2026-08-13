# MORPH0003: Multiple Morphant runtimes found

## Cause

The compilation contains more than one assembly that provides the Morphant
runtime contract. The generator cannot safely choose between them.

## Fix

Keep exactly one Morphant runtime reference. Remove duplicate package or
project references and any locally declared copies of Morphant runtime types.
Also check transitive dependencies if the duplicate is not referenced
directly.

[All diagnostics](../diagnostics.md)
