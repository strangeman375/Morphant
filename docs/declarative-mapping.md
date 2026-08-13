# Declarative mapping

A bare mapping uses exact-name conventions:

```csharp
builder.Map<Customer, CustomerDto>();
```

When conventions are not enough, use `Construct` or `Resolve` to choose the
destination and `Members` to configure its members.

## Choose the destination

Each mapping can use at most one of these methods:

| Method | When it applies | Use it for |
|---|---|---|
| `Construct` | No destination is available | Explicit constructor arguments |
| `Resolve` | Every Create and Update | Choosing reuse or replacement |
| `ConstructUsing` | No destination is available | A factory or another custom creation method |
| `ResolveUsing` | Every Create and Update | Custom reuse or replacement logic |

`Construct` and `Resolve` are available when the destination has a supported
constructor. Their inline lambdas describe construction:

```csharp
builder.Map<OrderDto, Order>()
    .Construct(source => new(source.Id));
```

`Resolve` can use the existing destination:

```csharp
builder.Map<SnapshotDto, Snapshot>()
    .Resolve((source, previous) =>
        previous.HasValue && previous.Value.Id == source.Id
            ? previous
            : new(source.Id));
```

`ConstructUsing` and `ResolveUsing` are ordinary synchronous C# callbacks.
Use them for factories, caches, interfaces, abstract destinations or other
ready-made results:

```csharp
builder.Map<OrderDto, IOrder>()
    .ConstructUsing(source =>
        orderFactory.Create(source.Id));
```

Their context-aware overloads receive `MappingContext`, whose `Mapper` can
invoke another registered mapping.

## Map destination members

```csharp
builder.Map<OrderDto, Order>()
    .Members((source, _) => new()
    {
        DisplayName = source.Name,
        Revision = Auto(),
        LegacyCode = Ignore()
    });
```

| Rule | Meaning |
|---|---|
| Ordinary expression | Use that value |
| `Auto()` | Apply exact-name convention to this member |
| `Ignore()` | Leave this member unchanged |
| `Map(...)` | Run an explicit nested mapping |
| `Value<T>(value)` | Pin the exact receiving type |

Members not mentioned in `Members` follow the configured
[`MemberSelection`](settings/member-selection.md).

Use `Value<T>` when target typing is otherwise ambiguous, for example for
boxing, nullable annotations, lambdas or overloaded constructors:

```csharp
.Members((source, _) => new()
{
    Payload = Value<object>(source.PayloadId),
    Label = Value<string?>(source.Label)
});
```

## Existing destinations

In `Resolve` and the overloads of `Members` that receive `previous`,
`Option<TDestination>` indicates whether Morphant has an existing destination
value to reuse. It is `Some(destination)` when one is available and `None`
otherwise:

```csharp
if (previous.TryGetValue(out var destination))
{
    // An existing destination is available.
}
```

`None` can mean Create or an Update whose destination is `null` and handled by
creating a replacement. Use the context overload and check
`context.Operation` when that distinction matters.

An Update can reuse the supplied instance or select a replacement. The caller
must always use the returned result.

## Lambda rules

Code passed to `Construct`, `Resolve` and `Members` follows these rules:

- pass an inline lambda;
- use expressions, initialized locals, complete `if`/`switch` branches,
  returns and throws;
- do not mutate `previous` or `result`;
- do not capture local variables declared inside `Configure`;
- do not rely on the order of independent member expressions or side effects.

A local can express a dependency and is evaluated once where needed. Use
[`Convert`](manual-mapping.md) when loops, mutation, `try`, strict statement
order or another ordinary C# algorithm would be clearer.

See [Nested mapping](nested-mapping.md) for `Map`, `Create` and `Update` rules,
and [Constructor selection](settings/constructor-selection.md) for convention
construction.
