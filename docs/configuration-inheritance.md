# Configuration inheritance

Morphant reuses configuration through the C# mapper hierarchy. The two opt-in
operations have separate purposes:

- `base.Configure(builder)` connects base mapper registrations and root-level
  settings.
- `IncludeBase()` composes one locally repeated mapping pair with the nearest
  matching pair in that connected chain.

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
    }
}
```

`CommonMapper` does not need `MorphantMapperAttribute` when its source is in the
same compilation. `ApplicationMapper` receives the inherited-only
`Order -> OrderDto` registration and the base root setting.

Without the direct `base.Configure(builder)` call, Morphant does not inspect or
apply the base configuration. Calls hidden in arbitrary helper methods or
control flow are not followed. Calling `base.Configure(builder)` more than once
is an invalid configuration.

Expression-bodied overrides are supported:

```csharp
protected override void Configure(MapperBuilder builder) =>
    base.Configure(builder);
```

## Repeat a pair locally

Repeating a pair in the derived mapper starts a clean pair plan:

```csharp
protected override void Configure(MapperBuilder builder)
{
    base.Configure(builder);

    builder.Map<Order, OrderDto>()
        .Members((source, _) => new()
        {
            Status = source.Status
        });
}
```

The local pair still uses connected base root settings, but it does not inherit
the base pair's map-level settings, `Construct`, `Members`, or `Convert`.

Call `IncludeBase()` to compose with the nearest matching base pair:

```csharp
builder.Map<Order, OrderDto>()
    .IncludeBase()
    .Members((source, _) => new()
    {
        Status = source.Status
    });
```

`IncludeBase()` is invalid when the base configuration is not connected, no
matching base pair exists, or it is called more than once on the same local
pair.

## Settings precedence

Each setting is resolved independently from the most specific level:

1. The current pair.
2. Included base pairs, nearest first.
3. The current mapper root.
4. Connected base mapper roots, nearest first.
5. The assembly MSBuild property.
6. The Morphant library default.

`Default` continues to the next level. Within one `Configure` level, the last
recognized call for a setting wins, including a final call with `Default`.

An inherited setting that does not apply to the selected mapping model has no
effect. For example, inherited constructor and member settings do not
invalidate a local `Convert`. The same setting written explicitly on that
manual pair remains an invalid configuration.

## Plan composition

An included plan follows these rules:

- A local `Construct` replaces the inherited `Construct` completely.
- Inherited and local `Members` rules merge by destination member. A local
  expression, `Auto()`, or `Ignore()` replaces the inherited rule for that
  member. Conventions run only for members left unoccupied after the merge.
- A local `Convert` replaces the entire inherited declarative plan.
- An inherited `Convert` cannot be partially combined with local `Construct`
  or `Members` rules.

The generator rebuilds dependencies from the effective member rules, so an
overridden inherited expression is not evaluated.

## Accessibility and generics

Transferred configuration is emitted inside the derived mapper. Referenced
base members must therefore be accessible there. `protected`, `internal`, and
public helpers can be reused when ordinary C# accessibility permits it;
private helpers and expressions containing an explicit `base.` access form an
unsupported inherited plan. A local `Construct` or `Convert` replacement can
remove an inaccessible inherited plan before emission.

Source-visible generic base mappers are supported for both open and closed
derived mappers, including nested partial mapper declarations. Morphant emits
the open configuration surface required to compile the generic base DSL and
specializes the effective mapping for the derived mapper's type arguments.

Cross-assembly `IncludeBase()` is not part of v0 because the source generator
cannot transfer a base `Configure` body that is unavailable in the current
compilation. Register mappings from another assembly independently instead.
