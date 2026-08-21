# `ForDerived`

Adds one runtime source branch to an exact base mapping.

```csharp
builder.Map<Animal, AnimalDto>()
    .ForDerived<Dog, DogDto>();

builder.Map<Dog, DogDto>();
```

## Overload

```csharp
ForDerived<TDerivedSource, TDerivedDestination>()
    where TDerivedSource : TSource
    where TDerivedDestination : TDestination
```

The derived source must differ from the base source. Configure each derived
source at most once on the pair. Both types must be accessible to generated
code.

The method returns the same mapping builder. It creates a dispatch link only:
the derived pair must be registered separately, and no mapping rules are
inherited in either direction. `IncludeBase` remains the independent API for
rule reuse.

See [Runtime polymorphism](../runtime-polymorphism.md) for selection, unknown
types, Update behavior and runtime errors.
