# `Convert`

Uses an ordinary synchronous callback as the complete mapping algorithm. Use
it when construction, mutation, loops, branching, or strict statement order
are clearer in normal C# than as declarative rules.

## Availability

`Convert` is available for every valid mapping pair.

## Overloads

Each overload accepts a `mapping` callback and returns the same mapping
builder. Inline lambdas and method groups are supported, as are compatible
delegates stored in accessible mapper or static members.

| Callback | Available information |
|---|---|
| `source => result` | Original source |
| `(source, previous) => result` | Source and existing destination |
| `(source, previous, context) => result` | Source, destination, and `MappingContext` |

| Callback value | Description |
|---|---|
| `source` | Original source, which may be `null` |
| `previous` | `Option<TDestination>` containing the supplied destination, when available |
| `context` | Current `MappingContext`, including `Operation` and `Mapper` |
| Return value | Final mapping result: `null`, reused destination, or replacement |

```csharp
builder.Map<OrderDto, Order>()
    .Convert((source, previous) =>
    {
        if (source is null)
            return null!;

        var order = previous.TryGetValue(out var existing)
            ? existing
            : new Order(source.Id);

        order.UpdateFrom(source);
        return order;
    });
```

The callback bypasses null handling, constructor selection, member conventions,
and `Members`. `MappingMode` still controls whether Create and Update may be
called. `Convert` cannot be combined with destination methods, `Members`, or
`IncludeMembers`.

Related: [manual mapping](../manual-mapping.md),
[dependency injection and `IMapper`](../runtime-dispatch.md).
