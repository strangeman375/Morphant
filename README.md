# Morphant

Morphant is a compile-time object mapper for C#. A source generator turns an
explicit `TypeMapper` configuration into strongly typed
`ITypeMapper<TSource, TDestination>` implementations; runtime dispatch does no
reflection-based mapping discovery.

The documentation describes the implemented core v0 API. Current review
status and remaining boundaries are tracked in the
[mapping API roadmap](MAPPING_API_IMPLEMENTATION_PLAN.md).

Core v0 is an architectural preview focused on object lifecycle, nullability,
constructor/member plans, manual algorithms, nested mapping, and predictable
Update identity. Automatic collection semantics, projection, automatic DI
registration, and the other [post-v0 capabilities](docs/core-v0.md) are
intentionally outside this release boundary. Collection, tuple, delegate,
expression-tree, deferred, and observable roots may still be mapped as opaque
values with runtime policies or `Convert`.

## Quick start

Reference the runtime package, which includes the source generator as an
analyzer:

```xml
<PackageReference Include="Morphant" Version="0.1.0" />
```

Declare the types and a partial mapper:

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

Register every closed pair explicitly with the application's DI container.
For `Microsoft.Extensions.DependencyInjection`:

```csharp
services.AddScoped<ApplicationMapper>();
services.AddScoped<ITypeMapper<Customer, CustomerDto>>(
    provider => provider.GetRequiredService<ApplicationMapper>());
services.AddScoped<IMapper, Mapper>();
```

Use the application-wide mapper and always keep the returned Update result:

```csharp
var created = mapper.Map<Customer, CustomerDto>(customer);
var updated = mapper.Map(customer, existingDto);
existingDto = updated;
```

For a concrete mapper without application-wide DI, use the context-free exact
pair extensions. A statically typed pair infers both types; a multi-pair
concrete mapper names them directly:

```csharp
ITypeMapper<Customer, CustomerDto> pair = new ApplicationMapper();
var direct = pair.Create(customer);

var multiPairMapper = new ApplicationMapper();
var directFromMultiPair =
    multiPairMapper.Create<Customer, CustomerDto>(customer);
```

No separate pair selector is required. Nested mappings can use every exact
pair declared by that same generated mapper instance. The pair inventory is
generated into the mapper and does not use runtime reflection.

The generated Update may return the supplied instance or an authoritative
replacement. Ignoring its return value is therefore incorrect.

See the complete [quick start](docs/quick-start.md) for generated code setup,
manual registration, and Create/Update behavior.

## Configuration model

- `Construct` creates a structured result only when no previous destination
  exists; `Resolve` selects a structured result for every operation.
- `ConstructUsing` and `ResolveUsing` are pair-specific generated runtime result
  policies emitted for every eligible pair; each has a short overload and a
  context-aware overload whose final parameter is the real `MappingContext`.
- `Members` describes destination member values around that selected result.
- `Value<T>` pins an exact declarative member or constructor-parameter type
  when ordinary target typing is insufficient.
- `Convert` replaces the declarative pipeline with an ordinary synchronous C#
  algorithm.
- `Option<T>` distinguishes an absent previous destination from a present
  value without relying on `default(T)`.
- Declarative lambdas describe a path-sensitive dependency graph, not
  imperative statement order.

Read [Declarative mapping](docs/declarative-mapping.md) before relying on
expression evaluation order or side effects.

## Documentation

- [Quick start](docs/quick-start.md)
- [Declarative mapping and `Option<T>`](docs/declarative-mapping.md)
- [Manual mapping with `Convert`](docs/manual-mapping.md)
- [Nested mapping](docs/nested-mapping.md)
- [Runtime dispatch and DI](docs/runtime-dispatch.md)
- [Observable failures](docs/observable-failures.md)
- [Configuration inheritance](docs/configuration-inheritance.md)
- [Generated artifacts](docs/generated-code.md)
- [Core v0 scope and non-goals](docs/core-v0.md)
- [Mapping modes](docs/settings/mapping-mode.md)
- [Null handling](docs/settings/null-handling.md)
- [Member selection](docs/settings/member-selection.md)
- [Constructor selection](docs/settings/constructor-selection.md)
- [Unmapped member validation](docs/settings/unmapped-member-validation.md)

## Language and runtime

The generated consumer surface supports C# 9 and newer language versions.
The runtime package targets `netstandard2.0`.
