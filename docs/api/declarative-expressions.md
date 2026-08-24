# Declarative expressions

These helpers are used inside `Construct`, `Resolve`, and `Members`. They
describe a mapping rule; they are not runtime methods for application code.

## Convention and value helpers

| Call | Use |
|---|---|
| `ByConvention()` | Select a constructor by convention; optional overrides can follow |
| `Auto()` | Apply convention mapping to the inferred target |
| `Auto<T>()` | Apply convention mapping to target type `T` |
| `Ignore()` | Leave the inferred target unchanged |
| `Ignore<T>()` | Leave a target of type `T` unchanged |
| `Value<T>(value)` | Use `value` with the exact target type `T` |

Use the generic forms when target typing would otherwise be ambiguous.
`ByConvention()` is for `Construct` and `Resolve`; the other helpers can be
used for constructor arguments or destination members where applicable.
`ByConvention()` is available only when the destination has at least one
supported constructor parameter to override. Use `new()` for parameterless
construction.

## Nested mapping helpers

| Call | Source | Destination | Operation |
|---|---|---|---|
| `Map()` | Inferred by target name | Inferred from target | Create or Update from current state |
| `Map(source)` | `source` | Inferred from target | Create or Update from current state |
| `Map<T>()` | Inferred by target name | `T` | Create or Update from current state |
| `Map<T>(source)` | `source` | `T` | Create or Update from current state |
| `Create(source)` | `source` | Inferred from target | Create |
| `Create<T>(source)` | `source` | `T` | Create |
| `Update(source, destination)` | `source` | Inferred from target | Update supplied `destination` |
| `Update<T>(source, destination)` | `source` | `T` | Update supplied `destination` |

| Parameter | Description |
|---|---|
| `T` | Nested destination type when it cannot or should not be inferred |
| `source` | Value passed to the nested mapping |
| `destination` | Existing nested destination passed to Update; may be `null` |
| `value` | Expression wrapped with the exact receiving type `T` |

Nested mappings are always explicit and must be registered as exact mapping
pairs. See [Nested mapping](../nested-mapping.md) for operation selection and
read-only members.
