# MORPH0042: Member rule cannot be applied

## Cause

A member rule reaches an operation where it cannot run. An explicit value for
an `init`-only member cannot be assigned after `ConstructUsing`/`ResolveUsing`
returns an already constructed result. The same restriction applies to a
scalar rule for a read-only `System.Tuple` element after a runtime factory.

Ordinary Update that reuses a destination skips its `init`-only assignments;
those rules alone do not produce this diagnostic.

A value required during construction cannot read `result` before the
destination exists. This occurs when an automatic constructor argument depends
on a member value or condition that itself needs `result`, or when an
initializer needs the not-yet-created destination. The diagnostic identifies
the affected rule and operations.

## Fix

Initialize creation-only members inside the runtime factory and remove their
assignments from `Members`. Use a settable member if it must change afterward.

Supply an independent constructor value from `source` or an available
`previous` destination, then read `result` in a writable member rule.
Alternatively, guard `result` access with conditions that exclude creation.
`Operation == Update` alone does not exclude `Update(null)` or
a replacement selected by `Resolve`. See
[reading `result`](../api/members.md#reading-result) for valid examples.

See [factory result rules](../api/construct-using.md#factory-result) for the
member operations allowed after a runtime callback.

[All diagnostics](../diagnostics.md)
