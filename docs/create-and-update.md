# Create and Update

Every mapping has two operations:

| Call | Operation |
|---|---|
| `mapper.Map<TSource, TDestination>(source)` | Create |
| `mapper.Map(source, destination)` | Update |

Create produces a destination without an existing value. Update receives a
destination, but may reuse it or return a replacement. Always keep the returned
value.

```csharp
destination = mapper.Map(source, destination);
```

## Choosing the destination

- [`Construct`](api/construct.md) and
  [`ConstructUsing`](api/construct-using.md) run when no destination is
  available.
- [`Resolve`](api/resolve.md) and
  [`ResolveUsing`](api/resolve-using.md) choose the result for every Create
  and Update.
- Without an explicit rule, Morphant uses constructor conventions when
  possible.

`Resolve` can inspect `Option<TDestination> previous` to decide whether to
reuse the existing destination:

```csharp
.Resolve((source, previous) =>
    previous.TryGetValue(out var destination) &&
    destination.Id == source.Id
        ? previous
        : new(source.Id));
```

## Applying member rules

Create can assign constructor arguments, `init` properties, settable
properties and mutable fields. During Update, a replacement constructed by
`Resolve` also receives applicable creation-only rules. Reusing an existing
destination applies only post-construction rules, so its creation-only
members keep their values.

[`Members`](api/members.md) may use `previous` to read the supplied
destination and `result` to read the destination selected for the current
operation.

## Null values

[`NullSourceHandling`](settings/null-handling.md) is applied first. For Update,
`NullDestinationHandling.Create` treats a null destination as unavailable and
uses the creation rules. The operation exposed by `MappingContext.Operation`
remains Update.

[`MappingMode`](settings/mapping-mode.md) controls whether Create and Update
may be called.

## Manual mappings

[`Convert`](api/convert.md) receives the source, optional previous destination
and mapping context, then returns the final result. Constructor, member and
null-handling rules are not applied around it.

See [Choose a configuration method](api/README.md) for the complete decision
table and overload reference.
