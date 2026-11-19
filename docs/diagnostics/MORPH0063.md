# MORPH0063: Incompatible Morphant library

## Cause

A referenced library was built with a Morphant version that is incompatible
with the version used by this project. The application loads one Morphant
runtime, so the library would run with this project's version and can fail
with `TypeLoadException` or `MissingMethodException`. See
[Version compatibility](../compatibility.md) for the versions that work
together.

Morphant reads the version from the library's reference to the Morphant
runtime, so the diagnostic also covers libraries that use Morphant without
declaring mappers.

## Fix

Rebuild the library with the Morphant version used by this project. For a
library you do not maintain, use a release of it built with a compatible
Morphant version, or keep this project on the library's Morphant version.

[All diagnostics](../diagnostics.md)
