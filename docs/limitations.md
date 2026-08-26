# Current limitations

Morphant is in the 0.x series. It supports the object-mapping features listed
below, but not every feature expected from a general-purpose mapper.

## Included

- generated Create and Update mappings;
- convention and explicit constructor/member mapping;
- automatic name-based flattening of nested source properties and fields;
- opt-in convention lookup through selected nested source objects with
  `IncludeMembers`;
- manual mappings with `Convert`;
- explicit nested mapping;
- runtime lookup through DI and `IMapper`;
- explicit pair-local runtime polymorphism for class, interface and compatible
  value-type branches;
- null, member, constructor and mapping-mode settings;
- mapper and mapping-configuration inheritance;
- compile-time diagnostics and typed runtime exceptions;
- nullable, value, record, interface, abstract and closed generic destination
  types when Morphant can create them by convention or an explicit rule;
- first-class named and explicitly configured unnamed `ValueTuple` and
  `System.Tuple` mappings, including long and nullable tuple forms;
- C# 9 and newer consumers running Roslyn 4.4.0 or later.

## Not included

- automatic collection, dictionary or buffer element mapping;
- projection to `IQueryable`;
- unflattening a flat source into newly created nested destination objects;
- distinguishing missing, null and default values for patch/merge mappings;
- automatic immutable Update reconstruction;
- first-class multi-source mappings or per-call user data; combine values into
  one source record or named tuple and pass state to nested mappings
  explicitly;
- keyed mappings or discriminator-based dispatch;
- preserving shared object references or mapping cycles;
- cross-assembly configuration inheritance;
- generated DI registration;
- configurable enum mapping, reverse mapping, before/after hooks or async
  mapping.

Morphant does not guess behavior for unsupported cases. A synchronous special
case can still be implemented with `Convert`, including mapping a collection
as a whole with custom code. Features that require runtime reflection to
discover mappings are outside the roadmap.
