# MORPH0030: Mapping expression is unavailable

## Cause

A mapping callback references code that will not be available from the
generated mapper. This applies to `Construct`, `Resolve`, `Members`,
`ConstructUsing`, `ResolveUsing`, and `Convert`.

## Fix

Use constants or accessible mapper or static members. Do not capture
`Configure` locals or local functions, and do not reference inaccessible or
file-local symbols. The end of the diagnostic identifies the unavailable
reference.

See [Declarative mapping](../declarative-mapping.md).

[All diagnostics](../diagnostics.md)
