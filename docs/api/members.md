# `Members`

Defines explicit destination-member rules. Use it for renames, computed
values, ignored members, or explicit nested mappings while leaving other
members to the configured convention.

## Availability

`Members` is available when the destination has at least one supported member:
an assignable property or field, or an eligible readable reference member
that can be updated in place.

## Overloads

Each overload accepts a `members` callback and returns the same mapping
builder. The callback must be an inline lambda.

| Callback | Available information |
|---|---|
| `source => plan` | Source |
| `(source, previous) => plan` | Source and existing destination |
| `(source, previous, result) => plan` | Source, existing destination, and selected result |
| `(source, previous, result, context) => plan` | All of the above plus current operation |

| Callback value | Description |
|---|---|
| `source` | Non-null source after null-source handling |
| `previous` | `Option<TDestination>` containing the supplied destination, when available |
| `result` | Non-null destination selected for this operation |
| `context` | Declarative context; `Operation` is Create or Update |
| Return value | Object initializer describing destination-member rules |

```csharp
builder.Map<OrderDto, Order>()
    .Members((source, _) => new()
    {
        Name = source.DisplayName,
        Revision = Auto(),
        LegacyCode = Ignore()
    });
```

`Members` runs after destination selection. Unmentioned members follow
[`MemberSelection`](../settings/member-selection.md). It can be combined with
one destination method, but not with `Convert`.

Related: [declarative expressions](declarative-expressions.md),
[nested mapping](../nested-mapping.md).
