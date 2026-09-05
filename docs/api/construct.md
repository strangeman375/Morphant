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

Each overload accepts a `construct` callback and returns the same mapping
builder. The callback must be an inline lambda.

| Callback | Use when |
|---|---|
| `source => construction` | Construction depends only on the source |
| `(source, context) => construction` | Construction also depends on the current operation |

| Callback value | Description |
|---|---|
| `source` | Non-null source after null-source handling |
| `context` | Declarative context; `Operation` is Create or Update |
| Return value | A constructor expression using `new(...)` without a type name |

```csharp
builder.Map<OrderDto, Order>()
    .Construct(source => new(source.Id));
```

Use [`ConstructUsing`](construct-using.md) to return an ordinary object such
as `new Order(source.Id)` instead.

`Construct` runs for Create and for Update when no usable destination exists.
It can be combined with [`Members`](members.md), but not with another
destination method or `Convert`.

Related: [declarative expressions](declarative-expressions.md),
[constructor selection](../settings/constructor-selection.md),
[tuple mapping](../tuple-mapping.md).
