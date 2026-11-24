# MORPH0058: Mapper self type is invalid

> **Removed in Morphant 0.6.0.** Mappers derive from the non-generic
> `TypeMapper`, so Morphant no longer checks a self type.

## In Morphant 0.5

A mapper or reusable configuration base closed `TypeMapper<TMapper>` with a
type other than its final mapper. See the
[0.5.0 help](https://github.com/strangeman375/Morphant/blob/v0.5.0/docs/diagnostics/MORPH0058.md).

## After upgrading

Derive the mapper from `TypeMapper`, and remove the self-type parameter and
its recursive constraint from reusable bases. See
[Migrating from 0.5](../../CHANGELOG.md#migrating-from-05).

[All diagnostics](../diagnostics.md)
