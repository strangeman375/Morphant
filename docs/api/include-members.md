# `IncludeMembers`

Adds selected nested source objects to constructor and destination-member
conventions. It does not start a nested mapping.

## Availability

Import `Morphant` to use `IncludeMembers` on a registered mapping pair. It is
generated only where it can affect conventions: the source is not an enum, a
built-in type such as `int` or `string`, an array or a delegate, and the
destination has constructor or member conventions. Elsewhere the call does not
compile. It cannot be combined with [`Convert`](convert.md).

## Overload

| Call | Description |
|---|---|
| `IncludeMembers(selector)` | Include one path or the paths in an anonymous object |

| Parameter | Description |
|---|---|
| `selector` | Inline property or field path rooted in `source`, or an anonymous object containing several paths |

The selector receives the same source as [`Members`](members.md): a non-null
reference even after `Map<Source?, Destination>()`, and the value of a
nullable struct. The method returns the same mapping builder.

```csharp
builder.Map<Order, OrderDto>()
    .IncludeMembers(source => new
    {
        source.Customer,
        source.Audit
    });
```

The root source keeps precedence. Deep and conditional paths are supported;
computed expressions, method calls, casts, and indexers are not.

See [Include nested source members](../include-members.md) for matching,
precedence, nullability, and validation.
