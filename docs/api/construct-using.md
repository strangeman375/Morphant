# `ConstructUsing`

Runs an ordinary synchronous callback when no destination is available. Use it
for factories, interfaces, abstract destinations, caches, or other creation
that is not a destination constructor expression.

## Availability

`ConstructUsing` is available for every valid mapping pair.

## Overloads

Use an inline lambda, method group, or accessible delegate.

| Callback | Use when |
|---|---|
| `source => destination` | Creation needs only the source |
| `(source, context) => destination` | Creation also needs `MappingContext` |

Return the destination object. See [callback inputs](README.md#callback-inputs).

```csharp
builder.Map<OrderDto, IOrder>()
    .ConstructUsing(source =>
        orderFactory.Create(source.Id));
```

## Factory result

A `null` callback result is final: Morphant skips `Members` and does not apply
null handling again. A non-null result can continue through
[`Members`](members.md), but it is already constructed and remains the selected
result. Morphant can assign settable members or run an eligible nested `Update`;
an `init`-only member must already be initialized in the returned result.
Configuring it in `Members` produces
[`MORPH0042`](../diagnostics/MORPH0042.md).

For tuple destinations, writable `ValueTuple` elements and eligible nested
`Update` statements remain applicable. A scalar rule for a read-only
`System.Tuple` element produces `MORPH0042`. See
[Tuple mapping](../tuple-mapping.md).

`ConstructUsing` cannot be combined with another destination method or
`Convert`.

Related: [`ResolveUsing`](resolve-using.md),
[dependency injection and `IMapper`](../runtime-dispatch.md).
