# Quick start

## Install Morphant

The package contains both the runtime and the source generator:

```shell
dotnet add package Morphant
dotnet add package Microsoft.Extensions.DependencyInjection
```

## Declare a mapping

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
public sealed partial class ApplicationMapper : TypeMapper<ApplicationMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Customer, CustomerDto>();
}
```

The generated `ApplicationMapper` implements
`ITypeMapper<Customer, CustomerDto>`. Exact, case-sensitive member names are
mapped when C# provides a warning-free implicit conversion.

The mapper is its own `TypeMapper<TMapper>` argument. This self type keeps
generated fluent methods attached to the correct mapper configuration.

## Register it with DI

Register the concrete mapper, every source/destination mapping it implements,
and the application `IMapper`. With
`Microsoft.Extensions.DependencyInjection`:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddScoped<ApplicationMapper>();
services.AddScoped<ITypeMapper<Customer, CustomerDto>>(
    provider => provider.GetRequiredService<ApplicationMapper>());
services.AddScoped<IMapper, Mapper>();
```

If one mapper implements several mappings, register every
`ITypeMapper<TSource, TDestination>` against the same scoped mapper instance.

## Create and update

Resolve `IMapper` from the current application scope:

```csharp
var created = mapper.Map<Customer, CustomerDto>(customer);

var existing = new CustomerDto();
existing = mapper.Map(customer, existing);
```

The source-only overload performs Create. Supplying a destination performs
Update, even when that destination is `null`.

Always keep the returned value. Update may mutate and reuse `existing`, or
return a replacement.

## Add explicit rules

Use [`Construct`](api/construct.md) and [`Members`](api/members.md) when
conventions are not enough:

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

Use [`Convert`](api/convert.md) when the whole mapping is clearer as ordinary
synchronous C#:

```csharp
builder.Map<string, Uri>()
    .Convert(source =>
        new Uri(source!, UriKind.RelativeOrAbsolute));
```

Continue with [Choose a configuration method](api/README.md),
[Create and Update](create-and-update.md), [Conventions](conventions.md), or
[Dependency injection and `IMapper`](runtime-dispatch.md). For reusable mapper
bases, see [Configuration inheritance](configuration-inheritance.md).

## Calling without DI

Morphant also allows a generated mapper to be used through an exact
`ITypeMapper<TSource, TDestination>` when application-wide lookup is not
needed:

```csharp
ITypeMapper<Customer, CustomerDto> typeMapper = new ApplicationMapper();

var created = typeMapper.Create(customer);
var updated = typeMapper.Update(customer, existing);
```

This is an additional option; the main application setup uses DI and `IMapper`
as shown above.
