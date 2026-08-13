# Quick start

## Install Morphant

The package contains both the runtime and the source generator:

```shell
dotnet add package Morphant --version 0.1.0
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
public sealed partial class ApplicationMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Customer, CustomerDto>();
}
```

The generated `ApplicationMapper` implements
`ITypeMapper<Customer, CustomerDto>`. Exact, case-sensitive member names are
mapped when C# provides a warning-free implicit conversion.

## Register it with DI

Register the concrete mapper, every pair it implements, and the application
`IMapper` facade. With `Microsoft.Extensions.DependencyInjection`:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddScoped<ApplicationMapper>();
services.AddScoped<ITypeMapper<Customer, CustomerDto>>(
    provider => provider.GetRequiredService<ApplicationMapper>());
services.AddScoped<IMapper, Mapper>();
```

If one mapper implements several pairs, register every
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

The returned value is authoritative. Update may mutate and reuse `existing`,
or return a replacement, so always keep its result.

## Add explicit rules

Use `Construct` and `Members` when conventions are not enough:

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

Use `Convert` when the whole mapping is clearer as ordinary synchronous C#:

```csharp
builder.Map<string, Uri>()
    .Convert(source =>
        new Uri(source!, UriKind.RelativeOrAbsolute));
```

Continue with [Declarative mapping](declarative-mapping.md),
[Manual mapping](manual-mapping.md), or
[Runtime dispatch and DI](runtime-dispatch.md).

## Calling a pair without DI

Morphant also allows an exact generated pair to be called directly when
application-wide dispatch is deliberately not used:

```csharp
ITypeMapper<Customer, CustomerDto> pair = new ApplicationMapper();

var created = pair.Create(customer);
var updated = pair.Update(customer, existing);
```

This is an additional capability; the main application setup uses DI and
`IMapper` as shown above.
