# `ResolveUsing`

Runs an ordinary synchronous callback to choose the destination for every
Create and Update. Use it when reuse or replacement needs a factory, cache, or
other runtime logic.

## Availability

`ResolveUsing` is available for every valid mapping pair.

## Overloads

Each overload accepts a `resolve` callback and returns the same mapping
builder. Inline lambdas and method groups are supported, as are compatible
delegates stored in accessible mapper or static members.

| Callback | Use when |
|---|---|
| `(source, previous) => destination` | Selection needs the source and existing destination |
| `(source, previous, context) => destination` | Selection also needs `MappingContext` |

| Callback value | Description |
|---|---|
| `source` | Non-null source after null-source handling |
| `previous` | `Option<TDestination>` containing the existing destination, when available |
| `context` | Current `MappingContext`, including `Operation` and `Mapper` |
| Return value | Destination selected for the operation |

```csharp
builder.Map<OrderDto, IOrder>()
    .ResolveUsing((source, previous) =>
        previous.TryGetValue(out var order) && order.Id == source.Id
            ? order
            : orderFactory.Create(source.Id));
```

A `null` callback result is final: Morphant skips `Members` and does not apply
null handling again. A non-null result can continue through
[`Members`](members.md), but it remains the selected result regardless of
whether it is the previous instance or a replacement. Morphant can assign
settable members or run an eligible nested `Update`; an `init`-only member must
already be initialized in the returned result. Configuring it in `Members`
produces [`MORPH0042`](../diagnostics/MORPH0042.md). `ResolveUsing` cannot be
combined with another destination method or `Convert`.

Related: [`Resolve`](resolve.md),
[dependency injection and `IMapper`](../runtime-dispatch.md).
