# MORPH0060: Mapper family parameter is absent from mapping

## Cause

A generic parameter of a reusable mapper family, other than its CRTP
self-parameter, does not occur in the source or destination type of a declared
mapping pair:

```csharp
public abstract class CommonMapper<TMapper, TState> : TypeMapper<TMapper>
    where TMapper : CommonMapper<TMapper, TState>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source, Destination>(); // TState is absent
}
```

Every non-self family parameter must occur in every pair declared by that
family. A reference from a generic constraint does not count. C# does not use
method constraints for generic type inference, so Morphant cannot expose a
safe family-scoped fluent API for such a pair.

## Fix

Use the parameter in the source or destination type:

```csharp
builder.Map<Source<TState>, Destination<TState>>();
```

If the mapping does not vary with that parameter, remove the parameter or move
the mapping to a reusable base that does not declare it. The `TMapper`
self-parameter is exempt because it identifies the mapper family.

[All diagnostics](../diagnostics.md)
