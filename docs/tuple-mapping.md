# Tuple mapping

Morphant supports C# value tuples and `System.Tuple` as ordinary mapping
sources and destinations. Tuple conventions use semantic element names. There
is no positional fallback.

```csharp
builder.Map<Customer, (string Name, int Id)>();
builder.Map<(int Id, string Name), CustomerDto>();
builder.Map<(int X, int Y), (int Y, int X)>();
```

The last mapping swaps the values because `X` maps to `X` and `Y` maps to `Y`.

## Combining sources, destinations, and user state

A tuple source can combine several input objects or pass extra data needed
by one mapping call:

```csharp
builder.Map<
    (Order Order, Customer Customer, decimal TaxRate),
    OrderDto>()
    .Members(source => new()
    {
        CustomerName = source.Customer.Name,
        Total = source.Order.Subtotal * (1m + source.TaxRate)
    });

var dto = mapper.Map<
    (Order Order, Customer Customer, decimal TaxRate),
    OrderDto>((order, customer, taxRate));
```

A tuple destination can return several mapped results from one call:

```csharp
builder.Map<Order, (OrderDto Order, AuditDto Audit)>()
    .Members(source => new()
    {
        Order = Map<OrderDto>(source),
        Audit = Map<AuditDto>(source)
    });

var (orderDto, auditDto) = mapper.Map<
    Order,
    (OrderDto Order, AuditDto Audit)>(order);
```

Configure each output explicitly. Nested `Map` calls can reuse registered
mappings; pass any extra data in the source tuple of the nested mapping that
needs it.

## Named and unnamed elements

A named destination element follows the normal member convention: it needs one
compatible source member with the exact, case-sensitive name. Constructor
conventions also allow the normal unique case-insensitive match.

An unnamed element has no semantic name. Use its `ItemN` name in an explicit
rule; Morphant never treats `ItemN` as a positional convention:

```csharp
builder.Map<Source, (int, string)>()
    .Members(source => new()
    {
        Item1 = source.Id,
        Item2 = source.DisplayName
    });
```

Partially named tuples combine both rules: named elements can use convention;
unnamed elements need an explicit value. `Auto()` and `ByConvention()` do not
turn `ItemN` into a semantic name.

## Construction

For a tuple destination, `new(...)` inside `Construct` or `Resolve` accepts one
argument per element:

```csharp
builder.Map<Source, (int Id, string Name)>()
    .Construct(source => new(
        source.Id,
        source.DisplayName));
```

`Members` can provide some or all final constructor values as well:

```csharp
builder.Map<Source, (int Id, string Name)>()
    .Construct(source => new(source.Id, source.RawName))
    .Members(source => new()
    {
        Name = Normalize(source.RawName)
    });
```

A `Members` value available before construction overrides the corresponding
constructor argument. The overridden expression is not evaluated.

## Update

`ValueTuple` fields are mutable. Update changes the selected tuple value and
returns it; because the destination is passed by value, always keep the return
value:

```csharp
current = mapper.Map(source, current);
```

Unmatched fields retain their destination values. Explicit `Members` rules can
replace individual fields or run nested mappings just as they do for other
mutable destinations.

`System.Tuple` elements are read-only. A normal Update preserves the existing
tuple instance and does not reconstruct it for scalar rules. An explicit nested
`Update` may still mutate an eligible referenced object stored in an element.
Scalar rules can apply while Morphant creates or replaces the tuple.

## Runtime factories

`ConstructUsing` and `ResolveUsing` return an already constructed tuple.
`Members` can change writable `ValueTuple` fields or update referenced objects
in place. It cannot replace read-only `System.Tuple` elements; such a rule
produces [`MORPH0042`](diagnostics/MORPH0042.md).

See [factory result rules](api/construct-using.md#factory-result) for null
results and other restrictions shared with ordinary objects.

## `System.Tuple` and `ITuple`

`System.Tuple<T...>` has no semantic element names. Its `ItemN` elements can be
used in explicit `Construct`, `Resolve`, or `Members` rules, and Morphant can
create the result whenever every required element has a final value. A factory
is not required.

Custom `ITuple` implementations follow ordinary object-mapping conventions.

## Presentation conflicts

Use consistent tuple names, nullable annotations and `dynamic`/`object` choices
for each mapping pair within a mapper and its connected base configuration.
For conflicting declarations, see [`MORPH0056`](diagnostics/MORPH0056.md).

Related: [Conventions](conventions.md),
[Create and Update](create-and-update.md), and
[Declarative mapping](declarative-mapping.md).
