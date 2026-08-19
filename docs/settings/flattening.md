# Flattening

`Flattening` controls convention lookup through nested source paths. Its
default is `Auto`.

| Value | Behavior |
|---|---|
| `Auto` | Match joined nested property and field names |
| `None` | Use direct convention source members only |

```csharp
builder.Map<Order, OrderDto>()
    .Flattening(Flattening.None);
```

Configure the assembly default with `MorphantFlattening`:

```xml
<PropertyGroup>
  <MorphantFlattening>None</MorphantFlattening>
</PropertyGroup>
```

The setting is available at assembly, mapper and mapping levels and follows
the standard [precedence and inheritance rules](README.md). `Default`
continues to the next level. A mapping-level `Auto` can therefore override an
assembly or mapper default of `None`.

`Flattening` affects constructor and destination-member conventions. It does
not apply to a manual `Convert` mapping. See
[Flatten nested source members](../flattening.md) for matching, precedence and
nullable behavior.
