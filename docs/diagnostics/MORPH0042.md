# MORPH0042: Member rule cannot be applied

## Cause

A member rule reaches an operation where it cannot run. For example, an
`init`-only member cannot be assigned after an existing destination has been
selected or after `ConstructUsing`/`ResolveUsing` has returned an already
initialized result. A creation-time rule also cannot read `result` before that
destination exists. The same restriction applies to a scalar rule for a
read-only `System.Tuple` element after a runtime factory returns.

A writable member also needs its value before construction when it corresponds
to an ordinary destination constructor parameter. A reachable `result` access
in that value, a local dependency, or its selecting condition produces this
diagnostic. The message names the corresponding parameter when applicable.

## Fix

Use a settable member for paths that update an existing instance, avoid reading
`result` in a creation-time rule, or change `Resolve` so the affected path
creates a replacement that can receive the rule during initialization. You can
also ensure the member is initialized in the result returned by
`ConstructUsing`/`ResolveUsing`, or restrict the mapping mode when the rule is
valid for only Create or only Update.

For constructor values, guard `result` with conditions that exclude every
creation path. `Operation == Update` alone does not exclude `Update(null)` or
a replacement selected by `Resolve`. See
[reading `result`](../api/members.md#reading-result) for valid examples.

Morphant does not reconstruct or replace a result returned by a runtime
callback. Eligible nested `Update` statements remain valid because they
operate on the referenced object rather than assigning the creation-only
member.

The diagnostic message identifies both the reason and affected operations.

[All diagnostics](../diagnostics.md)
