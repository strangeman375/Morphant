# MORPH0004: Incompatible Morphant runtime

## Cause

The referenced runtime does not have the contract required by the loaded
source generator. The diagnostic message includes the detected mismatch.

## Fix

Use matching Morphant runtime and generator versions. Prefer referencing only
the `Morphant` package instead of pinning its generator separately. Remove
stale analyzer references and rebuild after changing package versions.

[All diagnostics](../diagnostics.md)
