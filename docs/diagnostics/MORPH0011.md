# MORPH0011: Mapping type is inaccessible

## Cause

The registered source or destination type cannot be named from generated
mapper code. The inaccessible part may be the type itself, a containing type,
one of its generic type arguments, or a required generic constraint. A type
that is available only through a non-global `extern alias` also cannot be
named by the generated mapper.

## Fix

Use a source and destination type whose complete type declaration is accessible
to the mapper. Change private, protected, or file-local types as needed, or
move the mapper and mapping types to a location where the required access is
available. For an aliased reference, also expose the assembly through the
`global` alias, or use globally nameable mapping contract types.

[All diagnostics](../diagnostics.md)
