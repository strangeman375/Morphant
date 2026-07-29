# Mapping modes

`MappingMode` controls whether a generated mapping can create a new
destination, update an existing destination, or do both.

## Configure a default

Set the mapper-level mode when most registrations use the same behavior:

```csharp
protected override void Configure(MapperBuilder builder)
{
    builder.MappingMode(MappingMode.MapNew);

    builder.Map<Order, OrderDto>();
    builder.Map<Customer, CustomerDto>();
}
```

Both registrations inherit `MapNew`.

## Override one mapping

Pass a mode to `Map<TSource, TDestination>()` to override the mapper-level
value:

```csharp
protected override void Configure(MapperBuilder builder)
{
    builder.MappingMode(MappingMode.MapNew);

    builder.Map<Order, OrderDto>();
    builder.Map<Customer, CustomerDto>(MappingMode.MapExisting);
    builder.Map<Product, ProductDto>(MappingMode.MapNewAndExisting);
}
```

The effective value is selected in this order:

1. A non-`Default` value passed to `Map<TSource, TDestination>()`.
2. A non-`Default` mapper-level value passed to `builder.MappingMode(...)`.
3. `MappingMode.MapNewAndExisting`.

Root-level settings apply to the whole mapper, regardless of whether the
setting call appears before or after its mapping registrations.

## Mode behavior

| Effective mode | `Map(source, context)` | `Map(source, destination, context)` |
|---|---|---|
| `MapNew` | Maps to a new destination | Throws `NotSupportedException` |
| `MapExisting` | Throws `NotSupportedException` | Maps to the supplied destination |
| `MapNewAndExisting` | Maps to a new destination | Maps to the supplied destination |

`Default` means inheritance; it is not an operation by itself.

Every generated mapping continues to implement the single
`ITypeMapper<TSource, TDestination>` interface with both overloads. This keeps
runtime resolution uniform. Invoking an overload excluded by the effective
mode fails immediately in the generated mapper.

Mapping mode expressions must be compile-time constants composed only from
the defined `MapNew` and `MapExisting` flags.

## Invalid values

If the effective mode is not a compile-time constant or contains undefined
flags, Morphant still generates the `ITypeMapper<TSource, TDestination>`
implementation for the registered pair. Both mapping overloads throw
`NotSupportedException` when invoked.

An explicit valid mapping-level mode still overrides an invalid mapper-level
value. A mapping that uses `Default` inherits the invalid mapper-level value
and therefore has two throwing overloads.
