# Compile-time diagnostics

Morphant reports configuration problems while the consumer project is being
compiled. `MORPH0047` and `MORPH0048` are warnings; every other Morphant rule
is an error by default.

## Catalog

| ID | Category | Default | Meaning |
|---|---|---:|---|
| `MORPH0001` | Compatibility | Error | The project uses a C# language version older than C# 9. |
| `MORPH0002` | Compatibility | Error | Required Morphant runtime types are missing. |
| `MORPH0003` | Compatibility | Error | Required Morphant runtime types are defined more than once. |
| `MORPH0004` | Compatibility | Error | The Morphant runtime and source generator are incompatible. |
| `MORPH0005` | Declaration | Error | An attributed mapper does not derive from `TypeMapper`. |
| `MORPH0006` | Declaration | Error | The mapper declaration is not partial. |
| `MORPH0007` | Declaration | Error | A containing type required by a nested mapper is not partial. |
| `MORPH0008` | Declaration | Error | A mapper or required containing type is file-local. |
| `MORPH0009` | Declaration | Error | The mapper already implements the `ITypeMapper` that Morphant needs to generate. |
| `MORPH0010` | Declaration | Error | A mapper interface could conflict with a generated `ITypeMapper` for some generic type arguments. |
| `MORPH0011` | Registration | Error | A mapping type is inaccessible to generated code. |
| `MORPH0012` | Registration | Error | A top-level source or destination type is an unsupported type parameter. |
| `MORPH0013` | Registration | Error | The same source and destination types are registered more than once in one mapper. |
| `MORPH0014` | Registration | Error | Two mapping declarations could generate the same `ITypeMapper` for some generic type arguments. |
| `MORPH0015` | Configuration | Error | The mapper has no `Configure` override with readable source code. |
| `MORPH0016` | Configuration | Error | A called `base.Configure(builder)` method has no readable source code. |
| `MORPH0017` | Configuration | Error | Mapper settings use a control-flow pattern Morphant cannot analyze. |
| `MORPH0018` | Configuration | Error | A mapping uses a builder pattern Morphant cannot analyze. |
| `MORPH0019` | Composition | Error | Destination selection, `Members`, or `Convert` is configured more than once for one mapping. |
| `MORPH0020` | Composition | Error | `Convert` is combined with `Construct`, `Resolve`, or `Members`. |
| `MORPH0021` | Settings | Error | A setting argument is not a supported compile-time constant. |
| `MORPH0022` | Settings | Error | A Morphant MSBuild property has an invalid value. |
| `MORPH0023` | Settings | Error | A setting does not apply to this kind of mapping. |
| `MORPH0024` | Inheritance | Error | The same base `Configure` method is included more than once. |
| `MORPH0025` | Inheritance | Error | The same `IncludeBase` relation is configured more than once. |
| `MORPH0026` | Inheritance | Error | The mapping requested by `IncludeBase` cannot be found. |
| `MORPH0027` | Inheritance | Error | The current source or destination type is incompatible with the included mapping. |
| `MORPH0028` | Inheritance | Error | An inherited lambda references a member inaccessible from the generated mapper. |
| `MORPH0029` | Callbacks | Error | `Construct`, `Resolve`, or `Members` was not given an inline lambda. |
| `MORPH0030` | Callbacks | Error | A mapping lambda captures or references code unavailable to the generated mapper. |
| `MORPH0031` | Callbacks | Error | A `Construct`, `Resolve`, or `Members` lambda uses an unsupported statement. |
| `MORPH0032` | Callbacks | Error | A `Construct`, `Resolve`, or `Members` lambda modifies `previous` or `result`. |
| `MORPH0033` | Callbacks | Error | `Auto`, `Ignore`, `Map`, or another configuration method is used outside a supported mapping expression. |
| `MORPH0034` | Declaration | Error | A mapper member conflicts with generated `Supports(Type, Type)`. |
| `MORPH0035` | Construction | Error | Create or Update without an existing destination has no way to create one. |
| `MORPH0036` | Construction | Error | `ConstructorSelection` cannot select one usable constructor. |
| `MORPH0037` | Construction | Error | A rule for the selected constructor parameter is invalid. |
| `MORPH0038` | Construction | Error | `previous` is used where no existing destination is available. |
| `MORPH0039` | Construction | Error | `Construct` or `Resolve` returns `null` or `default` where destination creation is required. |
| `MORPH0040` | Members | Error | An explicit destination-member rule is invalid. |
| `MORPH0041` | Members | Error | A required destination member is left uninitialized on some operation. |
| `MORPH0042` | Members | Error | A member rule cannot be applied during the required Create or Update operation. |
| `MORPH0043` | Members | Error | `Members` returns `null` or `default` on some operation. |
| `MORPH0044` | NestedMapping | Error | The source or destination type of a nested mapping cannot be determined. |
| `MORPH0045` | NestedMapping | Error | A nested mapping result cannot be assigned to its destination. |
| `MORPH0046` | NestedMapping | Error | A nested Update has no valid destination value. |
| `MORPH0047` | MappingCompleteness | Warning | A supported source member is not used by the mapping. |
| `MORPH0048` | MappingCompleteness | Warning | A supported destination member is not mapped. |

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
