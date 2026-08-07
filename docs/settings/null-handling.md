# Null handling

`NullSourceHandling` controls what a generated mapping does when its source is
`null`. `NullDestinationHandling` controls what the `Update` overload
does when its destination is `null`.

The library defaults are:

```csharp
NullSourceHandling.ReturnNull
NullDestinationHandling.Create
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
  <MorphantNullDestinationHandling>Create</MorphantNullDestinationHandling>
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
    builder.NullDestinationHandling(NullDestinationHandling.Create);

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
        .NullDestinationHandling(NullDestinationHandling.Create);
}
```

Each property is resolved independently in this order:

1. A non-`Default` mapping-level value.
2. A non-`Default` value from a pair imported with
   `IncludeBase<TBaseSource, TBaseDestination>()`, nearest first.
3. A non-`Default` mapper-level value.
4. Non-`Default` root values from connected base mappers, nearest first.
5. A non-`Default` MSBuild property.
6. The library default.

Base roots participate only after an explicit `base.Configure(builder)` call,
and base pair values participate only after a typed `IncludeBase` call. See
[Configuration inheritance](../configuration-inheritance.md).

## Null source behavior

The source is handled before any destination check or mapping expression.

| Effective value | `Create` | `Update` |
|---|---|---|
| `ReturnNull` | Returns `default(TDestination)` | Returns `default(TDestination)` |
| `ReturnDestination` | Returns `default(TDestination)` | Returns the original destination |
| `Throw` | Throws `ArgumentNullException(nameof(source))` | Throws `ArgumentNullException(nameof(source))` |

Despite its name, `ReturnNull` returns `default(TDestination)`. This is `null`
for reference and nullable value destinations, and the zero-initialized value
for a non-nullable value destination.

When both source and destination are `null`, only `NullSourceHandling` applies
because the source check runs first.

When mapping continues, the declarative pipeline receives the normalized
non-null underlying source. A reference source has a non-null annotation, and
a nullable value source `T?` is unwrapped to `T`. `Construct` and `Members`
therefore do not repeat source null handling.

## Null destination behavior

`NullDestinationHandling` applies only to `Update`:

| Effective value | Behavior |
|---|---|
| `Create` | Treats the explicit `null` as no previous destination and runs the no-previous construction branch |
| `Throw` | Throws `ArgumentNullException(nameof(destination))` |

`Create` does not change the public operation: the call remains
`MappingOperation.Update`, and only `MappingMode.Update` must be enabled.
Inside the declarative pipeline, the normalized previous value is
`Option<TDestination>.None`, exactly as it is for public `Create`. A configured
no-previous `Construct` branch may obtain the result through a constructor,
factory, or another explicit strategy; the setting does not promise a new
object identity.

`Throw` runs before `Construct` or `Members`. Source handling also runs before
the destination check, so declarative mapping sees a non-null source.

No source or destination runtime null check is generated for a definitely
non-nullable value type.

## Manual `Convert`

Both null-handling settings are bypassed by a mapping configured with
`Convert`. The lambda receives the original source, including `null`, and an
`Option<TDestination>` describing the actual destination instance:

| Call | `context.Operation` | `previous` |
|---|---|---|
| `Map(source)` | `Create` | `None` |
| `Map(source, null)` | `Update` | `None` |
| `Map(source, destination)` | `Update` | `Some(destination)` |

Inherited null-handling settings remain useful to declarative mappings in the
same mapper but have no effect on `Convert`. Setting either policy explicitly
on a manual pair is an invalid configuration. Until configuration diagnostics
are implemented, invoking that pair throws `NotSupportedException`.

The value returned by `Convert`, including `null`, is final. Morphant does not
apply a null guard, construction fallback, or member mapping afterward.

## Invalid values

C# setting expressions must be compile-time constants whose values are
defined by the corresponding enum. MSBuild properties must use a named enum
value.

An invalid effective `NullSourceHandling` keeps the generated mapping
contract, but both methods throw `NotSupportedException`. An invalid
effective `NullDestinationHandling` affects only `Update`; public `Create`
remains available.

Configuration validity is checked independently of runtime arguments and type
nullability. A valid value at a more specific level can override an invalid
outer value for the same property.

See [Declarative mapping](../declarative-mapping.md) for previous presence and
the authoritative result, and [Manual mapping](../manual-mapping.md) for the
model that bypasses these policies.
