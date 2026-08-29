# Manual mapping

Use [`Convert`](api/convert.md) when the complete mapping is clearer as ordinary
synchronous C#:

```csharp
builder.Map<OrderDto, Order>()
    .Convert((source, previous, context) =>
    {
        if (source is null)
            return null!;

        if (previous.TryGetValue(out var destination))
        {
            destination.UpdateFrom(source);
            return destination;
        }

        return new Order(source.Id);
    });
```

The [`Convert` reference](api/convert.md) lists all callback overloads and
parameters.

`previous` is `Option<TDestination>`. It is `None` for Create and for an Update
without an actual destination, and `Some(destination)` otherwise.

The callback owns the whole mapping. Morphant does not apply null handling,
constructor selection, member conventions or `Members` afterward. The
configured [`MappingMode`](settings/mapping-mode.md) still controls whether
Create and Update are available.

The returned value is final: it may be `null`, the existing destination or a
replacement.

## Nested calls

Use the mapper from the current context for another runtime mapping:

```csharp
var address = previous.TryGetValue(out var destination)
    ? context.Mapper.Map(source.Address, destination.Address)
    : context.Mapper.Map<AddressDto, Address>(source.Address);
```

Configuration methods such as `Auto`, `Ignore`, `Value`, `Map`, `Create` and
`Update` are not used inside `Convert`; its body is normal C#.

`Convert` can also map a collection as a whole. See the
[collection recipe](recipes.md#map-a-collection-with-custom-code).

Tuples support first-class convention-based and explicit mapping. Use `Convert`
for a tuple only when the entire transformation is clearer as custom code;
otherwise see [Tuple mapping](tuple-mapping.md).

A mapping uses either `Convert` or destination-selection/member rules, not
both. See [Dependency injection and `IMapper`](runtime-dispatch.md) for nested
lookup and the lifetime of `context.Mapper`.
