# `IncludeMembers`

Adds selected nested source objects to constructor and destination-member
conventions. It does not start a nested mapping.

## Availability

Import `Morphant` to use `IncludeMembers` on a registered mapping pair.
It cannot be combined with [`Convert`](convert.md).

## Overload

| Call | Description |
|---|---|
| `IncludeMembers(selector)` | Include one path or the paths in an anonymous object |

| Parameter | Description |
|---|---|
| `selector` | Inline property or field path rooted in `source`, or an anonymous object containing several paths |

The selector receives a non-null reference source even when the registration
uses `Map<Source?, Destination>()`. A nullable value source retains its
`Nullable<T>` type. The method returns the same mapping builder.

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
