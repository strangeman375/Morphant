# `ConstructUsing`

Runs an ordinary synchronous callback when no destination is available. Use it
for factories, interfaces, abstract destinations, caches, or other creation
that is not a destination constructor expression.

## Availability

`ConstructUsing` is available for every valid mapping pair.

## Overloads

Each overload accepts a `construct` callback and returns the same mapping
builder.

| Callback | Use when |
|---|---|
| `source => destination` | Creation needs only the source |
| `(source, context) => destination` | Creation also needs `MappingContext` |

| Callback value | Description |
|---|---|
| `source` | Non-null source after null-source handling |
| `context` | Current `MappingContext`, including `Operation` and `Mapper` |
| Return value | Final destination |

```csharp
builder.Map<OrderDto, IOrder>()
    .ConstructUsing(source =>
        orderFactory.Create(source.Id));
```

A `null` callback result is final: Morphant skips `Members` and does not apply
null handling again. A non-null result can continue through
[`Members`](members.md). `ConstructUsing` cannot be combined with another
destination method or `Convert`.

Related: [`ResolveUsing`](resolve-using.md),
[dependency injection and `IMapper`](../runtime-dispatch.md).
