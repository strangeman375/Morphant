# `Resolve`

Chooses the destination after null handling on Create and Update. Use it when
the mapping must decide whether to reuse an existing destination or construct
a replacement.

## Availability

`Resolve` has the same constructor requirement as [`Construct`](construct.md):
the destination must expose at least one accessible constructor with
supported by-value parameters, or be a BCL tuple with intrinsic construction.

## Overloads

Use an inline lambda.

| Callback | Use when |
|---|---|
| `(source, previous) => result` | Selection depends on the source and existing destination |
| `(source, previous, context) => result` | Selection also depends on Create versus Update |

Return `previous` or a construction expression as in [`Construct`](construct.md). See [callback inputs](README.md#callback-inputs).

```csharp
builder.Map<OrderDto, Order>()
    .Resolve((source, previous) =>
    {
        if (previous.TryGetValue(out var order) && order.Id == source.Id)
            return previous;
        return new(source.Id);
    });
```

When a conditional expression returns `previous` in one branch, write the
construction type explicitly in the other, for example
`new OrderConstruction(source.Id)`.

Use [`ResolveUsing`](resolve-using.md) to return an ordinary object such as
`new Order(source.Id)` instead.

`Resolve` can be combined with [`Members`](members.md), but not with another
destination method or `Convert`.

Related: [Create and Update](../create-and-update.md),
[declarative expressions](declarative-expressions.md),
[tuple mapping](../tuple-mapping.md).
