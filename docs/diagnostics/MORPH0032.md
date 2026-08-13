# MORPH0032: Destination input is read-only

## Cause

An inline mapping lambda modifies `previous`, `result`, or a local alias that
refers to either value. These inputs describe destination state but are not
mutable working variables.

## Fix

Return the desired destination or member values instead of modifying these
inputs. Use `Convert`, `ConstructUsing`, or `ResolveUsing` when the mapping
requires ordinary mutation.

See [Declarative mapping](../declarative-mapping.md).

[All diagnostics](../diagnostics.md)
