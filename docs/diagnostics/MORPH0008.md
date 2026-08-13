# MORPH0008: File-local mapper declaration is not supported

## Cause

The mapper or one of its containing types uses the `file` modifier. Generated
source files cannot extend or name file-local types.

## Fix

Remove the `file` modifier and give the type ordinary accessibility, or move
the mapper outside the file-local container.

[All diagnostics](../diagnostics.md)
