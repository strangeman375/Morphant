# Template surface

`TemplateSurface` controls the compile-time `Template()` API generated for a
mapping pair. It does not enable or disable convention mapping.

The library default is:

```csharp
TemplateSurface.Full
```

## Surface values

| Effective value | Generated API |
|---|---|
| `Full` | A destination template type and `Template()` overloads whose lambda returns that template type |
| `Direct` | `Template()` overloads whose lambda returns `TDestination` directly; no destination template type is requested by this mapping |
| `None` | No `Template()` overload for this mapping; no destination template type is requested by this mapping |

Built-in types and other direct-only destination types remain direct when the
effective value is `Full`. For example, `Source → int` receives overloads
whose lambda returns `int`; Morphant does not generate an artificial template
record for `int`.

`None` only removes the template DSL for the mapping. The generated
`ITypeMapper<TSource, TDestination>` continues to use convention mapping and
the other effective settings.

## Configure an assembly default

Set `TemplateSurface` in `Directory.Build.props` to configure projects under a
directory:

```xml
<Project>
  <PropertyGroup>
    <TemplateSurface>Direct</TemplateSurface>
  </PropertyGroup>
</Project>
```

The same property can be set in a project file:

```xml
<PropertyGroup>
  <TemplateSurface>None</TemplateSurface>
</PropertyGroup>
```

Supported values are `Default`, `Full`, `Direct`, and `None`. Names are
case-insensitive. A missing or empty property has the same behavior as
`Default`.

MSBuild resolves imports before Morphant runs. A value in a `.csproj`
therefore normally overrides a value imported earlier from
`Directory.Build.props`, and the generator receives only the final value.

## Configure a mapper default

Set the mapper-level surface when most registrations use the same form:

```csharp
protected override void Configure(MapperBuilder builder)
{
    builder.TemplateSurface(TemplateSurface.Direct);

    builder.Map<Order, OrderDto>();
    builder.Map<Customer, CustomerDto>();
}
```

Mapper-level settings apply to the whole mapper, regardless of whether the
setting call appears before or after its mapping registrations. If
`builder.TemplateSurface(...)` is called more than once, the last call wins,
including a last call with `Default`.

## Override one mapping

Configure the builder returned by `Map<TSource, TDestination>()`:

```csharp
protected override void Configure(MapperBuilder builder)
{
    builder.TemplateSurface(TemplateSurface.Direct);

    builder.Map<SomeSource, SomeDestination>()
        .TemplateSurface(TemplateSurface.Full)
        .Template(source => new()
        {
            Value = source.Value
        });

    builder.Map<OtherSource, SomeDestination>()
        .TemplateSurface(TemplateSurface.Direct)
        .Template(source => new SomeDestination
        {
            Value = source.Value
        });

    builder.Map<ThirdSource, SomeDestination>()
        .TemplateSurface(TemplateSurface.None);
}
```

The effective value is selected in this order:

1. A non-`Default` mapping-level value.
2. A non-`Default` mapper-level value.
3. A non-`Default` `TemplateSurface` MSBuild property.
4. `TemplateSurface.Full`.

`Default` continues to the next level.

## Coordination by destination

The effective surface belongs to the canonical
`TSource → TDestination` pair. Different source types targeting the same
destination may use different surfaces.

When all registered pairs for a destination have the same effective surface,
Morphant generates one compact generic extension:

```csharp
Template<TSource>(
    this MapperBuilder<TSource, SomeDestination> builder,
    Func<TSource, SomeDestinationMorphantTemplate> template);
```

When the surfaces differ, Morphant generates exact overloads for the enabled
pairs:

```csharp
Template(
    this MapperBuilder<SomeSource, SomeDestination> builder,
    Func<SomeSource, SomeDestinationMorphantTemplate> template);

Template(
    this MapperBuilder<OtherSource, SomeDestination> builder,
    Func<OtherSource, SomeDestination> template);
```

No overload is emitted for the `ThirdSource → SomeDestination` pair configured
with `None`. If at least one pair uses `Full`, the destination template type is
generated once and shared by the applicable overloads.

A canonical pair ignores C# spelling differences that do not produce a
distinct CLR signature, including nullable reference annotations,
`dynamic`/`object`, aliases, native integer aliases, and tuple element names.
A pair may be registered only once in the compilation. Diagnostics for
duplicate registrations are planned separately; until then, behavior for a
duplicate pair is unsupported.

## Mixed-surface source limitations

Exact overloads are generated in a top-level generated class. Their source
type must therefore be nameable and accessible there.

In a mixed-surface destination, Morphant currently does not generate the
pair-specific `Template()` overload when its source is:

- a mapper type parameter;
- a private or protected nested type;
- a type that contains an otherwise inaccessible type argument.

Other nameable pairs for the same destination are still generated. A `Full`
pair still requests the shared destination template type even if its exact
extension cannot be emitted.

This limitation only applies when a destination needs pair-specific overloads.
If its surface is uniform, the generic `TSource` extension continues to support
mapper type parameters and private or protected nested source types. File-local
mapping types remain unsupported independently of `TemplateSurface`. A future
diagnostic will report the unsupported mixed-surface pair.

## Invalid values

C# setting expressions must be compile-time constants whose values are
defined by `TemplateSurface`. The MSBuild property must use one of the named
values.

If the effective value is invalid, Morphant generates no template surface for
the affected pair. Convention mapping and the generated mapper contract remain
available. A valid value at a more specific level overrides an invalid outer
value.
