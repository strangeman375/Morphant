# MORPH0063: Incompatible Morphant library

## Cause

A referenced DLL records a dependency on a Morphant version incompatible
with this project's runtime. This also applies to libraries that use Morphant
without declaring mappers. A call that needs a missing type or method can fail
at run time. See [Version compatibility](../compatibility.md).

## Fix

Rebuild the library with the Morphant version used by this project. For a
library you do not maintain, use a release of it built with a compatible
Morphant version, or keep this project on the library's Morphant version.

[All diagnostics](../diagnostics.md)
