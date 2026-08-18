# Configuration inheritance

Morphant has two explicit reuse mechanisms:

- `base.Configure(builder)` connects mapper-level defaults and makes base
  mappings available for inclusion;
- `IncludeBase<TSource, TDestination>()` imports rules from one named mapping
  configuration.

## Connect a base mapper

```csharp
public abstract class CommonMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.NullSourceHandling(NullSourceHandling.Throw);
        builder.Map<Order, OrderDto>();
    }
}

[MorphantMapper]
public partial class ApplicationMapper : CommonMapper
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

## Include mapping rules

```csharp
public abstract class AnimalMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Animal, AnimalDto>()
            .Members((source, _) => new()
            {
                Name = source.Name
            });
}

[MorphantMapper]
public partial class DogMapper : AnimalMapper
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

Each setting is resolved independently in this order:

1. Current mapping.
2. Included mappings, nearest first.
3. Current mapper.
4. Connected base mappers, nearest first.
5. MSBuild property.
6. Morphant default.

`Default` continues to the next level. See the
[settings overview](settings/README.md) for configuration examples.

## Boundaries

- Include base configuration only once at each level.
- Reused rules may only reference members accessible from the derived mapper.
- Cross-assembly configuration inheritance is not supported in Morphant 0.1;
  mappings from another assembly can still be registered independently with
  DI.
