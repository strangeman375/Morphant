# Include nested source members

`IncludeMembers` adds the readable properties and fields of a selected nested
source object to Morphant's convention lookup:

```csharp
builder.Map<Order, OrderDto>()
    .IncludeMembers(source => source.Customer)
    .IncludeMembers(source => source.Audit);
```

The included members can supply both constructor parameters and destination
members. They do not start a nested mapping and do not require a separate map
for the selected type.

## Selection and precedence

The selector must be an inline property or field path rooted in `source`.
Direct and deep paths are supported:

```csharp
.IncludeMembers(source => source.Customer)
.IncludeMembers(source => source.Envelope?.Audit)
```

Methods, indexers, casts, computed expressions and delegate variables are not
valid selectors.

Convention lookup uses this order:

1. A readable member on the root source.
2. A member from an included scope.

A root member therefore wins over an included member with the same exact,
case-sensitive name. If two included scopes expose the same name, Morphant
reports `MORPH0050`; remove one of the conflicting scopes. Ordinary explicit
`Members` rules still take precedence over conventions.

With `MemberSelection.Explicit`, included members are used only by explicitly
requested `Auto()` rules and by constructor conventions:

```csharp
builder.Map<Order, OrderDto>()
    .IncludeMembers(source => source.Customer)
    .MemberSelection(MemberSelection.Explicit)
    .Members(source => new()
    {
        Name = Auto()
    });
```

## Null paths

When a nullable path segment is `null`, an included value becomes
`default(TMember)`. The usual warning-free C# conversion check still applies,
so a possibly null value is not mapped automatically to a non-nullable target.

Use the null-forgiving operator only when the path is an application
invariant:

```csharp
.IncludeMembers(source => source.Customer!)
```

For nullable references, the assertion is preserved in generated code and can
throw `NullReferenceException` when it is false. As in ordinary C#,
null-forgiving does not unwrap `Nullable<T>`; a missing nullable value still
produces `default(TMember)`.

## Composition and validation

`IncludeBase` inherits included scopes in base-first order. Local root-member
precedence and ambiguity checks still apply. `IncludeMembers` cannot be
combined with `Convert`, because `Convert` owns the complete mapping.

When source-side `UnmappedMemberValidation` is enabled, the selected path
counts as used and the readable members of the included scope are validated.
