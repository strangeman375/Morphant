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

It is also available for BCL tuple destinations. Their generated plan exposes
one intrinsic logical constructor with one flat parameter per element, even
when the CLR representation uses `Rest`.

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
| Return value | A supported destination constructor expression |

```csharp
builder.Map<OrderDto, Order>()
    .Construct(source => new(source.Id));
```

`Construct` runs for Create and for Update when no usable destination exists.
It can be combined with [`Members`](members.md), but not with another
destination method or `Convert`.

Related: [declarative expressions](declarative-expressions.md),
[constructor selection](../settings/constructor-selection.md),
[tuple mapping](../tuple-mapping.md).
