# `Convert`

Uses an ordinary synchronous callback as the complete mapping algorithm. Use
it when construction, mutation, loops, branching, or strict statement order
are clearer in normal C# than as declarative rules.

## Availability

`Convert` is available for every valid mapping pair.

## Overloads

Use an inline lambda, method group, or accessible delegate.

| Callback | Available information |
|---|---|
| `source => result` | Original source |
| `(source, previous) => result` | Source and existing destination |
| `(source, previous, context) => result` | Source, destination, and `MappingContext` |

Return the final result: `null`, the supplied destination, or a replacement. See [callback inputs](README.md#callback-inputs).

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

The callback owns null handling, construction and member mapping. For the
settings that still apply and restrictions on explicit settings, see
[setting applicability](../settings/README.md#applicability). `Convert` cannot
be combined with destination methods, `Members`, or `IncludeMembers`.

## Nested calls

Use the context-aware overload and `context.Mapper` to call another mapping:

```csharp
var address = previous.TryGetValue(out var destination)
    ? context.Mapper.Map(source.Address, destination.Address)
    : context.Mapper.Map<AddressDto, Address>(source.Address);
```

The body is ordinary C#; declarative helpers such as `Auto()`, `Ignore()` and
nested `Map()` are not used here. See
[dependency injection and `IMapper`](../runtime-dispatch.md) for registration
and context lifetime, or the
[collection recipe](../recipes.md#map-a-collection-with-custom-code) for mapping
a collection with custom code.
