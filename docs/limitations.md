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
- polymorphic branch relationships that depend on unknown generic arguments
  ([MORPH0061](diagnostics/MORPH0061.md));
- `private`, `protected`, or `private protected` nested mapper declarations;
  a mapper and its containing types must be accessible to generated
  namespace-level code ([`MORPH0059`](diagnostics/MORPH0059.md));
- preserving shared object references or mapping cycles;
- cross-assembly configuration inheritance;
- mapping-contract types or required generic constraints that are available
  only through a non-global `extern alias` or have an ambiguous `global::`
  name, including a namespace/type path collision; the referenced assembly
  must also be available unambiguously through `global` for generated code;
- generated DI registration;
- configurable enum mapping, reverse mapping, before/after hooks or async
  mapping.

Morphant does not guess behavior for unsupported cases. A synchronous special
case can still be implemented with `Convert`, including mapping a collection
as a whole with custom code. Features that require runtime reflection are
outside the roadmap.
