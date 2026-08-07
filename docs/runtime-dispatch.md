# Runtime dispatch and DI

`IMapper` is the application-wide facade. It dispatches an exact closed
`TSource -> TDestination` pair to manually registered
`ITypeMapper<TSource, TDestination>` services from the current
`IServiceProvider`.

## Manual registration

Core v0 does not include `AddMorphant`, assembly scanning, registration
attributes, or generated manifests. Register the concrete mapper and every
closed pair it implements. With `Microsoft.Extensions.DependencyInjection`:

```csharp
services.AddScoped<ApplicationMapper>();

services.AddScoped<ITypeMapper<OrderDto, Order>>(
    provider => provider.GetRequiredService<ApplicationMapper>());
services.AddScoped<ITypeMapper<CustomerDto, Customer>>(
    provider => provider.GetRequiredService<ApplicationMapper>());

services.AddScoped<IMapper, Mapper>();
```

The concrete generated mapper can have ordinary constructor dependencies.
Its scoped or transient lifetime is controlled entirely by the container.
Morphant does not activate mapper types or inspect assemblies at runtime.

Mappings from several assemblies are registered into the same provider.
Mapper class and assembly identity are not part of the lookup key.

## Exact-pair lookup

For each call, `Mapper` requests exactly
`IEnumerable<ITypeMapper<TSource, TDestination>>`:

| Candidates | Result |
|---:|---|
| `0` | Mapping fails; no fallback or assignable-pair search runs |
| `1` | The single candidate executes |
| `2+` | Mapping fails as ambiguous; first/last registration never wins |

The source-only facade calls `ITypeMapper.Create`. The overload with a
destination calls `ITypeMapper.Update`, including an explicit null
destination. `MappingMode` gates methods on the selected mapping; it is not a
second lookup key.

Nullable annotations, mapper ownership, registration order, and the current
operation do not create hidden variants of an exact pair. Open-generic and
runtime-type lookup are outside core v0.

## Mapping scope

Every root `IMapper.Map` creates a mapping scope and completes it in a
`finally` block. `MappingContext` exposes:

- `Operation`, the immutable Create or Update frame for the current call;
- `Mapper`, the scoped facade used for nested calls.

Declarative nested markers and manual `context.Mapper.Map(...)` calls resolve
through the same service provider and registration set. Each nested call gets
its own immutable context frame but stays inside the root mapping scope.
Sequential recursion, reentrancy, and caught nested exceptions are supported.
The scoped facade cannot be retained and used after the root call completes.

Independent root calls create independent scopes and may execute in parallel.
Parallel use of one captured scoped facade inside a single mapping chain has
no guarantee.

## Returned result

Both facade overloads return the authoritative result produced by the
selected mapper. An Update candidate may reuse the destination or replace it.
Always assign or otherwise consume that return value:

```csharp
destination = mapper.Map(source, destination);
```

See [Declarative mapping](declarative-mapping.md) for identity selection and
[Manual mapping](manual-mapping.md) for manual nested dispatch.
