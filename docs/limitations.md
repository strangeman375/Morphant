# Current limitations

Morphant 0.1 is a core v0 preview. It supports the object-mapping features
listed below, but not every feature expected from a general-purpose mapper.

## Included

- generated Create and Update mappings;
- convention and explicit constructor/member mapping;
- manual mappings with `Convert`;
- explicit nested mapping;
- runtime lookup through DI and `IMapper`;
- null, member, constructor and mapping-mode settings;
- mapper and mapping-configuration inheritance;
- compile-time diagnostics and typed runtime exceptions;
- nullable, value, record, interface, abstract and closed generic destination
  types when Morphant can create them by convention or an explicit rule;
- C# 9 and newer consumers.

## Not included in core v0

- automatic collection, dictionary or buffer element mapping;
- projection to `IQueryable`;
- convention flattening or `IncludeMembers`;
- distinguishing missing, null and default values for patch/merge mappings;
- automatic immutable Update reconstruction;
- keyed mappings or selecting derived mappings from runtime types;
- preserving shared object references or mapping cycles;
- open-generic mappings or lookup by `System.Type`;
- cross-assembly configuration inheritance;
- automatic DI registration or assembly scanning;
- configurable enum mapping, reverse mapping, before/after hooks or async
  mapping.

Morphant does not guess behavior for unsupported cases. A synchronous special
case can still be implemented with `Convert`, including mapping a collection
as a whole with custom code.
