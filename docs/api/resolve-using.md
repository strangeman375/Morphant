# `ResolveUsing`

Runs an ordinary synchronous callback to choose the destination after null
handling on Create and Update. Use it when reuse or replacement needs a factory,
cache, or other runtime logic.

## Availability

`ResolveUsing` is available for every valid mapping pair.

## Overloads

Use an inline lambda, method group, or accessible delegate.

| Callback | Use when |
|---|---|
| `(source, previous) => destination` | Selection needs the source and existing destination |
| `(source, previous, context) => destination` | Selection also needs `MappingContext` |

Return the destination object to reuse or replace. See [callback inputs](README.md#callback-inputs).

```csharp
builder.Map<OrderDto, IOrder>()
    .ResolveUsing((source, previous) =>
        previous.TryGetValue(out var order) && order.Id == source.Id
            ? order
            : orderFactory.Create(source.Id));
```

A `null` result is final and skips `Members`. A non-null result receives only
rules valid after construction, including when the callback returns a
replacement. See [factory result rules](construct-using.md#factory-result)
for initialization restrictions.

`ResolveUsing` cannot be combined with another destination method or `Convert`.

Related: [`Resolve`](resolve.md),
[dependency injection and `IMapper`](../runtime-dispatch.md).
