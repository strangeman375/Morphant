<img src="logo.png" alt="Morphant logo" width="128" height="128">

# Morphant

[![CI](https://github.com/strangeman375/Morphant/actions/workflows/ci.yml/badge.svg?branch=main&event=push)](https://github.com/strangeman375/Morphant/actions/workflows/ci.yml?query=branch%3Amain+event%3Apush)
[![Line coverage](https://github.com/strangeman375/Morphant/raw/badges/coverage.svg)](https://github.com/strangeman375/Morphant/blob/badges/coverage.md)
[![NuGet](https://img.shields.io/nuget/v/Morphant.svg)](https://www.nuget.org/packages/Morphant)
[![NuGet downloads](https://img.shields.io/nuget/dt/Morphant.svg)](https://www.nuget.org/packages/Morphant)

Morphant is a compile-time object mapper for C#. It turns explicit
configuration into strongly typed mapping code without runtime reflection.
`IMapper` is the main application entry point.

If Morphant saves you time, you can
[support its development on Boosty](https://boosty.to/strangeman375).

> Morphant is currently in the 0.x series. It focuses on core object mapping;
> automatic collection mapping, projection and several other general-purpose
> mapper features are not included yet. See
> [Current limitations](https://github.com/strangeman375/Morphant/blob/main/docs/limitations.md).

## Install

The runtime package includes the source generator:

```shell
dotnet add package Morphant
```

The DI examples use `Microsoft.Extensions.DependencyInjection`, which is
included in ASP.NET Core shared frameworks or available as a separate package.

## Define a mapper

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

## Register with DI

Register the generated mapper and each source/destination mapping it
implements:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddScoped<ApplicationMapper>();
services.AddScoped<ITypeMapper<Customer, CustomerDto>>(
    provider => provider.GetRequiredService<ApplicationMapper>());
services.AddScoped<IMapper, Mapper>();
```

## Map objects

```csharp
var created = mapper.Map<Customer, CustomerDto>(customer);

var existing = new CustomerDto();
existing = mapper.Map(customer, existing);
```

Always use the value returned by Update: a mapping may reuse the supplied
destination or replace it.

Mappings can rely on conventions, configure construction and members
explicitly, call other registered mappings, or use `Convert` for an ordinary
synchronous C# algorithm.

Continue with the
[Quick start](https://github.com/strangeman375/Morphant/blob/main/docs/quick-start.md)
or browse the
[documentation](https://github.com/strangeman375/Morphant/blob/main/docs/README.md).

## Requirements

- C# 9 or newer;
- a compiler host compatible with Roslyn 4.4.0 or newer;
- a runtime compatible with `netstandard2.0`.

Roslyn 4.4.0 is the minimum host baseline, not a language-version cap.
Consumer code can use newer C# features when its compiler and target types
support them.

## Versioning

Morphant follows Semantic Versioning. Patch releases within a `0.x` minor
line preserve compatibility. Until `1.0`, minor releases may contain
documented breaking changes. See the
[changelog](https://github.com/strangeman375/Morphant/blob/main/CHANGELOG.md).

## License

Morphant is licensed under the
[MIT License](https://github.com/strangeman375/Morphant/blob/main/LICENSE).
