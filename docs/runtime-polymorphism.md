# Runtime polymorphism

Runtime polymorphism lets one exact base mapping route explicitly listed
runtime source types to separately registered mapping pairs.

```csharp
builder.Map<Animal, AnimalDto>()
    .ForDerived<Dog, DogDto>()
    .ForDerived<Cat, CatDto>();

builder.Map<Dog, DogDto>();
builder.Map<Cat, CatDto>();
```

```csharp
Animal source = new Dog();
AnimalDto result = mapper.Map<Animal, AnimalDto>(source);
// result is DogDto
```

`ForDerived` adds only a link. It does not register the derived pair or
inherit rules. Use `IncludeBase` separately when a derived mapping should
reuse base configuration.

## Selection

Dispatch is local to the requested base pair. Morphant considers only its
`ForDerived` links and chooses the unique most-specific matching source type.
A proxy subtype therefore uses its nearest explicitly linked ancestor.

Registration order is not a priority. If several incomparable interface
branches are equally specific, Morphant throws
`AmbiguousPolymorphicMappingException`.

The selected pair is resolved through the current `MappingContext.Mapper`.
It may belong to another registered mapper. Missing or duplicate derived-pair
registrations produce the normal `MappingNotFoundException` or
`AmbiguousMappingException`; there is no base fallback after a link matched.

## Unknown runtime types

The default `UnknownDerivedTypeHandling.UseBaseMapping` executes the base plan
when no link matches. Choose `Throw` for a closed hierarchy:

```csharp
builder.Map<Animal, AnimalDto>()
    .ForDerived<Dog, DogDto>()
    .UnknownDerivedTypeHandling(UnknownDerivedTypeHandling.Throw);
```

`Throw` rejects a non-exact runtime source with
`UnmatchedPolymorphicMappingException`, including when the pair has no links.
An exact concrete base instance still uses the base mapping. A null source has
no runtime subtype and follows the base null-source policy.

## Update

Create and Update select the same source branch. Update passes a compatible
existing destination to the derived pair. A null destination is passed when
the derived destination can represent null, so that pair applies its own
`NullDestinationHandling`.

An incompatible non-null destination, or null for a non-nullable value-type
branch, throws `PolymorphicDestinationTypeMismatchException`. The dispatcher
does not silently replace it or fall back to the base mapping. The selected
derived Update may still return a replacement according to its normal rules.

Class, interface and CLR-compatible value-type branches are supported.
Runtime dispatch also applies to explicit nested mapping calls. It is not a
projection feature and does not scan assemblies for derived registrations.

Related: [`ForDerived`](api/for-derived.md),
[`UnknownDerivedTypeHandling`](settings/unknown-derived-type-handling.md),
[`IncludeBase`](api/include-base.md), and [Exceptions](exceptions.md).
