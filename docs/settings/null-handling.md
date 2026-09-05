# Null handling

Morphant has separate settings for a null source and a null Update
destination.

## Null source

`NullSourceHandling` defaults to `ReturnNull`:

| Value | Create | Update |
|---|---|---|
| `ReturnNull` | Return `default(TDestination)` | Return `default(TDestination)` |
| `ReturnDestination` | Return `default(TDestination)` | Return the supplied destination |
| `Throw` | Throw `NullSourceException` | Throw `NullSourceException` |

For a non-nullable value destination, `ReturnNull` returns its zero-initialized
default value.

`NullSourceHandling` is applied before destination handling or mapping
expressions. If mapping continues, mapping lambdas receive the non-null source
value.

## Null Update destination

`NullDestinationHandling` defaults to `Create` and applies only to Update:

| Value | Behavior |
|---|---|
| `Create` | Use the same creation rules as when no destination is available |
| `Throw` | Throw `NullDestinationException` |

With `Create`, the public operation remains Update; `MappingMode.Update` must
be enabled, while `MappingMode.Create` is not required.

When both source and destination are null, `NullSourceHandling` is applied
first.

## Configure null handling

```csharp
builder.Map<OrderDto?, Order?>()
    .NullSourceHandling(NullSourceHandling.Throw)
    .NullDestinationHandling(NullDestinationHandling.Create);
```

The assembly properties are `MorphantNullSourceHandling` and
`MorphantNullDestinationHandling`.

Manual `Convert` mappings bypass both settings. They receive the original
source and an `Option<TDestination>` that indicates whether an existing
destination is available. See [Manual mapping](../manual-mapping.md).

## Result nullability

The caller chooses the result's nullable annotation through `TDestination`.
Use a nullable destination when your inputs and mapping can produce null:

```csharp
var order = mapper.Map<OrderDto, Order?>(orderDto);
```

A non-nullable destination expresses your expectation; Morphant does not add
a null-result check. Reference-type nullable annotations do not change the
registered mapping or its null-handling settings. Direct `Create` and `Update`
calls use the destination annotation of `ITypeMapper<TSource, TDestination>`.

See the [settings overview](README.md) for levels and precedence.
