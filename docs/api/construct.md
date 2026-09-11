# `Construct`

Describes destination construction when no destination is available. Use it
when constructor conventions are insufficient but a constructor call still
best expresses the mapping.

## Availability

`Construct` is available when the destination exposes at least one accessible
constructor with supported by-value parameters. For interfaces, abstract
types, scalar destinations, or factory-based creation, use
[`ConstructUsing`](construct-using.md), [`ResolveUsing`](resolve-using.md), or
[`Convert`](convert.md).

For BCL tuple destinations, `new(...)` accepts one argument per tuple element,
including long tuples.

## Overloads

Use an inline lambda.

| Callback | Use when |
|---|---|
| `source => construction` | Construction depends only on the source |
| `(source, context) => construction` | Construction also depends on the current operation |

Return a construction expression such as `new(source.Id)` or
`new OrderConstruction(source.Id)`. See [callback inputs](README.md#callback-inputs).

```csharp
builder.Map<OrderDto, Order>()
    .Construct(source => new(source.Id));
```

The explicit name refers to the generated construction type, such as
`OrderConstruction`. To return an ordinary object such as
`new Order(source.Id)`, use [`ConstructUsing`](construct-using.md).

`Construct` runs for Create and for Update when no usable destination exists.
It can be combined with [`Members`](members.md), but not with another
destination method or `Convert`. An automatic argument can use a corresponding
member rule; see [constructor parameters](members.md#constructor-parameters).

Related: [declarative expressions](declarative-expressions.md),
[constructor selection](../settings/constructor-selection.md),
[tuple mapping](../tuple-mapping.md).
