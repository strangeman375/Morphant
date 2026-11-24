# MORPH0060: Mapper family parameter is absent from mapping

> **Removed in Morphant 0.6.0.** Mappers in an assembly share configuration
> methods for each mapping pair, so a mapper's type parameters need not occur
> in its pairs.

## In Morphant 0.5

A generic parameter of a reusable mapper family, other than its self-type
parameter, did not occur in the source or destination type of a declared
pair. See the
[0.5.0 help](https://github.com/strangeman375/Morphant/blob/v0.5.0/docs/diagnostics/MORPH0060.md).

## After upgrading

No change is required.

[All diagnostics](../diagnostics.md)
