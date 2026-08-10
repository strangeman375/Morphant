# Nested mapping

This page documents the implemented core v0 API. Current review status and
remaining boundaries are tracked in the
[mapping API roadmap](../MAPPING_API_IMPLEMENTATION_PLAN.md).

Structured `Construct` / `Resolve` and `Members` plans can dispatch another
registered mapping. Morphant never turns a convention rule or `Auto()` into a
nested mapping automatically.

## Forms

| Declarative form | Source | Destination | Nested operation |
|---|---|---|---|
| `Map()` | Inferred from the target name | Inferred from the target | Follows the applicable outer branch |
| `Map<TDestination>()` | Inferred from the target name | `TDestination` | Follows the applicable outer branch |
| `Map(source)` | Explicit expression | Inferred from the target | Follows the applicable outer branch |
| `Map<TDestination>(source)` | Explicit expression | `TDestination` | Follows the applicable outer branch |
| `Create(source)` | Explicit expression | Inferred from the target | `Create` |
| `Create<TDestination>(source)` | Explicit expression | `TDestination` | `Create` |
| `Update(source, destination)` | Explicit expression | Inferred from the target | `Update` |
| `Update<TDestination>(source, destination)` | Explicit expression | `TDestination` | `Update` |

The short `Map` forms are intended for the common case. The explicit
`Create` and `Update` forms are useful when a nested operation must not follow
the outer mapping.

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

The source expression's static type selects the nested source type. A generic
destination must have a warning-free implicit conversion to the member or
constructor parameter receiving the result.

`Map<TDestination>` fixes the nested result type, not the exact final target
type. Its generic marker flows through the common `Map` marker surface, and
the mapped result is then checked with the normal warning-free implicit C#
conversion. This permits, for example, a concrete nested result to enter an
`object` or interface target without nullable warnings. Use
`Value<T>(value)` when the final receiving type itself must be exact.

An explicit cast to generated `Member<T>` or `ConstructorParameter<T>` may be
used to disambiguate C# binding. The cast is compile-time context only and is
removed during lowering, while its equivalent conversion to the actual `T` is
preserved. No wrapper object reaches runtime.

For parameterless `Map`, Morphant finds a readable source property or field
from the target name. Member names are matched exactly. A constructor
parameter is first associated with a readable destination member by exact
name, then by one unique case-insensitive match. When an association exists,
the destination member name is used for source lookup; otherwise a
no-previous branch uses the parameter name. An existing Update branch requires
an associated readable destination member and is unsupported without one.

Both generic and non-generic markers can be stored in declarative locals. The
local remains an alias for the marker and obtains its target context from the
final member or constructor parameter:

```csharp
var address = Map(source.Address);
return new() { Address = address };
```

Reusing one adaptive local for different current destination objects is
ambiguous in an Update branch and is unsupported.

## Adaptive operation

`Map` uses nested Create when the applicable outer branch has no previous
destination. In an existing outer Update branch it uses nested Update:

| Target | No-previous branch | Existing Update branch |
|---|---|---|
| Writable member | Nested Create; assign the result | Nested Update with the actual `result.Member`; assign the result |
| Constructor parameter | Nested Create; pass the result | Nested Update with the corresponding readable member of `previous`; pass the result |

The actual selected result is used for a writable member, including a
replacement selected by `Resolve`. A constructor parameter must use
`previous` because the new result does not exist yet. If no readable
destination member corresponds to that parameter, adaptive Update is
unsupported; use explicit `Create` or `Update` instead.

When a public Update call with a null destination is normalized to the
no-previous branch by `NullDestinationHandling.Create`, adaptive `Map` uses
nested Create. An explicit `Update(source, null)` remains an ordinary nested
Update, and the nested pair applies its own null-destination policy.

For `Map<TDestination>`, an existing destination value must be null or
runtime-compatible with `TDestination`. A non-null incompatible value throws
`NestedDestinationTypeMismatchException`; it is not silently converted to
null or replaced. A null value can flow into nested Update only when
`TDestination` can represent null. A broad target containing null cannot be
converted to a non-nullable value destination, so Morphant throws the same
typed mismatch exception and nested dispatch is not entered.

The nested result is authoritative for writable targets. A nested Update may
reuse its destination or return a replacement, and the returned value is
assigned to the outer target.

## Read-only members

A generated `DestinationMembers` surface exposes a readable non-writable
destination member as a get-only marker only when its type is an eligible
non-opaque reference-type nested destination in core v0. This can include true
get-only properties, properties whose ordinary setter is inaccessible to
generated code, and accessible `readonly` fields. Read-only value types,
opaque types, and other unsupported nested roots do not produce a proxy. For a
destination without constructor surface, `init`-only properties remain
creation-only and are not converted into read-only proxies. A proxy can select
an in-place nested Update without exposing the actual outer result for
mutation:

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

The standalone form is accepted only for `Update(..., members.Member)` when
`Member` is get-only in the generated surface. Morphant reads the destination
member from the actual selected result exactly once. If it is null, the nested
call is skipped and the source expression is not evaluated. Otherwise Morphant
performs an ordinary nested Update and discards its return value because the
outer member cannot accept a replacement.

Read-only markers do not participate in conventions, `Auto()`, or unmapped
member validation, and they do not make the corresponding destination member
writable.

## Declarative inputs

`previous` and `result` are read-only information sources in `Resolve` and
`Members`. Assignments, increments, decrements, and passing either input or a
member rooted in it through `ref` or `out` make the declarative plan
unsupported. Nested in-place updates of read-only members are expressed
through the generated `members.Member` marker shown above.

The maximum structured callbacks also receive `MappingContextMarker`. Only
its `Operation` may be read as a declarative value; the marker itself cannot
be stored, passed, captured, compared, or returned. It intentionally has no
`Mapper`, so declarative nested dispatch still has exactly the marker forms
listed above.

## Execution

Arguments are evaluated once, left to right in source order, including
reordered named arguments. A null guard for a read-only member runs before its
source expression. Equivalent declarative calls participate in the same
path-sensitive dependency graph as other `Construct`, `Resolve`, and `Members`
expressions.

Nested dispatch uses the scoped `IMapper` from the current mapping chain. It
creates a new `MappingContext` frame with the nested operation while retaining
the same application-wide service lookup and mapping scope. Exceptions from
user argument evaluation and the nested mapper propagate unchanged. A runtime
destination mismatch is the Morphant-authored exception described above.

Each exact `ITypeMapper<TSource, TDestination>` pair must currently be
registered manually with the application's service provider. See
[Runtime dispatch and DI](runtime-dispatch.md) for the scoped mapper lifecycle
and [Manual mapping](manual-mapping.md) for `context.Mapper` usage in
context-aware runtime result policies and `Convert`. See
[Observable failures](observable-failures.md) for all typed failure paths.
