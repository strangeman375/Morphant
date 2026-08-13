# Nested mapping

Nested mapping is always explicit. A convention or `Auto()` rule never starts
another mapping automatically.

## Forms

| Form | Operation |
|---|---|
| `Map(...)` | Create or Update according to the current outer branch |
| `Create(source)` | Always nested Create |
| `Update(source, destination)` | Always nested Update |

The source can be inferred from the destination-member name or supplied as an
expression. Add a generic destination argument when it cannot be inferred:

```csharp
builder.Map<OrderDto, Order>()
    .Construct(source => new(
        source.Id,
        Map<Address>(source.Address)))
    .Members((source, _) => new()
    {
        Customer = Map(),
        BillingAddress = Map<Address>(source.InvoiceAddress),
        Audit = Create(source.AuditSnapshot)
    });
```

The nested source and destination types select an exact registered mapping
pair. The result must be implicitly convertible to its final member or
constructor-parameter type.

## Adaptive `Map`

`Map` follows the applicable outer branch:

| Outer branch | Nested operation |
|---|---|
| Create or Update without a destination | Create |
| Update with an existing destination member | Update |

For a writable member, the result of nested Update is assigned back to that
member. This preserves a replacement returned by the nested mapping.

Use explicit `Create` or `Update` when the nested operation must not follow the
outer branch.

## Read-only members

An eligible readable reference member can be updated in place even when the
outer member is not writable:

```csharp
.Members((source, _) =>
{
    var members = new OrderMembers
    {
        Name = source.Name
    };

    Update(source.Address, members.Address);
    return members;
});
```

This form is only for standalone `Update(..., members.Member)`. If the current
member value is `null`, the nested call is skipped because a replacement could
not be assigned back.

## Registration and result

When using the application `IMapper`, every nested pair must be registered with
DI like any other mapping pair. Nested calls remain in the current mapping
scope and use the same registrations.

The nested result is authoritative. A nested Update may reuse its destination
or return a replacement; writable outer targets receive that returned value.

See [Runtime dispatch and DI](runtime-dispatch.md) for registration and
[Exceptions](exceptions.md) for lookup or destination-type failures.
