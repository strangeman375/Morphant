# MORPH0030: Mapping expression is unavailable

## Cause

An inline `Construct`, `Resolve`, or `Members` lambda references code that will
not be available from the generated mapper. Typical examples are locals from
`Configure`, inaccessible members, and file-local helper types.

## Fix

Use constants, static members, or mapper members that are accessible from the
generated mapper. Move runtime-only state into an ordinary callback with
`ConstructUsing`, `ResolveUsing`, or `Convert` when it cannot be expressed that
way. The end of the diagnostic message identifies the unavailable reference.

See [Declarative mapping](../declarative-mapping.md).

[All diagnostics](../diagnostics.md)
