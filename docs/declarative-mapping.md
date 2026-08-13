# Declarative mapping

A bare mapping uses exact-name conventions:

```csharp
builder.Map<Customer, CustomerDto>();
```

Use a result policy when Morphant should not select the destination by
convention, and use `Members` to describe destination-member values.

## Choose the result

Each mapping can have at most one result policy:

| Method | When it runs | Use it for |
|---|---|---|
| `Construct` | No destination is available | An explicit constructor plan |
| `Resolve` | Every Create and Update | Choosing reuse or replacement |
| `ConstructUsing` | No destination is available | A factory or ready-made result |
| `ResolveUsing` | Every Create and Update | Runtime reuse or replacement logic |

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

Members not mentioned in the plan follow the effective
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

`Option<T>` distinguishes an absent destination from a destination whose value
is `default` or `null`:

```csharp
if (previous.TryGetValue(out var destination))
{
    // A destination was supplied.
}
```

An Update can reuse the supplied instance or select a replacement. The caller
must always use the returned result.

## Structured callback rules

`Construct`, `Resolve` and `Members` are declarations rather than ordinary
runtime callbacks. Keep them predictable:

- pass an inline lambda;
- use expressions, initialized locals, complete `if`/`switch` branches,
  returns and throws;
- do not mutate `previous` or `result`;
- do not capture runtime locals declared inside `Configure`;
- do not rely on the order of independent member expressions or side effects.

A local can express a real dependency and is evaluated once where needed.
Use [`Convert`](manual-mapping.md) when loops, mutation, `try`, strict statement
order or another ordinary C# algorithm would be clearer.

See [Nested mapping](nested-mapping.md) for `Map`, `Create` and `Update` rules,
and [Constructor selection](settings/constructor-selection.md) for convention
construction.
