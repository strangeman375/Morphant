# Mapping recipes

These examples show common mappings that need more than the default
conventions.

## Rename a member

Configure the renamed member and leave the rest to conventions:

```csharp
builder.Map<CustomerDto, Customer>()
    .Members((source, _) => new()
    {
        Name = source.DisplayName
    });
```

## Fill one constructor parameter explicitly

Use `ByConvention()` for the selected constructor and override only the value
that needs a custom expression:

```csharp
builder.Map<OrderDto, Order>()
    .Construct(source => new(
        ByConvention(),
        new()
        {
            tenantId = source.Tenant.ExternalId
        }));
```

## Create an interface through a factory

`ConstructUsing` is ordinary synchronous C# and can use mapper dependencies:

```csharp
builder.Map<OrderDto, IOrder>()
    .ConstructUsing(source =>
        orderFactory.Create(source.Id));
```

Use `ResolveUsing` instead when the factory also decides whether to reuse an
existing destination.

## Map a nested object

Nested mappings are explicit:

```csharp
builder.Map<OrderDto, Order>()
    .Members((source, _) => new()
    {
        Address = Map<Address>(source.Address)
    });
```

Register the `AddressDto -> Address` mapping with DI as well. See
[Nested mapping](nested-mapping.md) for Create and Update selection.

## Map a collection with custom code

Core v0 has no automatic collection mapping. A `Convert` mapping can map a
collection as a whole:

```csharp
builder.Map<IReadOnlyList<OrderDto>, List<Order>>()
    .Convert((source, _, context) =>
        source is null
            ? new List<Order>()
            : source
                .Select(context.Mapper.Map<OrderDto, Order>)
                .ToList());
```

For larger algorithms, keep the callback small and call an application method
that returns the final destination.
