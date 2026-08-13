# Constructor selection

`ConstructorSelection` controls convention-based destination creation. Its
default is `Unambiguous`.

| Value | Selection |
|---|---|
| `Explicit` | Do not select a constructor automatically |
| `Parameterless` | Select the supported parameterless constructor |
| `Single` | Select a constructor only when exactly one is supported |
| `Unambiguous` | Select the only parameterized constructor, or the parameterless constructor when none is parameterized |
| `Greediest` | Select the unique applicable constructor with the most mapped arguments |
| `Largest` | Select the unique supported constructor with the most declared parameters |

Only constructors accessible to generated code with ordinary by-value,
nameable parameters are considered.

`Unambiguous` deliberately prefers one parameterized constructor over a
simultaneously available parameterless constructor. Multiple parameterized
constructors are ambiguous.

`Greediest` considers whether arguments can actually be supplied. `Largest`
selects by declared parameter count first. If the selected constructor cannot
be called, Morphant does not silently fall back to a smaller one. Ties are
also ambiguous.

## Configure selection

```csharp
builder.Map<OrderDto, Order>()
    .ConstructorSelection(ConstructorSelection.Greediest);
```

`ByConvention()` is useful when Morphant should select and populate a
constructor automatically, but some parameters need explicit rules. For
example, if `Order` has a constructor with `id`, `name` and `tenantId`
parameters, and `OrderDto` exposes `Id`, `Name` and `Tenant.Id`, the first two
can be matched by convention while `tenantId` is configured explicitly:

```csharp
builder.Map<OrderDto, Order>()
    .ConstructorSelection(ConstructorSelection.Greediest)
    .Construct(source => new(
        ByConvention(),
        new()
        {
            tenantId = source.Tenant.Id
        }));
```

The configured `ConstructorSelection` chooses the constructor used by
`ByConvention()`. Rules in the second argument override convention for the
named parameters; the remaining parameters are matched automatically.

An explicitly named constructor is unaffected by the setting.
`ConstructUsing`, `ResolveUsing` and `Convert` do not use constructor
selection.

Configure an assembly default with `MorphantConstructorSelection`. See the
[settings overview](README.md) for levels and precedence.
