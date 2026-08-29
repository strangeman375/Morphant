# MORPH0054: Polymorphic branch type is incompatible

## Cause

A `ForDerived` source is not assignable to the base source, or its destination
is not assignable to the base destination. The C# generic-constraint error may
be reported alongside this diagnostic.

## Fix

Use an assignable source and destination pair, or move the link to a base
mapping with compatible types.

[All diagnostics](../diagnostics.md)
