# Conventions

A bare mapping uses conventions for destination construction and members:

```csharp
builder.Map<Customer, CustomerDto>();
```

Use explicit rules when a convention does not describe the intended mapping.
Morphant does not normalize names or start nested mappings automatically.
When no direct source member matches, it can
[`flatten` a nested source path](flattening.md) by joining member names.

Use [`IncludeMembers`](include-members.md) to opt a selected nested source
object into the same constructor and member conventions.

## Members

A destination property or field is mapped when:

- the source has one readable instance property or field with the exact,
  case-sensitive name;
- the destination member can be assigned during the current operation;
- C# provides an implicit conversion without a compiler warning.

Inherited members are included. Static members, indexers and inaccessible
members are ignored.

Create can initialize `init` properties, required members and mutable fields.
Update changes settable properties and mutable fields; creation-only members
keep their existing values.

Use [`MemberSelection.Explicit`](settings/member-selection.md) to disable
automatic member mapping. `Auto()` enables the convention for one explicitly
listed member.

## Constructors

Create selects a constructor according to
[`ConstructorSelection`](settings/constructor-selection.md). Constructor
parameters are matched to readable source members by exact name first, then by
a unique case-insensitive name. Nested paths participate through auto
flattening. Optional parameters may keep their defaults.

Only accessible constructors with ordinary by-value parameters participate.
If no constructor can be selected unambiguously, choose an explicit
[destination method](api/README.md) or [`Convert`](api/convert.md).

## Tuples

Tuple elements map by semantic name, never by position. Fully or partially
unnamed elements need explicit rules through their technical `ItemN` names.
Named tuple members use the same exact, case-sensitive member convention as
other destinations; logical tuple constructor parameters use the normal
constructor matching rules.

Tuple construction is intrinsic and does not use `ConstructorSelection`.
Long tuple plans remain flat and do not expose the BCL `Rest` representation.
See [Tuple mapping](tuple-mapping.md) for `ValueTuple`, `System.Tuple`, Update,
and factory behavior.

## Destination types

Mappings can use classes, structs, records, tuples, nullable value types and
closed generic types. Interfaces, abstract classes and scalar types can also be
destinations, but Create needs an explicit result when Morphant cannot
construct one.

An existing interface or abstract destination can still be updated when its
members are assignable. For a custom whole-value mapping, use
[`Convert`](api/convert.md).

See [Declarative mapping](declarative-mapping.md) for explicit rules and
[Current limitations](limitations.md) for unsupported features.
