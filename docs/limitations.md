# Current limitations

Morphant 0.1 is a core v0 preview. It provides the mapping lifecycle and
configuration model, but not every feature expected from a general-purpose
mapper.

## Included

- generated Create and Update mappings;
- convention and explicit constructor/member mapping;
- manual mappings with `Convert`;
- explicit nested mapping;
- exact-pair runtime dispatch through DI;
- null, member, constructor and mapping-mode settings;
- mapper and mapping-pair inheritance;
- compile-time diagnostics and typed runtime exceptions;
- nullable, value, record, interface, abstract and constructed generic
  destinations within their documented support boundaries;
- C# 9 and newer consumers.

## Not included in core v0

- automatic collection, dictionary or buffer element mapping;
- projection to `IQueryable`;
- convention flattening or `IncludeMembers`;
- patch/merge presence policies;
- automatic immutable Update reconstruction;
- keyed mappings or runtime polymorphic dispatch;
- reference tracking, shared identity or cycle handling;
- open-generic and runtime-type lookup;
- cross-assembly configuration inheritance;
- automatic DI registration or assembly scanning;
- enum policies, reverse mapping, hooks or async mapping.

These cases do not trigger a hidden fallback algorithm. A synchronous special
case can still be implemented explicitly with `Convert`, including a
collection treated as one opaque value.
