# MORPH0030: Mapping expression is unavailable

## Cause

A mapping callback references code that will not be available from the
generated mapper. This applies to `Construct`, `Resolve`, `Members`,
`ConstructUsing`, `ResolveUsing`, and `Convert`. It can also occur when an
expression violates the input or nullable contract where the mapping uses it,
including a `Members` value passed to a constructor.

## Fix

Use constants or accessible mapper or static members. Do not capture
`Configure` locals or local functions, and do not reference inaccessible or
file-local symbols. The end of the diagnostic identifies the unavailable
reference or incompatible expression. For nullable values, provide a value
that satisfies the receiving parameter or member, for example with `??`.

See [Callback forms](../api/README.md#callback-forms).

[All diagnostics](../diagnostics.md)
