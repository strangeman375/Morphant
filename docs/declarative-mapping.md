# Declarative mapping

A bare mapping uses [automatic conventions](conventions.md):

```csharp
builder.Map<Customer, CustomerDto>();
```

When conventions are not enough, use [`Construct`](api/construct.md) or
[`Resolve`](api/resolve.md) to choose the destination and
[`Members`](api/members.md) to configure its members. See
[Choose a configuration method](api/README.md) for availability and overloads.

`Configure` is analyzed at compile time. Keep mapper settings and `Map`
registrations in an unconditional sequence, and keep each mapping on the
fluent chain returned by `Map`; do not store or pass either builder.

## Choose the destination

Each mapping can use at most one of these methods:

| Method | When it applies | Use it for |
|---|---|---|
| [`Construct`](api/construct.md) | No destination is available | Explicit constructor arguments |
| [`Resolve`](api/resolve.md) | Every Create and Update | Choosing reuse or replacement |
| [`ConstructUsing`](api/construct-using.md) | No destination is available | A factory or another custom creation method |
| [`ResolveUsing`](api/resolve-using.md) | Every Create and Update | Custom reuse or replacement logic |

`Construct` and `Resolve` are available when the destination has a supported
constructor. Their inline lambdas describe construction:

```csharp
builder.Map<OrderDto, Order>()
    .Construct(source => new(source.Id));
```

`Resolve` can use the existing destination:

```csharp
builder.Map<SnapshotDto, Snapshot>()
    .Resolve((source, previous) =>
        previous.HasValue && previous.Value.Id == source.Id
            ? previous
            : new(source.Id));
```

`ConstructUsing` and `ResolveUsing` are ordinary synchronous C# callbacks.
Use them for factories, caches, interfaces, abstract destinations or other
ready-made results:

```csharp
builder.Map<OrderDto, IOrder>()
    .ConstructUsing(source =>
        orderFactory.Create(source.Id));
```

Their context-aware overloads receive `MappingContext`, whose `Mapper` can
invoke another registered mapping.

If `ConstructUsing` or `ResolveUsing` returns `null`, that is the final mapping
result. `Members` is skipped, and null handling is not applied again.

## Map destination members

```csharp
builder.Map<OrderDto, Order>()
    .Members((source, _) => new()
    {
        DisplayName = source.Name,
        Revision = Auto(),
        LegacyCode = Ignore()
    });
```

| Rule | Meaning |
|---|---|
| Ordinary expression | Use that value |
| [`Auto()`](api/declarative-expressions.md) | Apply exact-name convention to this member |
| [`Ignore()`](api/declarative-expressions.md) | Leave this member unchanged |
| [`Map(...)`](api/declarative-expressions.md) | Run an explicit nested mapping |
| [`Value<T>(value)`](api/declarative-expressions.md) | Pin the exact receiving type |

Members not mentioned in `Members` follow the configured
[`MemberSelection`](settings/member-selection.md).

Use `Value<T>` when target typing is otherwise ambiguous, for example for
boxing, nullable annotations, lambdas or overloaded constructors:

```csharp
.Members((source, _) => new()
{
    Payload = Value<object>(source.PayloadId),
    Label = Value<string?>(source.Label)
});
```

On C# 12 and newer, `Value<T>` also supplies the collection target type:

```csharp
.Members((source, _) => new()
{
    Values = Value<int[]>([.. source.Values])
});
```

## Existing destinations

In `Resolve` and the overloads of `Members` that receive `previous`,
`Option<TDestination>` indicates whether Morphant has an existing destination
value to reuse. It is `Some(destination)` when one is available and `None`
otherwise:

```csharp
if (previous.TryGetValue(out var destination))
{
    // An existing destination is available.
}
```

`None` can mean Create or an Update whose destination is `null` and handled by
creating a replacement. Use the context overload and check
`context.Operation` when that distinction matters.

An Update can reuse the supplied instance or select a replacement. The caller
must always use the returned result.

## Lambda rules

Code passed to `Construct`, `Resolve` and `Members` follows these rules:

- pass an inline lambda;
- use expressions, initialized locals, complete `if`/`switch` branches,
  returns and throws;
- do not mutate `previous` or `result`;
- do not capture local variables declared inside `Configure`;
- do not rely on the order of independent member expressions or side effects.

Only the selected branch is evaluated. Each expression needed by that branch
is evaluated at most once; expressions used only by unselected branches or
inapplicable rules are not evaluated. A local can express an explicit
dependency. Use [`Convert`](api/convert.md) when loops, mutation, `try`,
strict statement order or another ordinary C# algorithm would be clearer.

See [Nested mapping](nested-mapping.md) for `Map`, `Create` and `Update` rules,
and [Constructor selection](settings/constructor-selection.md) for convention
construction.
