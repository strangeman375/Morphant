# MORPH0033: Invalid mapping method use

## Cause

A configuration-only method such as `Auto`, `Ignore`, `Value`, `Map`, `Create`,
or `Update` is used outside a supported constructor argument, member rule, or
standalone read-only member update.

## Fix

Use these methods only in the positions shown in
[Declarative mapping](../declarative-mapping.md) and
[Nested mapping](../nested-mapping.md). Do not call them from `Convert`, save
their marker values for later runtime use, or wrap them in unrelated runtime
operations.

[All diagnostics](../diagnostics.md)
