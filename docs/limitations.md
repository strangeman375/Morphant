# Current limitations

Morphant is in the 0.x series. It supports the object-mapping features listed
below, but not every feature expected from a general-purpose mapper.

## Included

- generated Create and Update mappings;
- convention and explicit constructor/member mapping;
- opt-in convention lookup through selected nested source objects with
  `IncludeMembers`;
- manual mappings with `Convert`;
- explicit nested mapping;
- runtime lookup through DI and `IMapper`;
- null, member, constructor and mapping-mode settings;
- mapper and mapping-configuration inheritance;
- compile-time diagnostics and typed runtime exceptions;
- nullable, value, record, interface, abstract and closed generic destination
  types when Morphant can create them by convention or an explicit rule;
- C# 9 and newer consumers running Roslyn 4.4.0 or later.

## Not included

- automatic collection, dictionary or buffer element mapping;
- projection to `IQueryable`;
- automatic name-based flattening, for example supplying `tenantId` or
  `TenantId` from `source.Tenant.Id` without an explicit `IncludeMembers` or
  `Members` rule;
- distinguishing missing, null and default values for patch/merge mappings;
- automatic immutable Update reconstruction;
- first-class multi-source mappings or per-call user data; combine values into
  one source type, such as a tuple, handle it with `Convert`, and pass state to
  nested mappings explicitly;
- keyed mappings or generated dispatch for explicitly configured derived
  types;
- preserving shared object references or mapping cycles;
- cross-assembly configuration inheritance;
- generated DI registration;
- configurable enum mapping, reverse mapping, before/after hooks or async
  mapping.

Morphant does not guess behavior for unsupported cases. A synchronous special
case can still be implemented with `Convert`, including mapping a collection
as a whole with custom code. Features that require runtime reflection to
discover mappings are outside the roadmap.
