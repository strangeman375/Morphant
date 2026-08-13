# MORPH0011: Mapping type is inaccessible

## Cause

The registered source or destination type cannot be named from generated
mapper code. The inaccessible part may be the type itself, a containing type,
or one of its generic type arguments.

## Fix

Use a source and destination type whose complete type declaration is accessible
to the mapper. Change private, protected, or file-local types as needed, or
move the mapper and mapping types to a location where the required access is
available.

[All diagnostics](../diagnostics.md)
