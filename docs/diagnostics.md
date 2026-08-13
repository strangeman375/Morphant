# Compile-time diagnostics

Morphant reports configuration problems while the consumer project is being
compiled. `MORPH0047` and `MORPH0048` are warnings; every other Morphant rule
is an error by default.

## Catalog

| ID | Category | Default | Meaning |
|---|---|---:|---|
| `MORPH0001` | Compatibility | Error | The effective C# language version is older than C# 9. |
| `MORPH0002` | Compatibility | Error | A compatible Morphant runtime contract is missing. |
| `MORPH0003` | Compatibility | Error | More than one Morphant runtime contract is visible. |
| `MORPH0004` | Compatibility | Error | The visible runtime contract is incompatible with this generator. |
| `MORPH0005` | Declaration | Error | An attributed mapper does not derive from `TypeMapper`. |
| `MORPH0006` | Declaration | Error | The mapper declaration is not partial. |
| `MORPH0007` | Declaration | Error | A containing type required by a nested mapper is not partial. |
| `MORPH0008` | Declaration | Error | A mapper or required containing type is file-local. |
| `MORPH0009` | Declaration | Error | The mapper already declares the exact generated mapping contract. |
| `MORPH0010` | Declaration | Error | A declared interface can unify with the generated mapping contract. |
| `MORPH0011` | Registration | Error | A mapping type is inaccessible to generated code. |
| `MORPH0012` | Registration | Error | A root type parameter is unsupported as a mapping root. |
| `MORPH0013` | Registration | Error | The same canonical pair is registered more than once in one mapper. |
| `MORPH0014` | Registration | Error | Two generated mapping contracts can unify. |
| `MORPH0015` | Configuration | Error | The mapper has no source-bodied `Configure` override. |
| `MORPH0016` | Configuration | Error | A directly included base mapper has no available `Configure` body. |
| `MORPH0017` | Configuration | Error | Mapper-level builder flow cannot be analyzed. |
| `MORPH0018` | Configuration | Error | Pair-level builder flow cannot be analyzed. |
| `MORPH0019` | Composition | Error | A local result, `Members`, or `Convert` plan slot is configured more than once. |
| `MORPH0020` | Composition | Error | `Convert` is mixed with a result policy or `Members`. |
| `MORPH0021` | Settings | Error | An effective C# setting is not a supported compile-time constant. |
| `MORPH0022` | Settings | Error | An effective Morphant MSBuild property has an invalid value. |
| `MORPH0023` | Settings | Error | A setting is not applicable to the selected mapping model or capability. |
| `MORPH0024` | Inheritance | Error | Direct base configuration is included more than once. |
| `MORPH0025` | Inheritance | Error | The same `IncludeBase` edge is configured more than once. |
| `MORPH0026` | Inheritance | Error | The requested included mapping pair cannot be found. |
| `MORPH0027` | Inheritance | Error | A current mapping type is incompatible with the included pair. |
| `MORPH0028` | Inheritance | Error | An effective inherited callback is inaccessible from the generated mapper. |
| `MORPH0029` | Callbacks | Error | A structured callback is not an inline lambda. |
| `MORPH0030` | Callbacks | Error | Callback code cannot be transferred with its binding and lifetime intact. |
| `MORPH0031` | Callbacks | Error | A structured callback contains unsupported imperative syntax. |
| `MORPH0032` | Callbacks | Error | A normalized structured destination input is mutated. |
| `MORPH0033` | Callbacks | Error | A compile-time marker escapes a supported terminal DSL position. |
| `MORPH0034` | Declaration | Error | A mapper member conflicts with generated `Supports(Type, Type)`. |
| `MORPH0035` | Construction | Error | A reachable no-previous path has no construction policy. |
| `MORPH0036` | Construction | Error | Convention construction cannot select one applicable constructor. |
| `MORPH0037` | Construction | Error | A rule for the selected constructor parameter is invalid. |
| `MORPH0038` | Construction | Error | A structured result selects `previous` where no previous destination exists. |
| `MORPH0039` | Construction | Error | A structured construction plan is statically `null` or `default`. |
| `MORPH0040` | Members | Error | An effective explicit destination-member rule is invalid. |
| `MORPH0041` | Members | Error | A required destination member is not initialized on a reachable path. |
| `MORPH0042` | Members | Error | A valid member rule requires an unavailable lifecycle phase. |
| `MORPH0043` | Members | Error | A structured member plan is statically `null` or `default`. |
| `MORPH0044` | NestedMapping | Error | A terminal nested mapping pair cannot be determined statically. |
| `MORPH0045` | NestedMapping | Error | The nested result is incompatible with its final target. |
| `MORPH0046` | NestedMapping | Error | An explicit or generated nested Update destination is invalid. |
| `MORPH0047` | MappingCompleteness | Warning | A supported source member does not participate in the effective plan. |
| `MORPH0048` | MappingCompleteness | Warning | A supported destination member is not occupied by the effective plan. |

## Configure severity

Use the standard `.editorconfig` syntax:

```ini
[*.cs]
dotnet_diagnostic.MORPH0047.severity = none
dotnet_diagnostic.MORPH0048.severity = error
```

Supported severities include `none`, `silent`, `suggestion`, `warning` and
`error`.

Changing severity affects compiler presentation only. In particular,
suppressing an error does not turn an invalid mapping into a valid one.

Service registration cannot be checked at compile time. Missing, duplicate or
invalid DI registrations are described under [Exceptions](exceptions.md).
See [Unmapped member validation](settings/unmapped-member-validation.md) for
the two completeness warnings.
