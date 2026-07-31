# Template mode

`TemplateMode` controls how Morphant interprets a mapping's `Template()`
lambda. It does not enable or disable convention mapping by itself.

The library default is:

```csharp
TemplateMode.Dsl
```

## Modes

| Effective value | Lambda result | Behavior |
|---|---|---|
| `Dsl` | A generated destination template | Morphant interprets the template and applies the remaining effective constructor and member mapping rules |
| `Raw` | `TDestination` | Morphant uses the returned value as the final mapping result without applying constructor or member mappings |

`Dsl` is the regular Morphant template experience. The lambda describes the
parts of the mapping that need explicit control, while markers, configured
rules, and conventions determine the rest:

```csharp
builder.Map<Order, OrderDto>()
    .TemplateMode(TemplateMode.Dsl)
    .Template(source => new()
    {
        Number = source.Number.Trim(),
        Customer = Map()
    });
```

`Raw` gives the lambda complete responsibility for the result:

```csharp
builder.Map<Order, OrderDto>()
    .TemplateMode(TemplateMode.Raw)
    .Template(source => CreateOrderDto(source));
```

This is equivalent in intent to returning `CreateOrderDto(source)` directly.
Morphant does not apply constructor selection, explicit member mappings, or
convention member mappings afterward.

For `MapExisting`, a raw lambda may preserve or replace the supplied
destination:

```csharp
builder.Map<Order, OrderDto>()
    .TemplateMode(TemplateMode.Raw)
    .Template((source, destination) =>
        UpdateOrReplace(source, destination));
```

The value returned by `UpdateOrReplace` is the mapping result.

If no `Template()` is configured, both modes leave ordinary convention mapping
unchanged. `Raw` defines how a present template is interpreted; it does not
turn convention mapping off globally.

## Direct-only destinations

Built-in scalars and other direct-only destination types cannot use a generated
template record. Under `Dsl`, their `Template()` lambda therefore returns the
destination value directly:

```csharp
builder.Map<Session, TimeSpan>()
    .Template(source =>
        TimeSpan.FromMinutes(source.DurationMinutes));
```

The returned scalar is necessarily the final result. Morphant does not
generate an artificial template record for such types.

`TemplateMode` does not expand the general set of supported mapping or
template destination types. Unsupported destinations receive no `Template()`
API in either mode.

## Configure an assembly default

Set `MorphantTemplateMode` in `Directory.Build.props` to configure projects
under a directory:

```xml
<Project>
  <PropertyGroup>
    <MorphantTemplateMode>Raw</MorphantTemplateMode>
  </PropertyGroup>
</Project>
```

The same property can be set in a project file. Supported values are
`Default`, `Dsl`, and `Raw`; names are case-insensitive. A missing or empty
property has the same behavior as `Default`.

MSBuild resolves imports before Morphant runs. A value in a `.csproj`
therefore normally overrides a value imported earlier from
`Directory.Build.props`, and the generator receives only the final value.

## Configure a mapper default

Set the mapper-level mode when most registrations use the same behavior:

```csharp
protected override void Configure(MapperBuilder builder)
{
    builder.TemplateMode(TemplateMode.Raw);

    builder.Map<Order, OrderDto>()
        .Template(source => CreateOrderDto(source));

    builder.Map<Customer, CustomerDto>()
        .Template(source => CreateCustomerDto(source));
}
```

Mapper-level settings apply to the whole mapper, regardless of whether the
setting call appears before or after its mapping registrations. If
`builder.TemplateMode(...)` is called more than once, the last call wins,
including a last call with `Default`.

## Override one mapping

Configure the builder returned by `Map<TSource, TDestination>()`:

```csharp
protected override void Configure(MapperBuilder builder)
{
    builder.TemplateMode(TemplateMode.Raw);

    builder.Map<SomeSource, SomeDestination>()
        .TemplateMode(TemplateMode.Dsl)
        .Template(source => new()
        {
            Value = source.Value
        });

    builder.Map<OtherSource, SomeDestination>()
        .Template(source => CreateDestination(source));
}
```

The effective value is selected in this order:

1. A non-`Default` mapping-level value.
2. A non-`Default` mapper-level value.
3. A non-`Default` `MorphantTemplateMode` MSBuild property.
4. `TemplateMode.Dsl`.

`Default` continues to the next level.

## Coordination by destination

The effective mode belongs to the canonical `TSource → TDestination` pair.
Different source types targeting the same destination may use different
modes.

When all registered pairs for a destination need the same generated template
form, Morphant emits one compact generic extension. For a custom destination
whose pairs all use `Dsl`, it looks like:

```csharp
Template<TSource>(
    this MapperBuilder<TSource, SomeDestination> builder,
    Func<TSource, SomeDestinationMorphantTemplate> template);
```

When custom-destination pairs mix `Dsl` and `Raw`, Morphant emits exact
pair-specific overloads:

```csharp
Template(
    this MapperBuilder<SomeSource, SomeDestination> builder,
    Func<SomeSource, SomeDestinationMorphantTemplate> template);

Template(
    this MapperBuilder<OtherSource, SomeDestination> builder,
    Func<OtherSource, SomeDestination> template);
```

The destination template type is generated once and shared by all applicable
`Dsl` overloads.

A canonical pair ignores C# spelling differences that do not produce a
distinct CLR signature, including nullable reference annotations,
`dynamic`/`object`, aliases, native integer aliases, and tuple element names.
A pair may be registered only once in the compilation. Diagnostics for
duplicate registrations are planned separately; until then, behavior for a
duplicate pair is unsupported.

## Mixed-mode source limitations

Exact overloads are generated in a top-level generated class. Their source
type must therefore be nameable and accessible there.

For a mixed-mode destination, Morphant currently does not generate the
pair-specific `Template()` overload when its source is:

- a mapper type parameter;
- a private or protected nested type;
- a type that contains an otherwise inaccessible type argument.

Other nameable pairs for the same destination are still generated. A `Dsl`
pair still requests the shared destination template type even if its exact
extension cannot be emitted.

This limitation only applies when a destination needs pair-specific overloads.
If its generated template form is uniform, the generic `TSource` extension
continues to support mapper type parameters and private or protected nested
source types. File-local mapping types remain unsupported independently of
`TemplateMode`. A future diagnostic will report the unsupported mixed-mode
pair.

## Invalid values

C# setting expressions must be compile-time constants whose values are
defined by `TemplateMode`. The MSBuild property must use one of the named
values.

If the effective value is invalid, Morphant generates no template API for the
affected pair. Convention mapping and the generated mapper contract remain
available. A valid value at a more specific level overrides an invalid outer
value.
