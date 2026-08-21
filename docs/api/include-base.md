# `IncludeBase`

Reuses configuration from one compatible mapping. Use it for shared settings,
included source scopes, and declarative member rules.

## Availability

`IncludeBase` is available on every mapping-pair builder. The referenced
mapping must exist in the current mapper or in a base mapper connected through
`base.Configure(builder)`.

## Overload

| Call | Description |
|---|---|
| `IncludeBase<TBaseSource, TBaseDestination>()` | Include the nearest compatible mapping for the specified pair |

| Type parameter | Requirement |
|---|---|
| `TBaseSource` | Current source must be assignable to this type |
| `TBaseDestination` | Current destination must be assignable to this type |

The method has no value parameters and returns the same mapping builder.

```csharp
builder.Map<Dog, DogDto>()
    .IncludeBase<Animal, AnimalDto>();
```

Local rules take precedence. A different pair contributes settings,
`IncludeMembers` scopes, and member rules; an exact pair from a base mapper can
also contribute its destination or `Convert` rule.

`IncludeBase` never imports `ForDerived` links. Runtime routing and
configuration reuse are independent.

See [Configuration inheritance](../configuration-inheritance.md) for
precedence and boundaries.
