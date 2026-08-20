# `Resolve`

Chooses the destination for every Create and Update. Use it when the mapping
must decide whether to reuse an existing destination or construct a
replacement.

## Availability

`Resolve` has the same constructor requirement as [`Construct`](construct.md):
the destination must expose at least one accessible constructor with
supported by-value parameters.

## Overloads

Each overload accepts a `resolve` callback and returns the same mapping
builder.

| Callback | Use when |
|---|---|
| `(source, previous) => result` | Selection depends on the source and existing destination |
| `(source, previous, context) => result` | Selection also depends on Create versus Update |

| Callback value | Description |
|---|---|
| `source` | Non-null source after null-source handling |
| `previous` | `Option<TDestination>` containing the existing destination, when available |
| `context` | Declarative context; `Operation` is Create or Update |
| Return value | `previous` or a supported destination constructor expression |

```csharp
builder.Map<OrderDto, Order>()
    .Resolve((source, previous) =>
        previous.TryGetValue(out var order) && order.Id == source.Id
            ? previous
            : new(source.Id));
```

`Resolve` can be combined with [`Members`](members.md), but not with another
destination method or `Convert`.

Related: [Create and Update](../create-and-update.md),
[declarative expressions](declarative-expressions.md).
