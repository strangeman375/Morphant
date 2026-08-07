# Quick start

Morphant generates mapping code in the consumer compilation. The runtime and
analyzer ship together in the package:

```xml
<PackageReference Include="Morphant" Version="0.1.0" />
```

The same layout can be used while developing from project references:

```xml
<ProjectReference Include="..\Morphant\Morphant.csproj" />
<ProjectReference Include="..\Morphant.Generator\Morphant.Generator.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

## Declare a mapping

The minimal mapping relies on exact-name conventions:

```csharp
using Morphant;

public sealed class Customer
{
    public string Name { get; set; } = string.Empty;
}

public sealed class CustomerDto
{
    public string Name { get; set; } = string.Empty;
}

[MorphantMapper]
public sealed partial class ApplicationMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Customer, CustomerDto>();
}
```

`MorphantMapperAttribute` selects a concrete partial mapper for generation.
Morphant emits an implementation of
`ITypeMapper<Customer, CustomerDto>` into the other partial declaration. The
same generated mapper class may implement several closed mapping pairs.

Conventions use exact case-sensitive names and warning-free implicit C#
conversions. A matching complex type does not start another mapping
automatically; nested mapping is always an explicit `Map(...)`, `Create(...)`,
or `Update(...)` rule.

## Register the generated pair

Core v0 intentionally has no `AddMorphant`, assembly scanning, or generated
registration manifest. Register the concrete mapper and each closed pair with
the application's DI container. With
`Microsoft.Extensions.DependencyInjection`:

```csharp
services.AddScoped<ApplicationMapper>();
services.AddScoped<ITypeMapper<Customer, CustomerDto>>(
    provider => provider.GetRequiredService<ApplicationMapper>());
services.AddScoped<IMapper, Mapper>();
```

If `ApplicationMapper` implements more pairs, register each
`ITypeMapper<TSource, TDestination>` interface against the same scoped
concrete instance. Mappings generated in other assemblies are registered in
the same way; assembly identity is not part of the lookup key.

See [Runtime dispatch and DI](runtime-dispatch.md) for the exact zero/one/many
candidate rule and mapping scope lifecycle.

## Create and update

Resolve `IMapper` from the current application scope:

```csharp
var created = mapper.Map<Customer, CustomerDto>(customer);

var existing = new CustomerDto();
existing = mapper.Map(customer, existing);
```

The source-only overload invokes generated `ITypeMapper.Create`. The overload
with a destination invokes generated `ITypeMapper.Update`, including when the
destination argument is explicitly `null`.

The return value is authoritative. Update may reuse `existing`, mutate it and
return it, or return a replacement chosen by the mapping plan. Always keep the
returned value.

## Add explicit behavior

Use `Construct` and `Members` when conventions are not the complete plan:

```csharp
builder.Map<OrderDto, Order>()
    .Construct(source => new(source.Id))
    .Members((source, _) => new()
    {
        DisplayName = source.Name,
        Revision = Auto(),
        LegacyCode = Ignore()
    });
```

Use `Convert` when the entire mapping is clearer as normal synchronous C#:

```csharp
builder.Map<string, Uri>()
    .Convert((source, _, _) => new Uri(source!, UriKind.RelativeOrAbsolute));
```

Continue with [Declarative mapping](declarative-mapping.md),
[Manual mapping](manual-mapping.md), and [Null handling](settings/null-handling.md).
