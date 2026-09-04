# Configuration inheritance

Morphant has two explicit reuse mechanisms:

- `base.Configure(builder)` connects mapper-level defaults and makes base
  mappings available for inclusion;
- [`IncludeBase<TSource, TDestination>()`](api/include-base.md) imports rules
  from one named mapping configuration.

## Connect a base mapper

```csharp
public abstract class CommonMapper<TMapper> : TypeMapper<TMapper>
    where TMapper : CommonMapper<TMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.NullSourceHandling(NullSourceHandling.Throw);
        builder.Map<Order, OrderDto>();
    }
}

[MorphantMapper]
public partial class ApplicationMapper : CommonMapper<ApplicationMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        base.Configure(builder);
        builder.Map<Invoice, InvoiceDto>();
    }
}
```

`ApplicationMapper` inherits the mapper-level null setting. The base mapping
is available for `IncludeBase`, but does not automatically become a mapping
implemented by `ApplicationMapper`.

If `base.Configure(builder)` is not called, base configuration is not
included.

The recursive constraint on `TMapper` is required. It identifies one mapper
family so inherited generated fluent methods keep the final mapper scope.
Every reusable generic layer that carries the self type must constrain it back
to that layer, rather than only to `TypeMapper<TMapper>` or an earlier base. A
concrete mapper must close that family with itself; an invalid layer or
unrelated self type produces [`MORPH0058`](diagnostics/MORPH0058.md).

Each other generic parameter of a reusable mapper family must occur in the
source or destination type of every mapping declared by that family. Put a
mapping that does not vary with the family parameters in a separate
non-generic reusable base. Morphant reports
[`MORPH0060`](diagnostics/MORPH0060.md) when this boundary is crossed.

## Include mapping rules

```csharp
public abstract class AnimalMapper<TMapper> : TypeMapper<TMapper>
    where TMapper : AnimalMapper<TMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Animal, AnimalDto>()
            .Members((source, _) => new()
            {
                Name = source.Name
            });
}

[MorphantMapper]
public partial class DogMapper : AnimalMapper<DogMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        base.Configure(builder);

        builder.Map<Dog, DogDto>()
            .IncludeBase<Animal, AnimalDto>()
            .Members((source, _) => new()
            {
                Breed = source.Breed
            });
    }
}
```

`Dog` must be assignable to `Animal`, and `DogDto` to `AnimalDto`. The included
mapping may also be declared in the same mapper; declaration order does not
matter.

Local member rules replace inherited rules for the same destination member.
Other inherited member rules remain. `IncludeMembers` scopes are also
inherited, with base scopes considered before local scopes.

When the current and included mappings use different source or destination
types, `IncludeBase` reuses member rules and mapping settings, but not
`Construct`, `Resolve`, `ConstructUsing`, `ResolveUsing` or `Convert`. For the
same source and destination types, those rules can also be inherited from a
base mapper.

## Settings precedence

Local mapping settings take precedence over included mapping settings, and
current mapper settings take precedence over connected base mapper settings.
Each setting is resolved independently. See the
[settings overview](settings/README.md) for the complete order and examples.

## Boundaries

- Include base configuration only once at each level.
- Reused rules may only reference members accessible from the derived mapper.
- A mapper and all its containing types must be accessible to generated
  namespace-level code. See
  [Generated code](generated-code.md#mapper-accessibility).
- Cross-assembly configuration inheritance is not supported. Mappings from
  another assembly can still be registered independently with DI.
