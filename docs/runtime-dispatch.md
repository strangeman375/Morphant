# Runtime dispatch and DI

This page documents the implemented core v0 API. Current review status and
remaining boundaries are tracked in the
[mapping API roadmap](../MAPPING_API_IMPLEMENTATION_PLAN.md).

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
| `0` | `MappingNotFoundException`; no fallback or assignable-pair search runs |
| `1` | The single candidate executes |
| `2+` | `AmbiguousMappingException`; first/last registration never wins |

If the only registration resolves to `null`, Morphant throws
`InvalidMappingRegistrationException`. Candidate cardinality takes precedence:
two or more registrations are ambiguous even if one resolves to `null`.

The source-only facade calls `ITypeMapper.Create`. The overload with a
destination calls `ITypeMapper.Update`, including an explicit null
destination. `MappingMode` gates methods on the selected mapping; it is not a
second lookup key.

Nullable annotations, mapper ownership, registration order, and the current
operation do not create hidden variants of an exact pair. Open-generic and
runtime-type lookup are outside core v0.

## Context-free exact-pair calls

DI is optional when the caller already has the generated mapper. The public
extensions on `ITypeMapper<TSource, TDestination>` create the root mapping
scope and expose the same authoritative Create/Update result:

```csharp
ITypeMapper<OrderDto, Order> pair = new ApplicationMapper();

var created = pair.Create(orderDto);
var updated = pair.Update(orderDto, order);
```

For a concrete mapper implementing several pairs, specify the pair on the
method. Morphant intentionally has no additional selector object:

```csharp
var created = applicationMapper.Create<OrderDto, Order>(orderDto);
```

The root extension invokes the selected `ITypeMapper<TSource, TDestination>`
capability directly. A contravariant conversion of the receiver therefore
remains valid for that root call.

Within a generated `TypeMapper` root call, `context.Mapper` resolves every
exact closed pair declared by the same mapper instance. The generator emits
the pair checks into the mapper and chains inherited declarations through the
base mapper. Runtime code compares exact `Type` identities; it does not scan
interfaces, inspect assemblies, or maintain a reflection cache.

Nested lookup does not use source contravariance, assignable destination
types, or another mapper object. A manually implemented `ITypeMapper` that
does not derive from `TypeMapper` exposes only the pair selected by the root
receiver; its other interfaces are not discovered. A missing nested pair
throws `MappingNotFoundException`; use application-wide `IMapper` when the
pair is registered elsewhere.

## Mapping scope

Every root `IMapper.Map` and context-free `ITypeMapper.Create` / `Update` call
creates a mapping scope and completes it in a `finally` block.
`MappingContext` exposes:

- `Operation`, the immutable Create or Update frame for the current call;
- `Mapper`, the scoped facade used for nested calls.

Declarative nested markers and runtime callback `context.Mapper.Map(...)`
calls resolve through the same service provider and registration set. Each
nested call gets its own immutable context frame but stays inside the root
mapping scope.
Sequential recursion, reentrancy, and caught nested exceptions are supported.
The scoped facade cannot be retained and used after the root call completes;
doing so throws `MappingScopeCompletedException`.

A default-initialized `MappingContext` is not a mapping frame. It is not
eagerly rejected by every generated Create/Update entry: a mapping that never
uses context data can still execute. Reading `Operation` or `Mapper` from that
default value throws `InvalidMappingContextException` at the point of use.

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
[Manual mapping](manual-mapping.md) for manual nested dispatch. See
[Observable failures](observable-failures.md) for constructor, lookup, scope,
and generated-mapping failure types.
