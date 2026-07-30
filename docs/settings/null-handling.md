# Null handling

`NullSourceHandling` controls what a generated mapping does when its source is
`null`. `NullDestinationHandling` controls what the `MapExisting` overload
does when its destination is `null`.

The library defaults are:

```csharp
NullSourceHandling.ReturnNull
NullDestinationHandling.CreateNew
```

## Configure assembly defaults

Set both properties in `Directory.Build.props` to configure projects under a
directory:

```xml
<Project>
  <PropertyGroup>
    <MorphantNullSourceHandling>ReturnDestination</MorphantNullSourceHandling>
    <MorphantNullDestinationHandling>Throw</MorphantNullDestinationHandling>
  </PropertyGroup>
</Project>
```

The same properties can be set in a project file:

```xml
<PropertyGroup>
  <MorphantNullSourceHandling>Throw</MorphantNullSourceHandling>
  <MorphantNullDestinationHandling>CreateNew</MorphantNullDestinationHandling>
</PropertyGroup>
```

MSBuild resolves imports before Morphant runs. Values in a `.csproj` therefore
normally override values imported earlier from `Directory.Build.props`, and
the generator receives only the final values.

Property values are case-insensitive. A missing, empty, or `Default` value
inherits the library default.

## Configure a mapper default

Set mapper-level values when most registrations use the same behavior:

```csharp
protected override void Configure(MapperBuilder builder)
{
    builder.NullSourceHandling(NullSourceHandling.Throw);
    builder.NullDestinationHandling(NullDestinationHandling.CreateNew);

    builder.Map<Order, OrderDto>();
    builder.Map<Customer, CustomerDto>();
}
```

Mapper-level settings apply to the whole mapper, regardless of whether the
setting call appears before or after its mapping registrations. If the same
setting is called more than once, the last call wins, including a last call
with `Default`.

## Override one mapping

Configure the builder returned by `Map<TSource, TDestination>()`:

```csharp
protected override void Configure(MapperBuilder builder)
{
    builder.NullSourceHandling(NullSourceHandling.Throw);
    builder.NullDestinationHandling(NullDestinationHandling.Throw);

    builder.Map<Order, OrderDto>()
        .NullSourceHandling(NullSourceHandling.ReturnNull)
        .NullDestinationHandling(NullDestinationHandling.CreateNew);
}
```

Each property is resolved independently in this order:

1. A non-`Default` mapping-level value.
2. A non-`Default` mapper-level value.
3. A non-`Default` MSBuild property.
4. The library default.

## Null source behavior

The source is handled before any destination check or mapping expression.

| Effective value | `MapNew` | `MapExisting` |
|---|---|---|
| `ReturnNull` | Returns `default(TDestination)` | Returns `default(TDestination)` |
| `ReturnDestination` | Returns `default(TDestination)` | Returns the original destination |
| `Throw` | Throws `ArgumentNullException(nameof(source))` | Throws `ArgumentNullException(nameof(source))` |

Despite its name, `ReturnNull` returns `default(TDestination)`. This is `null`
for reference and nullable value destinations, and the zero-initialized value
for a non-nullable value destination.

When both source and destination are `null`, only `NullSourceHandling` applies
because the source check runs first.

## Null destination behavior

`NullDestinationHandling` applies only to `MapExisting`:

| Effective value | Behavior |
|---|---|
| `CreateNew` | Runs the complete MapNew creation and mapping plan |
| `Throw` | Throws `ArgumentNullException(nameof(destination))` |

`CreateNew` also works when the effective `MappingMode` is `MapExisting`.
`MappingMode` controls which public overloads can be called; it does not
prevent the supported `MapExisting` overload from creating a replacement for
a missing destination.

For a two-parameter template:

```csharp
builder.Map<Source, Destination>()
    .Template((source, previous) => /* ... */);
```

`previous` always means the original destination value. When `CreateNew`
handles a missing destination, the template receives `null` or
`default(TDestination)` rather than the newly created object. The source
parameter is non-null because source handling runs before the template.

No source or destination runtime null check is generated for a definitely
non-nullable value type.

## Invalid values

C# setting expressions must be compile-time constants whose values are
defined by the corresponding enum. MSBuild properties must use a named enum
value.

An invalid effective `NullSourceHandling` keeps the generated mapping
contract, but both overloads throw `NotSupportedException`. An invalid
effective `NullDestinationHandling` affects only `MapExisting`; `MapNew`
remains available.

Configuration validity is checked independently of runtime arguments and type
nullability. A valid value at a more specific level can override an invalid
outer value for the same property.
