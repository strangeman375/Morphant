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

The effective setting is also used by `ByConvention()` inside `Construct` or
`Resolve`:

```csharp
builder.Map<OrderDto, Order>()
    .ConstructorSelection(ConstructorSelection.Greediest)
    .Construct(source => new(
        ByConvention(),
        new()
        {
            tenantId = source.TenantId
        }));
```

An explicitly selected constructor is unaffected by the setting. Runtime
result policies and manual `Convert` do not use constructor selection.

Configure an assembly default with `MorphantConstructorSelection`. See the
[settings overview](README.md) for levels and precedence.
