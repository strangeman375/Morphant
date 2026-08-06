# Nested mapping

Declarative `Construct` and `Members` plans can dispatch another registered
mapping explicitly. Morphant never turns a convention rule into a nested
mapping automatically.

## Forms

| Declarative form | Destination pair | Nested operation |
|---|---|---|
| `Map(source)` | Inferred from the target | `Create` |
| `Map<TDestination>(source)` | `TDestination` | `Create` |
| `Map(source, destination)` | Inferred from the target | `Update` |
| `Map<TDestination>(source, destination)` | `TDestination` | `Update` |

The first argument's static type selects the nested source type. The generic
destination must have a warning-free implicit conversion to the member or
constructor parameter receiving the result. An explicit destination is never
inserted by the generator.

Use the generic form when no member or constructor parameter supplies target
typing, including a declarative local:

```csharp
var address = Map<Address>(source.Address);
```

`var address = Map(source.Address)` is unsupported because it does not define
one nested destination pair at the local declaration.

```csharp
builder.Map<OrderDto, Order>()
    .Construct((source, previous) => new(
        source.Id,
        Map<Address>(source.Address)))
    .Members((source, previous, result) => new()
    {
        Customer = Map<Customer>(source.Customer),
        ShippingAddress = previous.HasValue
            ? Map(source.ShippingAddress,
                previous.Value.ShippingAddress)
            : Map(source.ShippingAddress),
        BillingAddress = Map(
            source.BillingAddress,
            result.BillingAddress)
    });
```

The overload determines the nested operation independently of the outer
operation. In particular, `Map(source, null)` is an `Update` call with a null
destination; the nested pair applies its own null-destination policy.

The nested result is authoritative. An Update mapping may reuse its argument
or return a replacement, and that returned value is assigned to the outer
target.

## Execution

Arguments are evaluated once, left to right in source order, including
reordered named arguments. Equivalent declarative calls participate in the
same path-sensitive dependency graph as other `Construct` and `Members`
expressions.

Nested dispatch uses the scoped `IMapper` from the current mapping chain. It
creates a new `MappingContext` frame with the nested operation while retaining
the same application-wide service lookup and mapping scope. Exceptions from
argument evaluation or the nested mapper propagate normally.

Each exact `ITypeMapper<TSource, TDestination>` pair must currently be
registered manually with the application's service provider. See
[Manual mapping](manual-mapping.md) for the scoped mapper lifecycle.

`Auto()` only performs a direct warning-free convention conversion. Registering
a child mapping does not make `Auto()` dispatch it implicitly.
