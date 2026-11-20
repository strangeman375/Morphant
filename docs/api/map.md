# `Map`

Registers one exact source/destination pair. Use a bare `Map` when conventions
are sufficient, then chain only the rules that differ.

## Availability

`Map` is available on `MapperBuilder` inside `Configure`. Both types must form
a supported, accessible mapping pair.

## Mapper declarations

Mark the mapper with `[MorphantMapper]`, declare it `partial`, and derive from
`TypeMapper`:

```csharp
[MorphantMapper]
public partial class OrderMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<OrderDto, Order>();
}
```

For a nested mapper, declare each containing type partial.

Mappers can configure the same pair with different rules. Registrations in one
assembly must agree on tuple element names, nested nullable annotations and
`dynamic`. See [conflicting pair presentations](../diagnostics/MORPH0056.md)
and [configuration inheritance](../configuration-inheritance.md).

## Call forms

| Call | Meaning |
|---|---|
| `Map<TSource, TDestination>()` | Register the pair and inherit `MappingMode` |
| `Map<TSource, TDestination>(mappingMode)` | Register the pair with an operation override |

| Parameter | Description |
|---|---|
| `TSource` | Exact source type |
| `TDestination` | Exact destination type |
| `mappingMode` | Compile-time `MappingMode` value; defaults to `Default` |

The method returns the pair builder used by the remaining configuration
methods.

```csharp
builder.Map<OrderDto, Order>()
    .Members(source => new()
    {
        Name = source.DisplayName
    });
```

This method is unrelated to application-side `IMapper.Map` and the nested
declarative `Map` expression. See [Create and Update](../create-and-update.md)
and [declarative expressions](declarative-expressions.md), respectively.

Related: [conventions](../conventions.md),
[mapping modes](../settings/mapping-mode.md).
