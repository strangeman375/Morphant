# Nested mapping

Nested mapping is always explicit. A convention or `Auto()` rule never starts
another mapping automatically. The
[declarative expressions reference](api/declarative-expressions.md) lists
every `Map`, `Create`, and `Update` form.

## Forms

| Form | Operation |
|---|---|
| `Map(...)` | Select Create or Update from whether a non-null outer destination was supplied |
| `Create(source)` | Always nested Create |
| `Update(source, destination)` | Always nested Update |

The source can be inferred from the destination member name or supplied as an
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

The nested source and destination types identify one exact
`ITypeMapper<TSource, TDestination>` mapping. Its result must be implicitly
convertible to the destination member or constructor parameter.

## How `Map` chooses an operation

`Map` follows the supplied outer destination:

| Outer call | Nested operation |
|---|---|
| Create, or Update with a null outer destination | Create |
| Update with a non-null outer destination | Update |

A writable nested member's current value is passed to nested Update even when
it is `null`. That mapping applies its own
[`NullDestinationHandling`](settings/null-handling.md#null-update-destination),
and its operation remains Update.

When `Resolve` or `ResolveUsing` selects a replacement, nested member rules use
the replacement's member values. The returned nested result is assigned back
to the writable member.

Use explicit `Create` or `Update` when the operation must not be selected this
way.

## Read-only members

A readable reference-type member can be updated in place even without a
setter when its current value can be passed to a nested Update:

`OrderMembers` below is the generated callback result type; import the
namespace shown for it by the IDE. Pass a member selected through this object
(`members.Address`), not through the callback's `result` destination.

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

`Update(..., members.Member)` must appear as a statement on its own. If the
current member value is `null`, the nested call is skipped because a
replacement could not be assigned back. Otherwise, nested Update runs in
place and its returned value is discarded because the member cannot be
reassigned.

## Registration and result

When using the application `IMapper`, every nested source/destination mapping
must also be registered with DI.

Except for the standalone read-only form above, always use the nested result.
A nested Update may reuse its destination or return a replacement; writable
destination members receive the returned value.

See [Dependency injection and `IMapper`](runtime-dispatch.md) for registration
and [Exceptions](exceptions.md) for lookup or destination-type failures.
