# Quick start

## Install Morphant

Start with a C# console project. Install Morphant, which includes the runtime
and source generator, and the DI package used below:

```shell
dotnet add package Morphant
dotnet add package Microsoft.Extensions.DependencyInjection
```

Reference Morphant directly in each project that declares mappers. A project
that only calls a library's compiled mappers can use its transitive runtime dependency.

## Declare a mapping

Add `ApplicationMapper.cs`:

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
        builder.Map<Customer, CustomerDto>()
            .UnmappedMemberValidation(UnmappedMemberValidation.Destination);
}
```

The generated `ApplicationMapper` implements
`ITypeMapper<Customer, CustomerDto>`. Exact, case-sensitive member names are
mapped when C# provides a warning-free implicit conversion.

The example enables [destination validation](settings/unmapped-member-validation.md)
to report destination members that cannot be mapped. This check is disabled
by default.

The mapper is its own `TypeMapper<TMapper>` argument.

## Register it with DI

Replace `Program.cs` with this complete example:

```csharp
using System;
using Microsoft.Extensions.DependencyInjection;
using Morphant;

var services = new ServiceCollection();
services.AddScoped<ApplicationMapper>();
services.AddScoped<ITypeMapper<Customer, CustomerDto>>(
    provider => provider.GetRequiredService<ApplicationMapper>());
services.AddScoped<IMapper, Mapper>();

using var serviceProvider = services.BuildServiceProvider();
using var scope = serviceProvider.CreateScope();
var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();

var customer = new Customer { Name = "Ada" };
var created = mapper.Map<Customer, CustomerDto>(customer);

customer.Name = "Grace";
var existing = new CustomerDto { Name = "Old" };
existing = mapper.Map(customer, existing);

Console.WriteLine($"{created.Name} -> {existing.Name}"); // Ada -> Grace
```

Run the project with `dotnet run`.

If one mapper implements several mappings, register every
`ITypeMapper<TSource, TDestination>` against the same scoped mapper instance.

## Create and update

The source-only overload performs Create. Supplying a destination performs
Update, even when that destination is `null`.

Always keep the returned value. Update may mutate and reuse `existing`, or
return a replacement.

## Add explicit rules

For example, trim customer names by adding [`Members`](api/members.md) to the
existing `Map<Customer, CustomerDto>()` chain:

```csharp
.Members(source => new()
{
    Name = source.Name.Trim()
});
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
