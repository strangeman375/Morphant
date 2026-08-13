# MORPH0034: Mapper member conflicts with generated Supports

## Cause

The mapper declares a member with the signature
`Supports(System.Type, System.Type)`, which Morphant also generates for runtime
mapping lookup.

## Fix

Remove or rename the conflicting member. Do not implement `Supports` manually
on a generated mapper.

[All diagnostics](../diagnostics.md)
