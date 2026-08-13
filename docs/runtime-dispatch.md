# Dependency injection and `IMapper`

`IMapper` is Morphant's main application entry point. It gets the requested
`ITypeMapper<TSource, TDestination>` from the current `IServiceProvider`.

## Register mappings

Register the generated mapper, each source/destination mapping it implements,
and `IMapper`. With `Microsoft.Extensions.DependencyInjection`:

```csharp
services.AddScoped<ApplicationMapper>();

services.AddScoped<ITypeMapper<OrderDto, Order>>(
    provider => provider.GetRequiredService<ApplicationMapper>());
services.AddScoped<ITypeMapper<CustomerDto, Customer>>(
    provider => provider.GetRequiredService<ApplicationMapper>());

services.AddScoped<IMapper, Mapper>();
```

All registrations for one generated mapper should resolve the same concrete
scoped instance. The mapper can use ordinary constructor injection.

Mappings from several assemblies are registered in the same way. Core v0 does
not include assembly scanning or automatic registration.

## How a mapping is found

Morphant looks only for the exact
`ITypeMapper<TSource, TDestination>` service:

| Registrations | Result |
|---:|---|
| `0` | `MappingNotFoundException` |
| `1` | The mapping runs |
| `2+` | `AmbiguousMappingException` |

Registration order does not select a winner. Morphant does not substitute a
mapping for base classes or an open generic type.

`Map(source)` calls Create. `Map(source, destination)` calls Update, including
when `destination` is explicitly `null`.

## Mapping context

Context-aware mapping code receives a `MappingContext` for its current Create
or Update operation.

`MappingContext` exposes:

- `Operation`, the current Create or Update operation;
- `Mapper`, the `IMapper` used for nested calls.

Do not retain `context.Mapper` after the top-level `IMapper.Map` call returns.

## Returned result

Both `IMapper` overloads return the mapping result. Update may reuse the
supplied destination or replace it:

```csharp
destination = mapper.Map(source, destination);
```

See [Create and Update](create-and-update.md) for operation-specific behavior.

## Calling without DI

A generated mapper can also be used through an exact
`ITypeMapper<TSource, TDestination>` when application-wide lookup is not
needed:

```csharp
ITypeMapper<OrderDto, Order> typeMapper = new ApplicationMapper();

var created = typeMapper.Create(orderDto);
var updated = typeMapper.Update(orderDto, order);
```

This is an additional option. The standard application path remains DI
registration and `IMapper`.

See [Nested mapping](nested-mapping.md) for nested operations and
[Exceptions](exceptions.md) for runtime failure types.
