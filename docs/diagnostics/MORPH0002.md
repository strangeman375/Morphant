# MORPH0002: Morphant runtime not found

## Cause

The source generator is loaded, but the project does not reference the
compatible Morphant runtime types used by generated mappers.

## Fix

Reference the `Morphant` package as shown in the
[quick start](../quick-start.md). The package contains both the runtime and the
source generator. If they are referenced separately, remove the standalone
generator reference or ensure the matching runtime is also referenced.

[All diagnostics](../diagnostics.md)
