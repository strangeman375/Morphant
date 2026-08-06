# Mapping modes

`MappingMode` controls whether a generated mapping can create a new
destination, update an existing destination, or do both.

## Configure an assembly default

Set `MorphantMappingMode` in `Directory.Build.props` to configure all projects
under a directory:

```xml
<Project>
  <PropertyGroup>
    <MorphantMappingMode>Create</MorphantMappingMode>
  </PropertyGroup>
</Project>
```

The same property can be set in a project file:

```xml
<PropertyGroup>
  <MorphantMappingMode>Update</MorphantMappingMode>
</PropertyGroup>
```

MSBuild resolves imports before Morphant runs. A value in a `.csproj`
therefore normally overrides a value imported earlier from
`Directory.Build.props`, and the generator receives only the final value.

Supported values are `Default`, `Create`, `Update`, and
`CreateAndUpdate`. Names are case-insensitive. An empty or missing property
has the same behavior as `Default`.

## Configure a mapper default

Set the mapper-level mode when most registrations use the same behavior:

```csharp
protected override void Configure(MapperBuilder builder)
{
    builder.MappingMode(MappingMode.Create);

    builder.Map<Order, OrderDto>();
    builder.Map<Customer, CustomerDto>();
}
```

Both registrations inherit `Create`.

## Override one mapping

Pass a mode to `Map<TSource, TDestination>()` to override the mapper-level
value:

```csharp
protected override void Configure(MapperBuilder builder)
{
    builder.MappingMode(MappingMode.Create);

    builder.Map<Order, OrderDto>();
    builder.Map<Customer, CustomerDto>(MappingMode.Update);
    builder.Map<Product, ProductDto>(MappingMode.CreateAndUpdate);
}
```

The effective value is selected in this order:

1. A non-`Default` value passed to `Map<TSource, TDestination>()`.
2. A non-`Default` mapper-level value passed to `builder.MappingMode(...)`.
3. A non-`Default` `MorphantMappingMode` MSBuild property.
4. `MappingMode.CreateAndUpdate`.

`Default` continues to the next level. Mapper-level settings apply to the
whole mapper, regardless of whether the setting call appears before or after
its mapping registrations. If `builder.MappingMode(...)` is called more than
once, the last call wins, including a last call with `Default`.

## Mode behavior

| Effective mode | `Create(source, context)` | `Update(source, destination, context)` |
|---|---|---|
| `Create` | Maps to a new destination | Throws `NotSupportedException` |
| `Update` | Throws `NotSupportedException` | Maps to the supplied destination |
| `CreateAndUpdate` | Maps to a new destination | Maps to the supplied destination |

`Default` means inheritance; it is not an operation by itself.

Every generated mapping continues to implement the single
`ITypeMapper<TSource, TDestination>` interface with both methods. This keeps
runtime resolution uniform. Invoking a method excluded by the effective mode
fails immediately in the generated mapper.

The same gate applies to a manual `Convert`. `MappingMode` is the only
effective setting used by a manual mapping; once the selected operation is
enabled, the lambda itself owns the complete mapping lifecycle.

Mapping mode expressions must be compile-time constants composed only from
the defined `Create` and `Update` flags. The MSBuild property must use one
of the named values listed above.

## Invalid values

If a C# mode is not a compile-time constant, contains undefined flags, or an
inherited `MorphantMappingMode` value is not recognized, Morphant still
generates the `ITypeMapper<TSource, TDestination>` implementation for the
registered pair. Both mapping methods throw `NotSupportedException` when
invoked.

An explicit valid mapping-level mode still overrides an invalid mapper-level
value. A mapping that uses `Default` inherits the invalid mapper-level value
and therefore has two throwing methods.

The same rule applies to the assembly level: a valid mapper-level or
mapping-level value overrides an invalid `MorphantMappingMode`, while a
mapping that inherits the invalid property has two throwing methods.
