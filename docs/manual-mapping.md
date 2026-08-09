# Manual mapping

This page documents the implemented core v0 API. Current review status and
remaining boundaries are tracked in the
[mapping API roadmap](../MAPPING_API_IMPLEMENTATION_PLAN.md).

Use `Convert` when the whole mapping is easier to express as ordinary
synchronous C# than as a result policy and `Members` plan:

```csharp
builder.Map<OrderDto, Order>()
    .Convert((source, previous, context) =>
    {
        if (source is null)
            return null!;

        if (previous.TryGetValue(out var destination))
        {
            destination.UpdateFrom(source);
            return destination;
        }

        return new Order(source.Id);
    });
```

`Convert` has three prefix overloads: `source`; `source, previous`; and
`source, previous, context`. They implement the same lifecycle and differ only
in available inputs. The full lambda receives:

- the original source, before `NullSourceHandling`;
- the actual existing destination as `Option<TDestination>`;
- the current immutable `MappingContext` frame.

`Map(source)` supplies `MappingOperation.Create` and `Option.None`.
`Map(source, null)` supplies `MappingOperation.Update` and `Option.None`.
`Map(source, destination)` supplies `MappingOperation.Update` and
`Option.Some(destination)`.

The returned value is authoritative, including `null`, reuse of the previous
instance, or a replacement instance. Morphant does not run null handling,
convention construction, member mapping, or declarative markers afterward.
Only the effective `MappingMode` gates whether Create and Update may call the
lambda.

Expression lambdas and arbitrary synchronous block bodies are supported.
Constructors, factories, mutation, loops, `try` statements, local functions,
multiple returns, record `with`, method calls, and exceptions keep their normal
C# semantics. Configure-local runtime values and Configure-local functions
cannot be captured; reusable state or behavior belongs on the mapper type.

Collection, tuple, delegate, expression-tree, task/deferred, buffer, and
observable roots are eligible opaque values. Their pairs receive
`ConstructUsing`, `ResolveUsing`, and `Convert`, but no `Construct`, `Resolve`,
`Members`, conventions, or special element/await/rebinding behavior. For
example, collection mapping is an explicit ordinary algorithm in core v0:

```csharp
builder.Map<IReadOnlyList<OrderDto>, List<Order>>()
    .Convert((source, _, context) =>
        source is null
            ? new List<Order>()
            : source.Select(context.Mapper.Map<OrderDto, Order>).ToList());
```

The opaque boundary prevents the root type from implying a hidden lifecycle;
the callback is responsible for all container behavior.

For a nested mapping, call the scoped mapper from the current context:

```csharp
var address = previous.TryGetValue(out var destination)
    ? context.Mapper.Map(source.Address, destination.Address)
    : context.Mapper.Map<AddressDto, Address>(source.Address);
```

The nested overload selects its own Create or Update frame while preserving
the mapping scope. Declarative `Auto`, `Ignore`, `ByConvention`, and nested
`Map` / `Create` / `Update` calls are not available as markers inside
`Convert`.

A pair may contain one `Convert` or a declarative plan made from one of
`Construct`, `Resolve`, `ConstructUsing`, `ResolveUsing` plus `Members`, but
not both. Pair-specific null, member, and constructor settings are invalid for
`Convert`; inherited settings are simply inactive for that pair.

See [Runtime dispatch and DI](runtime-dispatch.md) for service lookup and
mapping-scope lifetime, and [Declarative mapping](declarative-mapping.md) for
the lifecycle that `Convert` replaces.
