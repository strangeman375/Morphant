# MORPH0055: Polymorphic branch type is inaccessible

## Cause

A `ForDerived` source or destination cannot be named from the generated mapper,
for example because it is file-local or hidden by an inaccessible containing
type.

## Fix

Make the type accessible to the generated mapper, or remove the link. The
derived pair may live in another mapper or assembly, but its public contract
types must still be nameable.

[All diagnostics](../diagnostics.md)
