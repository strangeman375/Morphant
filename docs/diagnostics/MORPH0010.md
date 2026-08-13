# MORPH0010: Mapping may conflict with a declared interface

## Cause

In a generic mapper, a declared `ITypeMapper<,>` interface and a generated
mapping can become the same closed interface for some type arguments.

For example, `ITypeMapper<T, T>` can conflict with a generated
`ITypeMapper<string, string>` when `T` is `string`.

## Fix

Remove one of the overlapping contracts or split them into different mapper
types. Make sure no supported construction of the generic mapper can make the
declared and generated source/destination pairs identical.

[All diagnostics](../diagnostics.md)
