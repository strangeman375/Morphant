# MORPH0028: Inherited mapping expression is inaccessible

## Cause

An expression imported from a base mapping references a member that the
current mapper cannot access, such as a private helper on the base mapper.

## Fix

Make the referenced helper accessible to the derived mapper, for example
`protected` or `internal`, or replace the inherited rule with a local rule that
uses accessible members.

See [Configuration inheritance](../configuration-inheritance.md).

[All diagnostics](../diagnostics.md)
