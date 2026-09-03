# MORPH0059: Mapper type is inaccessible to generated code

## Cause

A type marked with `[MorphantMapper]`, or one of its containing types, is not
accessible from namespace-level generated code in the same assembly. This
includes `private`, `protected`, and `private protected` declarations.

## Fix

Make the reported type `public`, `internal`, or `protected internal`, or move
the mapper out of the inaccessible containing type. Every type in the nesting
chain must satisfy the requirement.

[All diagnostics](../diagnostics.md)
