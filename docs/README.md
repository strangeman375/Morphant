# Documentation

Start with the [Quick start](quick-start.md). It covers package installation,
mapper declaration, DI registration, Create, Update and a first explicit rule.

## Mapping guides

- [Declarative mapping](declarative-mapping.md) — creating a destination and
  mapping its members.
- [Manual mapping](manual-mapping.md) — use `Convert` for an ordinary C#
  algorithm.
- [Nested mapping](nested-mapping.md) — call another registered mapping.
- [Dependency injection and `IMapper`](runtime-dispatch.md) — registration,
  mapping selection and `MappingContext`.
- [Configuration inheritance](configuration-inheritance.md) — reuse mapper
  defaults and mapping rules.

## Settings

Read the [settings overview](settings/README.md) for configuration levels,
precedence and defaults.

- [Mapping modes](settings/mapping-mode.md)
- [Null handling](settings/null-handling.md)
- [Member selection](settings/member-selection.md)
- [Constructor selection](settings/constructor-selection.md)
- [Unmapped member validation](settings/unmapped-member-validation.md)

## Reference

- [Compile-time diagnostics](diagnostics.md)
- [Exceptions](exceptions.md)
- [Generated code](generated-code.md)
- [Current limitations](limitations.md)
