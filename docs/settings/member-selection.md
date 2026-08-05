# Member selection

`MemberSelection` controls destination members that are not mentioned in an
explicit `Members` plan. It does not change the meaning of an explicit value,
`Auto()`, or `Ignore()` rule.

The library default is:

```csharp
MemberSelection.Auto
```

## Configure an assembly default

Set `MorphantMemberSelection` in `Directory.Build.props` to configure projects
under a directory:

```xml
<Project>
  <PropertyGroup>
    <MorphantMemberSelection>Explicit</MorphantMemberSelection>
  </PropertyGroup>
</Project>
```

The same property can be set in a project file. Supported values are
`Default`, `Auto`, and `Explicit`; names are case-insensitive. A missing,
empty, or `Default` value continues to the library default.

MSBuild resolves imports before Morphant runs. A value in a `.csproj`
therefore normally overrides a value imported earlier from
`Directory.Build.props`, and the generator receives only the final value.

## Configure a mapper default

Use a mapper-level setting when most registrations should use the same
selection policy:

```csharp
protected override void Configure(MapperBuilder builder)
{
    builder.MemberSelection(MemberSelection.Explicit);

    builder.Map<OrderDto, Order>()
        .Members((source, _) => new()
        {
            Number = source.Number
        });

    builder.Map<CustomerDto, Customer>()
        .Members((source, _) => new()
        {
            Name = source.Name
        });
}
```

Mapper-level settings apply to the whole mapper, regardless of whether the
setting call appears before or after its mapping registrations. If the same
setting is called more than once, the last call wins, including a last call
with `Default`.

## Override one mapping

Configure the builder returned by `Map<TSource, TDestination>()`:

```csharp
builder.Map<OrderDto, Order>()
    .MemberSelection(MemberSelection.Auto)
    .Members((source, _) => new()
    {
        DisplayName = source.Number
    });
```

The effective value is selected in this order:

1. A non-`Default` mapping-level value.
2. A non-`Default` mapper-level value.
3. A non-`Default` `MorphantMemberSelection` MSBuild property.
4. `MemberSelection.Auto`.

`Default` continues to the next level.

## Selection behavior

| Effective value | Member absent from `Members` |
|---|---|
| `Auto` | Maps by convention when an exact-name, warning-free implicit conversion exists |
| `Explicit` | Preserves the value supplied by construction or the existing destination |

An explicit rule always occupies its destination member before conventions:

```csharp
builder.Map<OrderDto, Order>()
    .Members((source, _) => new()
    {
        Name = source.DisplayName,
        Revision = Auto(),
        LegacyCode = Ignore()
    });
```

- `Name` uses the explicit expression even if a source member named `Name`
  exists.
- `Revision` must be mappable by the ordinary convention rules, regardless of
  the effective `MemberSelection`.
- `LegacyCode` is not assigned and preserves the value of the selected result.
- Other supported members follow the effective selection policy.

Conventions and `Auto()` require an exact case-sensitive member name and a
warning-free implicit C# conversion. They never start a nested mapping merely
because two member names match. Nested mapping uses an explicit `Map(...)`
rule.

The policy applies after constructor, direct `Construct`, and `ByFactory`
result selection. A direct or factory result only exposes post-construction
setters and mutable fields; structured construction can additionally expose
applicable `init` and creation-time `required` members.

## Invalid values

C# setting expressions must be compile-time constants whose values are
defined by `MemberSelection`. The MSBuild property must use one of the named
values above.

An invalid effective value keeps the generated `ITypeMapper` contract, but
both mapping overloads throw `NotSupportedException` when invoked. A valid
value at a more specific level overrides an invalid outer value.
