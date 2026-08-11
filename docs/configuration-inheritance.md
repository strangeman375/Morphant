# Configuration inheritance

This page documents the implemented core v0 API. Current review status and
remaining boundaries are tracked in the
[mapping API roadmap](../MAPPING_API_IMPLEMENTATION_PLAN.md).

Morphant reuses configuration through explicit pair links and the C# mapper
hierarchy. The two opt-in operations have separate purposes:

- `base.Configure(builder)` connects base mapper root-level settings and makes
  its pair configurations available to `IncludeBase`.
- `IncludeBase<TBaseSource, TBaseDestination>()` imports reusable
  configuration from one explicitly named base mapping pair into the current
  pair.

There is no runtime configuration dispatch. The source generator resolves the
chain and emits one effective mapper implementation.

## Connect a base mapper

Call `base.Configure(builder)` directly from the overriding `Configure` method:

```csharp
public abstract class CommonMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.NullSourceHandling(NullSourceHandling.Throw);

        builder.Map<Order, OrderDto>()
            .Members((source, _) => new()
            {
                Number = source.Number
            });
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

`CommonMapper` does not need `MorphantMapperAttribute` when its source is in the
same compilation. `ApplicationMapper` receives the base root setting, but does
not register `Order -> OrderDto`. Only its local `Invoice -> InvoiceDto` pair is
part of the generated mapper surface. The base `Order -> OrderDto`
configuration remains available as an `IncludeBase` candidate.

Without the direct `base.Configure(builder)` call, Morphant does not inspect or
apply the base configuration. Calls hidden in arbitrary helper methods or
control flow are not followed. Calling `base.Configure(builder)` more than once
is an invalid configuration.

Expression-bodied overrides are supported:

```csharp
protected override void Configure(MapperBuilder builder) =>
    base.Configure(builder);
```

## Include a base pair

Use the generic arguments to name the mapping pair whose configuration the
current pair should reuse:

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

`Dog` must be assignable to `Animal`, and `DogDto` must be assignable to
`AnimalDto`. Class inheritance and interface implementation are both valid.
The C# method signature cannot express these two relationships with `where`
constraints while keeping the two-argument call above, because `TSource` and
`TDestination` belong to the containing `MapperBuilder<,>` type. Morphant
therefore validates both relationships during generation.

Morphant first searches the current mapper level, regardless of declaration
order, and then mapper levels connected through `base.Configure(builder)` from
nearest to farthest. This form is therefore valid without a mapper hierarchy:

```csharp
builder.Map<Dog, DogDto>()
    .IncludeBase<Animal, AnimalDto>();

builder.Map<Animal, AnimalDto>()
    .Members((source, _) => new()
    {
        Name = source.Name
    });
```

When the exact pair exists both on the current level and in the connected base
chain, the current-level pair wins. `base.Configure(builder)` is required only
when the requested pair comes from that chain.

The call is invalid when either type is not assignable, the exact base pair is
not available on the current or connected levels, the pair includes itself, or
`IncludeBase` is called more than once on the current pair.

A local mapping without `IncludeBase` starts with a clean pair plan. It still
uses connected root settings, but it does not import another pair's map-level
settings or member rules.

## Settings precedence

Every map-level setting is inherited, including `MappingMode` and
`ConstructorSelection`. Each setting is resolved independently from the most
specific level:

1. The current pair.
2. Included base pairs, nearest first.
3. The current mapper root.
4. Connected base mapper roots, nearest first.
5. The assembly MSBuild property.
6. The Morphant library default.

`Default` continues to the next level. Within one `Configure` level, the last
recognized call for a setting wins, including a final call with `Default`.

The inherited value is a policy for the current pair. For example, an included
`ConstructorSelection.Largest` selects again among constructors of `DogDto`;
it does not reuse a constructor chosen for `AnimalDto`.

An inherited setting that does not apply to the selected mapping model has no
effect. For example, inherited constructor and member settings do not
invalidate a local `Convert`. The same setting written explicitly on that
manual pair reports `MORPH0023` and uses pair-wide configuration-failure
recovery.

## Plan composition

For a cross-pair include, `IncludeBase` imports explicit `Members` rules and
evaluates them against the current pair:

- inherited and local rules merge by destination member;
- a local expression, `Auto()`, or `Ignore()` replaces the inherited rule for
  that member;
- `Auto()` and conventions are evaluated again for the current source and
  destination types;
- conventions run only for members left unoccupied after the merge;
- dependencies are rebuilt from the effective rules, so an overridden
  inherited expression is not evaluated.

Cross-pair composition never imports `Construct`, `Resolve`, `ConstructUsing`,
`ResolveUsing`, or `Convert`; construction is selected again for the current
destination.

An exact same-pair include from a connected base mapper imports its complete
applicable plan. A local result policy of any of the four families replaces
the inherited result policy, while local and inherited `Members` still merge
by destination member. A local `Convert` owns the complete pair and discards
the inherited declarative plan; any local declarative fragment discards an
inherited `Convert`.

## Accessibility and generics

Effective inherited result-policy, member, and converter expressions are
emitted inside the derived mapper. Referenced base members must therefore be
accessible there. `protected`,
`internal`, and public helpers can be reused when ordinary C# accessibility
permits it. A non-overridden rule that refers to a private helper or contains an
explicit `base.` access is unsupported. An inaccessible rule that is fully
overridden locally is removed before emission.

Source-visible generic base mappers are supported for both open and closed
derived mappers, including nested partial mapper declarations. Morphant emits
the open configuration surface required to compile the generic base DSL and
specializes both the selected base-pair types and effective member rules for
the derived mapper's type arguments.

Cross-assembly `IncludeBase` is not part of v0 because the source generator
cannot transfer a base `Configure` body that is unavailable in the current
compilation. Register mappings from another assembly independently instead.
See [Runtime dispatch and DI](runtime-dispatch.md) for cross-assembly runtime
registration.
