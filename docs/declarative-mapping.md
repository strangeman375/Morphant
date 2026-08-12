# Declarative mapping

This page documents the implemented core v0 API. Current review status and
remaining boundaries are tracked in the
[mapping API roadmap](../MAPPING_API_IMPLEMENTATION_PLAN.md).

A declarative pair has one optional result-policy slot and one cooperating
`Members` plan. Exactly one of these result policies may be configured:

| Policy | Runs | Callback model |
|---|---|---|
| `Construct` | Only when normalized previous is absent | Structured constructor DSL |
| `Resolve` | For every reachable operation | Structured constructor DSL |
| `ConstructUsing` | Only when normalized previous is absent | Ordinary runtime C# |
| `ResolveUsing` | For every reachable operation | Ordinary runtime C# |

`Construct` and `Resolve` are generated only when the destination has at least
one supported constructor. A sole parameterless constructor still gets both
methods so explicit construction, structured replacement, and creation-time
`init`/`required` members remain available. `ConstructUsing` and
`ResolveUsing` are pair-specific generated extension methods available for
every eligible pair through its `MappingExtension` artifact.

`Members` describes values around the selected result. If no result policy is
configured, Morphant constructs a structured destination by convention on a
no-previous branch and reuses an existing previous destination. `Convert` is a
separate manual model and cannot be combined with a result policy or
`Members`.

## Structured result policies

`Construct` describes only no-previous creation:

```csharp
builder.Map<OrderDto, Order>()
    .Construct(source => new(source.Id));
```

It runs for public Create and for an Update normalized to a no-previous branch.
It is not invoked when an existing destination is available.

Use `Resolve` when runtime data chooses reuse or replacement:

```csharp
builder.Map<SnapshotDto, Snapshot>()
    .Resolve((source, previous) =>
    {
        if (previous.HasValue && previous.Value.Id == source.Id)
            return previous;

        return new(source.Id);
    });
```

Both methods also have a maximum overload whose final parameter is
`MappingContextMarker`. The marker exposes only `Operation`; it has no runtime
instance or `Mapper` and cannot be stored, passed, compared, captured, or
returned. Its `Operation` value may be used as an ordinary declarative value.

Structured construction can select an explicit destination constructor or
`ByConvention()`. It cannot return an arbitrary ready-made destination.
`ByFactory` is not part of the API.

## Explicit declarative values

Use an ordinary expression when the generated member or constructor parameter
already provides enough target typing. Use `Value<T>(value)` when the exact
final type must be stated before Morphant lowers the declarative plan:

```csharp
builder.Map<JobDto, Job>()
    .Construct(source => new(
        Value<long>(source.Id),
        Value<Action>(() => Record(source.Id))))
    .Members((source, _) => new()
    {
        Payload = Value<object>(source.PayloadId),
        Label = Value<string?>(source.Label)
    });
```

This form supports overload selection, boxing and other implicit conversions,
nullable annotations, lambdas, method groups, and target-typed language
expressions. `T` is the exact receiving type, including nullability. The short
form `Value(value)` infers `T` from the argument only: `Value(1)` pins `int`,
while intentional boxing is written as `Value<object>(1)`.

`Value<T>` is a compile-time intrinsic, not a runtime wrapper or callback.
Its argument is evaluated once by the generated mapper, and the conversion to
`T` is preserved so generated code cannot bind a different constructor
overload. It may be used through supported conditionals, casts, and
declarative locals. Put helper calls inside its argument
(`Value<T>(Compute(...))`); do not pass the marker itself to a helper.
Morphant either lowers every intrinsic in the expression
or treats the plan as invalid; it never emits a partial runtime call to
`Value`, `Auto`, `Ignore`, or a nested-map marker.

## Runtime result policies

Use the generated runtime policies for a factory, cache, scalar, opaque value,
interface, abstract destination, or any other ready-made result:

```csharp
builder.Map<OrderDto, IOrder>()
    .ConstructUsing(source =>
        orderFactory.Create(source.Id));

builder.Map<OrderDto, IOrder>()
    .ResolveUsing((source, previous, context) =>
        previous.HasValue && CanReuse(previous.Value, source)
            ? previous.Value
            : orderFactory.Create(source.Id, context.Operation));
```

Each method has a short and a context-aware overload. `ConstructUsing`
receives `source` or `(source, context)`; `ResolveUsing` receives
`(source, previous)` or `(source, previous, context)`. Arity changes only the
available inputs, never lifecycle or applicability. There is no zero-argument
factory callback; write `_` when `source` is unused. These callbacks are
ordinary synchronous C# and may use `context.Mapper` for nested runtime
dispatch. They receive normalized inputs after declarative null handling, and
the common `Members` plan runs after a non-null result. Declarative markers are
unavailable inside them, including `Value`.

The generated receiver preserves the source and destination types of the
registered pair. Callback inputs are typed separately: `source` is the
root-normalized non-null source, and `previous` is an `Option` of the
root-normalized destination. The return type is exactly the destination type
carried by the pair builder, including root nullability. For example, a
`Source? -> Destination?` callback receives `Source` and returns
`Destination?`. This split is why the methods cannot be ordinary generic
members of `MapperBuilder<TSource, TDestination>`.

## Callback transfer and structured grammar

Structured `Construct`, `Resolve`, and `Members` arguments must be inline
lambdas. A method group or materialized delegate reports `MORPH0029`, because
Morphant cannot inspect it as a declarative plan. Runtime `ConstructUsing` and
`ResolveUsing` callbacks may use lambdas, method groups, or materialized
delegates.

All callbacks must remain transferable to the generated mapper. Mapper
instance/static members and compile-time constants are available, but a
runtime local declared in `Configure`, `builder` itself, an external local
function/delegate, a file-local symbol, or binding that cannot be preserved
reports `MORPH0030`. `previous`, `result`, and
`MappingContextMarker.Operation` may be read directly in structured code, but
cannot be captured by deferred code; take an ordinary snapshot first.

The outer block of a structured lambda supports initialized locals, nested
blocks, complete `if`/`switch` flow, return/throw paths, expressions, and the
documented terminal DSL markers. Loops, standalone side-effect statements,
subsequent local mutation, outer local functions, `try`/`using`/`lock`,
labels, and similar imperative syntax report `MORPH0031`. The sole standalone
assignment is a direct source discard such as `_ = source.LegacyField;`;
Morphant removes it without evaluating the getter.

Normalized destination inputs are read-only descriptions, not imperative
update handles. Assigning through `previous`, `result`, or a traced alias,
including `ref`/`out` and increment/decrement, reports `MORPH0032`. A
compile-time marker that escapes a terminal constructor/member/result
position, or appears in a runtime callback, reports `MORPH0033`.

These diagnostics are configurable, but suppression or a severity override
does not change the mapping plan. The affected reachable path keeps a typed
`MappingConfigurationException` recovery stub; independent paths and pairs
remain executable.

## Members

`Members` returns a generated destination-specific record whose assignments
are mapping rules. It has four prefix overloads: `source`; `source, previous`;
`source, previous, result`; and `source, previous, result, context`. The final
context is `MappingContextMarker`. Shorter forms only omit unused information
and do not change lifecycle or evaluation phase.

```csharp
builder.Map<OrderDto, Order>()
    .Members((source, previous) => new()
    {
        Number = source.Number,
        Revision = Auto(),
        LegacyCode = Ignore()
    });
```

- an ordinary expression is an explicit rule;
- `Value<T>(value)` is an explicit rule with an exact final target type;
- `Auto()` requests the exact-name convention for that member;
- `Ignore()` occupies the member without assigning it;
- `Map(...)`, `Create(...)`, and `Update(...)` perform explicit nested mapping.

`Auto<T>()` and `Ignore<T>()` also assert the exact receiving type, including
nullability. The untyped forms remain preferable in a directly target-typed
member initializer. Generated `Member<T>` and `ConstructorParameter<T>`
wrappers have no public value constructors; assignments and constructor
arguments use ordinary expressions or the intrinsic forms above.

An eligible readable non-writable reference member may appear only as a
get-only proxy for standalone nested `Update`; it is not an ordinary member
rule or convention candidate. See [Nested mapping](nested-mapping.md).

The result-aware overload can read the actual selected result:

```csharp
.Members((source, previous, result) => new()
{
    DisplayName = result.Prefix + source.Name
});
```

This does not expose an imperative post-processing phase. Each member rule is
classified independently: creation-time rules may enter an object initializer,
while result-dependent writable rules run after result selection. `init` and
`required` members follow the normal C# construction boundary.

## `Option<T>`

`Option<T>` represents presence separately from the stored value:

```csharp
if (previous.TryGetValue(out var destination))
{
    // A destination was supplied.
}

var absent = Option<Order>.None;
var present = Option<Order>.Some(destination);
```

`HasValue` and `TryGetValue` test presence. Reading `Value` when `HasValue` is
false is invalid. `Option<T>` is used because `default(T)` cannot distinguish
an absent destination from a present nullable or value destination.

Public Create and an Update normalized from a null destination both give a
declarative plan `Option.None`. Their public `MappingOperation` values remain
different. A manual `Convert` receives the original destination presence
without declarative normalization.

## Dependency graph, not statement order

Declarative lambdas use C# syntax to describe a path-sensitive dependency
graph. They are not executed as an imperative configuration callback at
runtime.

Morphant guarantees:

- a dependency executes before every expression that uses it;
- one bound expression shared by effective construction/member rules is
  evaluated once;
- conditions and selected branches remain path-sensitive;
- argument expressions execute once and named arguments keep source order;
- an overridden or unreachable rule and its now-unused dependencies do not
  execute.

Morphant does not guarantee relative order between independent member
expressions, generated assignments, or setter/nested-mapping side effects.
Do not make one independent rule observe mutation performed by another. Use a
declarative local to state a real data dependency, or use `Convert` when normal
sequential C# execution is required.

## Update identity and the returned result

An existing declarative Update normally starts with the supplied destination.
It may then:

1. keep that instance and apply writable member rules;
2. select a replacement in `Resolve` or `ResolveUsing` and apply rules to the
   replacement;
3. return the unchanged instance when no applicable rule mutates it.

The caller and every outer nested mapping must use the returned result. This
rule also applies to value destinations, where the returned value is the
authoritative updated copy.

Read [Nested mapping](nested-mapping.md) for replacement propagation and
[Manual mapping](manual-mapping.md) for algorithms that own the entire
lifecycle.
