# Unknown derived type handling

`UnknownDerivedTypeHandling` controls a non-exact runtime source for which no
`ForDerived` link matches.

| Value | Behavior |
|---|---|
| `UseBaseMapping` | Execute the requested base pair; this is the default. |
| `Throw` | Throw `UnmatchedPolymorphicMappingException`. |
| `Default` | Continue through normal setting precedence. |

```csharp
builder.Map<Animal, AnimalDto>()
    .ForDerived<Dog, DogDto>()
    .UnknownDerivedTypeHandling(UnknownDerivedTypeHandling.Throw);
```

Configure a mapper default with
`builder.UnknownDerivedTypeHandling(...)`. Configure an assembly default with
`MorphantUnknownDerivedTypeHandling`.

An exact concrete base instance always uses the base plan. A null source uses
the base null-source policy. `Throw` also applies to a pair with an empty
dispatch table.

See the [settings overview](README.md) for precedence and
[runtime polymorphism](../runtime-polymorphism.md) for branch selection.
