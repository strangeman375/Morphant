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

The source policy runs before destination handling or mapping expressions. If
mapping continues, declarative callbacks receive the non-null source value.

## Null Update destination

`NullDestinationHandling` defaults to `Create` and applies only to Update:

| Value | Behavior |
|---|---|
| `Create` | Run the branch used when no destination is available |
| `Throw` | Throw `NullDestinationException` |

With `Create`, the public operation remains Update; `MappingMode.Update` must
be enabled, while `MappingMode.Create` is not required.

When both source and destination are null, the source policy wins.

## Configure the policies

```csharp
builder.Map<OrderDto?, Order?>()
    .NullSourceHandling(NullSourceHandling.Throw)
    .NullDestinationHandling(NullDestinationHandling.Create);
```

The assembly properties are `MorphantNullSourceHandling` and
`MorphantNullDestinationHandling`.

Manual `Convert` mappings bypass both policies and receive the original source
and actual destination presence. See [Manual mapping](../manual-mapping.md).

See the [settings overview](README.md) for levels and precedence.
