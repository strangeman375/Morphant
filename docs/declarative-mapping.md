# Declarative mapping

A declarative pair has two cooperating plans:

- `Construct` selects the result for a no-previous branch and may explicitly
  choose between the previous destination and a replacement.
- `Members` describes values for destination members around the selected
  result.

If either plan is omitted, Morphant uses the applicable constructor and member
conventions. `Convert` is a separate manual model and cannot be combined with
these plans.

## Construction

A source-only `Construct` describes creation when no previous destination is
used:

```csharp
builder.Map<OrderDto, Order>()
    .Construct(source => new(source.Id));
```

It runs for public Create and for an Update normalized to a no-previous branch.
It does not run merely because an existing destination has immutable members.
Without a previous-aware plan, an existing Update starts from the supplied
destination.

Use the previous-aware overload when runtime data chooses identity or a
replacement:

```csharp
builder.Map<SnapshotDto, Snapshot>()
    .Construct((source, previous) =>
    {
        if (previous.HasValue && previous.Value.Id == source.Id)
            return previous;

        return new(source.Id);
    });
```

The returned construction result is authoritative. A selected previous value
preserves its identity; a constructed or factory value replaces it. A terminal
null/default result is not silently repaired by a hidden convention fallback.

Structured construction can select a destination constructor,
`ByConvention()`, or `ByFactory(...)`. A direct destination such as a scalar,
interface, abstract type, or opaque value object needs an explicit direct or
factory result whenever creation is reachable.

## Members

`Members` returns a generated destination-specific record whose assignments
are mapping rules:

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
- `Auto()` requests the exact-name convention for that member;
- `Ignore()` occupies the member without assigning it;
- `Map(...)`, `Create(...)`, and `Update(...)` perform explicit nested mapping.

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
2. select a replacement in previous-aware `Construct` and apply rules to the
   replacement;
3. return the unchanged instance when no applicable rule mutates it.

The caller and every outer nested mapping must use the returned result. This
rule also applies to value destinations, where the returned value is the
authoritative updated copy.

Read [Nested mapping](nested-mapping.md) for replacement propagation and
[Manual mapping](manual-mapping.md) for algorithms that own the entire
lifecycle.
