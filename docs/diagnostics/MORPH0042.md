# MORPH0042: Member rule cannot be applied

## Cause

A member rule reaches an operation where it cannot run. For example, an
`init`-only member cannot be assigned after an existing destination has been
selected, and a creation-time rule cannot read `result` before that destination
exists.

## Fix

Use a settable member for paths that update an existing instance, avoid reading
`result` in a creation-time rule, or change `Resolve` so the affected path
creates a replacement that can receive the rule during initialization. You can
also restrict the mapping mode when the rule is valid for only Create or only
Update.

The diagnostic message identifies both the reason and affected operations.

[All diagnostics](../diagnostics.md)
