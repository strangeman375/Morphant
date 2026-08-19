# Include nested source members

`IncludeMembers` adds the readable properties and fields of selected nested
source objects to Morphant's convention lookup. Select one source directly:

```csharp
builder.Map<Order, OrderDto>()
    .IncludeMembers(source => source.Customer);
```

Or select several sources in one call:

```csharp
builder.Map<Order, OrderDto>()
    .IncludeMembers(source => new
    {
        source.Customer,
        source.Audit
    });
```

The included members can supply both constructor parameters and destination
members. They do not start a nested mapping and do not require a separate map
for the selected type.

## Selection and precedence

Each selection must be an inline property or field path rooted in `source`.
Direct and deep paths are supported, including inside the anonymous object:

```csharp
.IncludeMembers(source => source.Customer)
.IncludeMembers(source => source.Envelope?.Audit)
```

Methods, indexers, casts, computed expressions and delegate variables are not
valid selectors. Repeating the same path, whether in one or several calls,
reports `MORPH0049`.

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

A nullable path makes every included value nullable for compatibility checks.
The usual warning-free C# conversion rules then apply:

- an included reference member can map to a nullable reference target, but not
  to a non-nullable one;
- an included `int` member can map to `int?`, but not to `int`;
- when the path is `null`, both nullable targets receive `null`.

Morphant never substitutes `0` for a missing included value merely because
the underlying member is an `int`.

Use the null-forgiving operator only when the path is an application
invariant:

```csharp
.IncludeMembers(source => source.Customer!)
```

For nullable references, the assertion is preserved in generated code and can
throw `NullReferenceException` when it is false. As in ordinary C#,
null-forgiving does not unwrap `Nullable<T>`; a missing nullable value still
produces `null`.

## Composition and validation

`IncludeBase` inherits included scopes in base-first order. Local root-member
precedence and ambiguity checks still apply. `IncludeMembers` cannot be
combined with `Convert`, because `Convert` owns the complete mapping.

When source-side `UnmappedMemberValidation` is enabled, the selected path
counts as used and the readable members of the included scope are validated.
Use the existing compile-time discard in `Construct`, `Resolve` or `Members`
to acknowledge one nested member or the entire included scope:

```csharp
.Members(source =>
{
    _ = source.Customer.LegacyCode; // one member
    _ = source.Audit;               // the whole included scope

    return new()
    {
        Name = Auto()
    };
})
```

These statements only affect validation; their getters are not called while
mapping.
