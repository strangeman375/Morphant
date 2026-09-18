# Documentation

Start with the [Quick start](quick-start.md): install Morphant, declare a
mapping, register it with DI, and call Create or Update.

## Mapping guides

- [Create and Update](create-and-update.md) — reuse and replacement.
- [Conventions](conventions.md) — automatic constructor and member mapping.
- [Declarative mapping](declarative-mapping.md) — explicit construction and
  member rules.
- [Manual mapping with `Convert`](api/convert.md) — ordinary C# algorithms.
- [Mapping recipes](recipes.md) — short examples for common tasks.

## Composition and specialized mappings

- [Nested mapping](nested-mapping.md) — call another mapping explicitly.
- [Flattening](flattening.md) — match names such as `CustomerAddressCity`.
- [Include nested source members](include-members.md) — use selected nested
  objects in conventions.
- [Configuration inheritance](configuration-inheritance.md) — reuse defaults
  and mapping rules.
- [Tuple mapping](tuple-mapping.md) — combine inputs, outputs and call-specific
  data.
- [Runtime polymorphism](runtime-polymorphism.md) — select derived mappings.

## Integration and testing

- [Dependency injection and `IMapper`](runtime-dispatch.md)
- [Testing mappings](testing.md)
- [Generated code and Git snapshots](generated-code.md)

## Reference

- [Configuration API](api/README.md) — choose a method and its overload.
- [Settings](settings/README.md) — defaults, applicability and precedence.
- [Compile-time diagnostics](diagnostics.md)
- [Build diagnostics](build-diagnostics.md)
- [Exceptions](exceptions.md)
- [Current limitations](limitations.md)
