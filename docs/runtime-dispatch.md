# Runtime dispatch and DI

`IMapper` is Morphant's main application entry point. It resolves an exact
`TSource -> TDestination` mapping from the current `IServiceProvider`.

## Register mappings

Register the generated mapper, each pair it implements, and `IMapper`. With
`Microsoft.Extensions.DependencyInjection`:

```csharp
services.AddScoped<ApplicationMapper>();

services.AddScoped<ITypeMapper<OrderDto, Order>>(
    provider => provider.GetRequiredService<ApplicationMapper>());
services.AddScoped<ITypeMapper<CustomerDto, Customer>>(
    provider => provider.GetRequiredService<ApplicationMapper>());

services.AddScoped<IMapper, Mapper>();
```

All pair registrations for one generated mapper should resolve the same
concrete scoped instance. The mapper can use ordinary constructor injection.

Mappings from several assemblies are registered in the same way. Core v0 does
not include assembly scanning or automatic registration.

## Exact-pair lookup

Lookup uses the exact closed `ITypeMapper<TSource, TDestination>` pair:

| Registrations | Result |
|---:|---|
| `0` | `MappingNotFoundException` |
| `1` | The mapping runs |
| `2+` | `AmbiguousMappingException` |

Registration order does not select a winner, and Morphant does not fall back
to assignable or open-generic pairs.

The source-only facade calls Create. Supplying a destination calls Update,
including when the destination argument is explicitly `null`.

## Mapping scope

Every root `IMapper.Map` call creates a mapping scope. Nested declarative calls
and `context.Mapper.Map(...)` use the same registrations while receiving their
own Create or Update `MappingContext`.

`MappingContext` exposes:

- `Operation`, the current Create or Update operation;
- `Mapper`, the scoped facade for nested calls.

Do not retain `context.Mapper` after the root call completes.

## Returned result

Both facade overloads return the authoritative mapping result. Update may
reuse the supplied destination or replace it:

```csharp
destination = mapper.Map(source, destination);
```

## Calling an exact pair directly

An exact generated pair can also be called without DI when application-wide
dispatch is deliberately unnecessary:

```csharp
ITypeMapper<OrderDto, Order> pair = new ApplicationMapper();

var created = pair.Create(orderDto);
var updated = pair.Update(orderDto, order);
```

This is an additional capability. The standard application path remains DI
registration and `IMapper`.

See [Nested mapping](nested-mapping.md) for nested operations and
[Exceptions](exceptions.md) for runtime failure types.
