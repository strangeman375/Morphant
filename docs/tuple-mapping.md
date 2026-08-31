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
The source and destination have the same underlying `ValueTuple<int, int>`
type, but element names still control mapping behavior.

## Combining sources, destinations, and user state

Tuples are Morphant's typed composition mechanism when all inputs and outputs
are known at compile time. A tuple source can combine several input objects
into a multi-source mapping or carry call-specific user state that is not
stored on those objects:

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

In both cases, Morphant treats the complete tuple as one statically registered
source/destination pair. It does not automatically merge existing mappings or
fan out to every tuple element. Configure that composition explicitly; nested
`Map` rules can reuse independently registered element mappings. User state
must likewise be included in the source tuple of each nested mapping that
needs it rather than being propagated as ambient context.

Root null-handling settings apply to the complete tuple. Null tuple elements
follow the ordinary member-nullability and explicit-rule behavior.

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

For a tuple destination, `Construct` and `Resolve` expose one parameter per
element. Long tuples use the same flat callback shape; their eighth and later
elements remain directly addressable by name or as `ItemN`. `Rest` is never
configured.

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

When `Construct` or a construction branch of `Resolve` overlaps `Members`, the
`Members` rule wins for that element and the overridden expression is not
evaluated. Surviving expressions keep declarative evaluation order and run
once. If a rule reads `result`, Morphant creates that initial tuple first
because the value is observable.

Tuple construction is intrinsic, not a choice among declared constructors.
An explicit pair-level `ConstructorSelection` therefore produces
[`MORPH0023`](diagnostics/MORPH0023.md); inherited defaults have no effect.

When a statement-bodied callback needs to instantiate its generated result
type explicitly, tuple templates have readable names. For example:

```csharp
using ResultMembers =
    Morphant.Generated.Tuples.ValueTuple2_Int32_String
        .Tuple_Id_Name_Members;
```

The namespace starts with `ValueTupleN` or `SystemTupleN` and identifies the
element types. C# predefined scalar types use short CLR names such as `Int32`;
every other named type uses `Type_` followed by its fully qualified name. A
`ValueTuple` template name identifies its element names; a `System.Tuple`
template uses `TupleConstructorParameters`, `TupleConstruction`, or
`TupleMembers`. Nested tuple contracts are represented recursively. Prefer IDE
completion instead of constructing these names by hand.

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

`ConstructUsing` and `ResolveUsing` return an authoritative result. Morphant
does not compare it with `previous` or create another tuple to apply read-only
element rules. A non-null result receives only member operations that can run
on that result:

- writable `ValueTuple` fields may be assigned;
- an eligible nested `Update` may run on a referenced element;
- a scalar rule for a read-only `System.Tuple` element produces
  [`MORPH0042`](diagnostics/MORPH0042.md).

A null factory result is final and skips `Members`, as for every other
destination type.

## `System.Tuple` and `ITuple`

`System.Tuple<T...>` has no semantic element names. Its `ItemN` elements can be
used in explicit `Construct`, `Resolve`, or `Members` rules, and Morphant can
create the result whenever every required element has a final value. A factory
is not required.

A concrete custom implementation of `System.Runtime.CompilerServices.ITuple`
maps through its declared static constructors, properties, and fields like any
other type. Morphant does not discover arbitrary elements by calling `Length`
and the indexer at runtime. When the mapping root is the `ITuple` interface,
use an explicit expression or `Convert` for indexer access.

## Presentation conflicts

C# tuple element names do not create different runtime types, but they define
the names available in Morphant configuration. All registrations with the
same underlying source and destination types must therefore use the same
recursive tuple element names. A conflict produces
[`MORPH0056`](diagnostics/MORPH0056.md).

Use one consistent presentation, or introduce wrapper types when the same
underlying pair needs different meanings.

Related: [Conventions](conventions.md),
[Create and Update](create-and-update.md), and
[Declarative mapping](declarative-mapping.md).
