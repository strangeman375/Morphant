# MORPH0033: Invalid declarative API use

## Cause

A declarative API such as `Auto`, `Ignore`, `Value`, `Map`, `Create`, `Update`,
or `context` is used in an unsupported position within a mapping callback.

## Fix

Use these methods only in the positions shown in
[Declarative mapping](../declarative-mapping.md) and
[Nested mapping](../nested-mapping.md). Do not call them from `Convert`, save
their marker values for later runtime use, or wrap them in unrelated runtime
operations.

[All diagnostics](../diagnostics.md)
