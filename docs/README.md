# Documentation

Start with the [Quick start](quick-start.md). It covers package installation,
mapper declaration, DI registration, Create, Update and a first explicit rule.

## Configure a mapping

- [Choose a configuration method](api/README.md) — method availability,
  intended use, overloads and parameters.
- [Settings](settings/README.md) — defaults, configuration levels and
  precedence.

## Mapping guides

- [Create and Update](create-and-update.md) — destination reuse, replacement
  and operation-specific behavior.
- [Conventions](conventions.md) — automatic constructor and member mapping.
- [Flatten nested source members](flattening.md) — map joined names such as
  `CustomerAddressCity` from `Customer.Address.City`.
- [Include nested source members](include-members.md) — opt selected nested
  objects into convention lookup.
- [Declarative mapping](declarative-mapping.md) — creating a destination and
  mapping its members.
- [Manual mapping](manual-mapping.md) — use `Convert` for an ordinary C#
  algorithm.
- [Nested mapping](nested-mapping.md) — call another registered mapping.
- [Runtime polymorphism](runtime-polymorphism.md) — route explicitly listed
  runtime source types to derived mapping pairs.
- [Dependency injection and `IMapper`](runtime-dispatch.md) — registration,
  mapping selection and `MappingContext`.
- [Configuration inheritance](configuration-inheritance.md) — reuse mapper
  defaults and mapping rules.
- [Mapping recipes](recipes.md) — short examples for common custom mappings.
- [Testing mappings](testing.md) — runtime behavior, diagnostics and generated
  code review.

## Reference

- [Compile-time diagnostics](diagnostics.md)
- [Exceptions](exceptions.md)
- [Generated code](generated-code.md)
- [Current limitations](limitations.md)
