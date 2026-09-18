# Current limitations

Morphant generates synchronous object mappings without runtime reflection.
For supported features and examples, see the [mapping guides](README.md#mapping-guides).

## Not included

- automatic collection, dictionary or buffer element mapping;
- projection to `IQueryable`;
- unflattening a flat source into newly created nested destination objects;
- distinguishing missing, null and default values for patch/merge mappings;
- automatic immutable Update reconstruction;
- keyed mappings or discriminator-based dispatch;
- preserving shared object references or mapping cycles;
- cross-assembly configuration inheritance;
- generated DI registration;
- configurable enum mapping, reverse mapping, before/after hooks or async
  mapping.

Morphant does not guess behavior for unsupported cases. A synchronous special
case can still be implemented with `Convert`, including mapping a collection
as a whole with custom code. Features that require runtime reflection are
outside the roadmap.
