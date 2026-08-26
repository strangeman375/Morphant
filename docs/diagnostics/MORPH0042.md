# MORPH0042: Member rule cannot be applied

## Cause

A member rule reaches an operation where it cannot run. For example, an
`init`-only member cannot be assigned after an existing destination has been
selected or after `ConstructUsing`/`ResolveUsing` has returned an already
initialized result. A creation-time rule also cannot read `result` before that
destination exists. The same restriction applies to a scalar rule for a
read-only `System.Tuple` element after a runtime factory returns.

## Fix

Use a settable member for paths that update an existing instance, avoid reading
`result` in a creation-time rule, or change `Resolve` so the affected path
creates a replacement that can receive the rule during initialization. You can
also ensure the member is initialized in the result returned by
`ConstructUsing`/`ResolveUsing`, or restrict the mapping mode when the rule is
valid for only Create or only Update.

Morphant does not reconstruct or replace a result returned by a runtime
callback. Eligible nested `Update` statements remain valid because they
operate on the referenced object rather than assigning the creation-only
member.

The diagnostic message identifies both the reason and affected operations.

[All diagnostics](../diagnostics.md)
