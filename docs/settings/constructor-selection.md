# Constructor selection

`ConstructorSelection` controls constructor choice for structured
convention-based creation, including a `ByConvention()` branch inside an
explicit `Construct` plan.

The library default is:

```csharp
ConstructorSelection.Unambiguous
```

## Configure an assembly default

Set `MorphantConstructorSelection` in `Directory.Build.props` to configure
projects under a directory:

```xml
<Project>
  <PropertyGroup>
    <MorphantConstructorSelection>Greediest</MorphantConstructorSelection>
  </PropertyGroup>
</Project>
```

The same property can be set in a project file. Supported values are
`Default`, `Explicit`, `Parameterless`, `Single`, `Unambiguous`, `Greediest`,
and `Largest`; names are case-insensitive. A missing, empty, or `Default`
value continues to the library default.

MSBuild resolves imports before Morphant runs. A value in a `.csproj`
therefore normally overrides a value imported earlier from
`Directory.Build.props`, and the generator receives only the final value.

## Configure a mapper default

Use a mapper-level setting when most registrations should follow the same
constructor policy:

```csharp
protected override void Configure(MapperBuilder builder)
{
    builder.ConstructorSelection(ConstructorSelection.Greediest);

    builder.Map<OrderDto, Order>();
    builder.Map<CustomerDto, Customer>();
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
    .ConstructorSelection(ConstructorSelection.Largest);
```

The effective value is selected in this order:

1. A non-`Default` mapping-level value.
2. A non-`Default` value from a pair imported with
   `IncludeBase<TBaseSource, TBaseDestination>()`, nearest first.
3. A non-`Default` mapper-level value.
4. Non-`Default` root values from connected base mappers, nearest first.
5. A non-`Default` `MorphantConstructorSelection` MSBuild property.
6. `ConstructorSelection.Unambiguous`.

`Default` continues to the next level.

Base roots participate only after an explicit `base.Configure(builder)` call,
and base pair values participate only after a typed `IncludeBase` call. See
[Configuration inheritance](../configuration-inheritance.md).

## Selection behavior

Only supported constructors participate. A supported constructor is
accessible from generated assembly-level code and has ordinary by-value,
nameable, non-ref-like parameters. Inaccessible constructors and constructors
with `ref`, `in`, or `out` parameters are ignored.

| Effective value | Selection |
|---|---|
| `Explicit` | No constructor is selected automatically |
| `Parameterless` | The supported parameterless constructor |
| `Single` | The constructor only when exactly one supported constructor exists |
| `Unambiguous` | The only supported parameterized constructor, or the parameterless constructor when no parameterized constructor exists |
| `Greediest` | The unique applicable constructor receiving the most mapped arguments |
| `Largest` | The unique supported constructor declaring the most parameters |

`Unambiguous` deliberately prefers one parameterized constructor over a
simultaneously available parameterless constructor. Two or more supported
parameterized constructors are ambiguous, even when a parameterless
constructor also exists.

`Greediest` first builds every applicable convention plan. Required arguments
must have warning-free implicit conversions from matching readable source
members. An `optional` or `params` parameter may be omitted; an omitted
parameter does not increase the score. A mapped `params` value is passed as
one array argument, never as an expanded argument list.

`Largest` counts declared parameters before applicability is checked. Once it
selects the unique largest constructor, a missing required argument or an
incompatible conversion makes creation unavailable. Morphant does not fall
back to a smaller constructor.

`Single`, `Unambiguous`, and `Parameterless` follow the same no-fallback rule:
after shape-based selection, the selected constructor must itself be
applicable.

A tie for the best `Greediest` score or largest declared size is ambiguous.
Morphant does not use declaration order as a tiebreaker; configure an explicit
`Construct` instead.

Required destination members must be supplied by the creation-time member
plan unless the selected constructor has `SetsRequiredMembers`. This check is
part of constructor applicability and therefore also participates in
`Greediest` selection.

## `ByConvention` and explicit `Construct`

`ByConvention()` uses the effective `ConstructorSelection`:

```csharp
builder.Map<OrderDto, Order>()
    .ConstructorSelection(ConstructorSelection.Greediest)
    .Construct(source => new(
        ByConvention(),
        new()
        {
            tenantId = source.TenantId,
            legacyCode = Ignore()
        }));
```

Written parameter rules participate in applicability and in the `Greediest`
score. Explicit expressions and successful `Auto()` rules count as passed
arguments. `Ignore()` is valid only for an optional or `params` parameter and
does not count.

An explicit constructor or factory branch is not changed by the setting:

```csharp
builder.Map<OrderDto, Order>()
    .ConstructorSelection(ConstructorSelection.Explicit)
    .Construct(source => new(source.Id));
```

With `Explicit`, a conventional mapping without `Construct`, or a
`ByConvention()` branch, has no available creation plan. Updating an existing
destination can still succeed because constructor selection applies only to
no-previous creation.

## Direct and manual mappings

Inherited constructor settings have no effect on a direct destination or a
mapping configured with `Convert`; the same mapper-level default can serve
other structured mappings.

Setting `ConstructorSelection` explicitly on an individual direct or manual
mapping is an invalid configuration, including an explicit `Default`. Until
configuration diagnostics are implemented, invoking that mapping throws
`NotSupportedException`.

## Invalid values

C# setting expressions must be compile-time constants whose values are
defined by `ConstructorSelection`. The MSBuild property must use one of the
named values above.

An invalid effective value makes convention-based creation and
`ByConvention()` unavailable. The generated mapping contract remains, and an
Update call with an existing destination may still execute without creating a
replacement. A valid value at a more specific level overrides an invalid
outer value.
