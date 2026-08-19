# Flatten nested source members

Auto flattening maps a destination name to a nested source path by joining
property and field names:

```csharp
// CustomerAddressCity <- source.Customer.Address.City
builder.Map<Order, OrderDto>();
```

It is enabled by default for convention-based constructor parameters and
destination members. Paths can have any depth, but every segment must be a
readable instance property or field. Destination-member matching is exact;
constructor parameters keep their usual exact-then-case-insensitive lookup.
Morphant does not remove underscores, normalize words, call methods or start a
nested mapping.

## Precedence and ambiguity

Morphant checks explicit rules, direct root members, direct
[`IncludeMembers`](include-members.md) members, root flattened paths, and
included flattened paths, in that order. A direct member keeps ownership even
when its type is incompatible. For a flattened tier, incompatible or
nullable-unsafe paths are ignored before choosing a candidate.

If several compatible paths have the same joined name, Morphant reports
`MORPH0051` instead of choosing by declaration order. Select the intended path
explicitly:

```csharp
.Members(source => new()
{
    CustomerAddressCity = source.Customer.Address.City
})
```

[`MemberSelection.Explicit`](settings/member-selection.md) disables implicit
destination-member flattening. `Auto()` requests it again for one listed
member. Constructor conventions still use flattening.

## Nullable paths

If any intermediate object can be `null`, the flattened value is nullable.
Thus `int` can map to `int?`, but not to `int`; a missing object produces
`null`, never `0` or another fabricated value. The same rule applies to
reference types and constructor arguments.

Use an explicit expression when your application has a stronger invariant or
needs a fallback value.

Morphant does not perform the reverse operation: a flat source member does not
cause nested destination objects to be created.
