# Include nested source members

[`IncludeMembers`](api/include-members.md) adds properties and fields of
selected nested source objects to constructor and destination-member
conventions:

```csharp
builder.Map<Order, OrderDto>()
    .IncludeMembers(source => new
    {
        source.Customer,
        source.Audit
    });
```

For one object, pass its path directly:
`.IncludeMembers(source => source.Customer)`. An included object does not need
its own map and does not start a nested mapping.

## Selection and precedence

Each selection must be an inline property or field path rooted in `source`.
Deep and conditional paths are supported:

```csharp
.IncludeMembers(source => source.Envelope?.Audit)
```

Methods, indexers, casts and computed expressions are invalid. Repeating a
path reports [`MORPH0049`](diagnostics/MORPH0049.md).

Explicit `Members` rules take precedence, followed by direct root-source and
included members, then root and included
[`flattened paths`](flattening.md). Two included objects exposing the same
exact, case-sensitive name produce
[`MORPH0050`](diagnostics/MORPH0050.md). With
[`MemberSelection.Explicit`](settings/member-selection.md), included members
are available to `Auto()` and constructor conventions only.

## Nullable paths

A nullable segment makes every value from that path nullable. For example, a
`string` member can map to `string?`, and an `int` member can map to `int?`.
If the selected object is missing, the result is `null`; Morphant does not use
`0` for a missing `int`. A nullable value is not mapped automatically to a
non-nullable target.

For an unconstrained generic `T`, automatic mapping is limited to targets that
can represent `null` for every `T`, such as `object?`.

Use `!` only when the object must exist:

```csharp
.IncludeMembers(source => source.Customer!)
```

The assertion is preserved in generated code and can throw when false. It does
not unwrap `Nullable<T>`.

## Composition and validation

[`IncludeBase`](api/include-base.md) inherits included objects.
`IncludeMembers` cannot be combined with [`Convert`](api/convert.md).

Source-side `UnmappedMemberValidation` checks the included member surface. A
compile-time discard can acknowledge one member or the complete object:

```csharp
.Members(source =>
{
    _ = source.Customer.LegacyCode;
    _ = source.Audit;

    return new() { Name = Auto() };
})
```

Discard statements are compile-time only and do not themselves call getters.
