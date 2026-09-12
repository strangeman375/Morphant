# `Resolve`

Chooses the destination after null handling on Create and Update. Use it when
the mapping must decide whether to reuse an existing destination or construct
a replacement.

## Availability

`Resolve` has the same constructor requirement as [`Construct`](construct.md).

## Overloads

Use an inline lambda.

| Callback | Use when |
|---|---|
| `(source, previous) => result` | Selection depends on the source and existing destination |
| `(source, previous, context) => result` | Selection also depends on Create versus Update |

Return the available value from `previous` or a construction expression as in
[`Construct`](construct.md). `previous` remains an `Option<Destination>`;
use `previous.Value` after checking `HasValue`, or the variable assigned by a
successful `TryGetValue`. Unchanged local aliases of these values are supported.
See [callback inputs](README.md#callback-inputs).

```csharp
builder.Map<OrderDto, Order>()
    .Resolve((source, previous) =>
    {
        if (previous.TryGetValue(out var order) && order.Id == source.Id)
            return order;
        return new(source.Id);
    });
```

When a conditional expression returns the existing destination in one branch, write the
construction type explicitly in the other, for example
`new OrderConstruction(source.Id)`. C# can otherwise interpret `new(...)` as
construction of an ordinary `Order`.

Use [`ResolveUsing`](resolve-using.md) to return an ordinary object such as
`new Order(source.Id)` or a cached instance instead. `Resolve` reports
[`MORPH0062`](../diagnostics/MORPH0062.md) for those results, including helper
calls: returning `Identity(previous.Value)` is not a direct reuse expression.

`Resolve` can be combined with [`Members`](members.md), but not with another
destination method or `Convert`.

Related: [Create and Update](../create-and-update.md),
[declarative expressions](declarative-expressions.md),
[tuple mapping](../tuple-mapping.md).
