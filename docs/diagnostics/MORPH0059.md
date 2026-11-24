# MORPH0059: Mapper type is inaccessible to generated code

> **Removed in Morphant 0.6.0.** Generated configuration methods no longer
> refer to the mapper, so a mapper and its containing types can be `private`
> or `protected`.

## In Morphant 0.5

A mapper marked with `[MorphantMapper]`, or one of its containing types, was
not accessible from namespace-level generated code. See the
[0.5.0 help](https://github.com/strangeman375/Morphant/blob/v0.5.0/docs/diagnostics/MORPH0059.md).

## After upgrading

No change is required. Containing types of a nested mapper must still be
`partial`.

[All diagnostics](../diagnostics.md)
