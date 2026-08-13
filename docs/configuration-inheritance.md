# Configuration inheritance

Morphant has two explicit reuse mechanisms:

- `base.Configure(builder)` connects mapper-level defaults and makes base
  pairs available for inclusion;
- `IncludeBase<TSource, TDestination>()` imports rules from one named mapping
  pair.

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

`ApplicationMapper` inherits the mapper-level null policy. The base mapping is
available for `IncludeBase`, but is not automatically registered as a pair on
the derived mapper.

Without the direct `base.Configure(builder)` call, base configuration is not
included.

## Include a mapping pair

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

`Dog` must be assignable to `Animal`, and `DogDto` to `AnimalDto`. The base
pair may also be declared in the same mapper; declaration order does not
matter.

Local member rules replace inherited rules for the same destination member.
Other inherited member rules remain. Construction and manual `Convert` rules
are selected independently for a different source/destination pair.

## Settings precedence

Each setting is resolved independently in this order:

1. Current mapping pair.
2. Included base pairs, nearest first.
3. Current mapper.
4. Connected base mappers, nearest first.
5. MSBuild property.
6. Morphant default.

`Default` continues to the next level. See the
[settings overview](settings/README.md) for configuration examples.

## Boundaries

- Include base configuration only once at each level.
- Reused members and callbacks must be accessible from the derived mapper.
- Cross-assembly configuration inheritance is not supported in core v0;
  mappings from another assembly can still be registered independently with
  DI.
