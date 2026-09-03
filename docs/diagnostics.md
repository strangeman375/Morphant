# Compile-time diagnostics

Morphant reports configuration problems while the consumer project is being
compiled. `MORPH0047` and `MORPH0048` are warnings; every other Morphant rule
is an error by default.

Published diagnostic IDs are never renumbered or reused. Changing an ID would
break existing suppressions and `dotnet_diagnostic.<ID>.severity` settings.

## Catalog

| ID | Category | Default | Meaning |
|---|---|---:|---|
| [MORPH0001](diagnostics/MORPH0001.md) | Compatibility | Error | The project uses a C# language version older than C# 9. |
| [MORPH0002](diagnostics/MORPH0002.md) | Compatibility | Error | Required Morphant runtime types are missing. |
| [MORPH0003](diagnostics/MORPH0003.md) | Compatibility | Error | Required Morphant runtime types are defined more than once. |
| [MORPH0004](diagnostics/MORPH0004.md) | Compatibility | Error | The Morphant runtime and source generator are incompatible. |
| [MORPH0005](diagnostics/MORPH0005.md) | Declaration | Error | An attributed mapper does not derive from `TypeMapper<TMapper>`. |
| [MORPH0006](diagnostics/MORPH0006.md) | Declaration | Error | The mapper declaration is not partial. |
| [MORPH0007](diagnostics/MORPH0007.md) | Declaration | Error | A containing type required by a nested mapper is not partial. |
| [MORPH0008](diagnostics/MORPH0008.md) | Declaration | Error | A mapper or required containing type is file-local. |
| [MORPH0009](diagnostics/MORPH0009.md) | Declaration | Error | The mapper already implements the `ITypeMapper` that Morphant needs to generate. |
| [MORPH0010](diagnostics/MORPH0010.md) | Declaration | Error | A mapper interface could conflict with a generated `ITypeMapper` for some generic type arguments. |
| [MORPH0011](diagnostics/MORPH0011.md) | Registration | Error | A mapping type is inaccessible to generated code. |
| [MORPH0012](diagnostics/MORPH0012.md) | Registration | Error | A top-level source or destination type is an unsupported type parameter. |
| [MORPH0013](diagnostics/MORPH0013.md) | Registration | Error | The same source and destination types are registered more than once in one mapper. |
| [MORPH0014](diagnostics/MORPH0014.md) | Registration | Error | Two mapping declarations could generate the same `ITypeMapper` for some generic type arguments. |
| [MORPH0015](diagnostics/MORPH0015.md) | Configuration | Error | The mapper has no `Configure` override with readable source code. |
| [MORPH0016](diagnostics/MORPH0016.md) | Configuration | Error | A called `base.Configure(builder)` method has no readable source code. |
| [MORPH0017](diagnostics/MORPH0017.md) | Configuration | Error | Morphant cannot analyze the mapper's `Configure` method. |
| [MORPH0018](diagnostics/MORPH0018.md) | Configuration | Error | Morphant cannot analyze a mapping configuration. |
| [MORPH0019](diagnostics/MORPH0019.md) | Composition | Error | A destination-selection rule, `Members`, or `Convert` is configured more than once. |
| [MORPH0020](diagnostics/MORPH0020.md) | Composition | Error | `Convert` is combined with destination-selection or member rules. |
| [MORPH0021](diagnostics/MORPH0021.md) | Settings | Error | A setting argument is not a supported compile-time constant. |
| [MORPH0022](diagnostics/MORPH0022.md) | Settings | Error | A Morphant MSBuild property has an invalid value. |
| [MORPH0023](diagnostics/MORPH0023.md) | Settings | Error | A setting does not apply to this kind of mapping. |
| [MORPH0024](diagnostics/MORPH0024.md) | Inheritance | Error | The same base `Configure` method is included more than once. |
| [MORPH0025](diagnostics/MORPH0025.md) | Inheritance | Error | The same `IncludeBase` relation is configured more than once. |
| [MORPH0026](diagnostics/MORPH0026.md) | Inheritance | Error | The mapping requested by `IncludeBase` cannot be found. |
| [MORPH0027](diagnostics/MORPH0027.md) | Inheritance | Error | The current source or destination type is incompatible with the included mapping. |
| [MORPH0028](diagnostics/MORPH0028.md) | Inheritance | Error | An inherited lambda references a member inaccessible from the generated mapper. |
| [MORPH0029](diagnostics/MORPH0029.md) | Callbacks | Error | `Construct`, `Resolve`, or `Members` was not given an inline lambda. |
| [MORPH0030](diagnostics/MORPH0030.md) | Callbacks | Error | A mapping callback captures or references code unavailable to the generated mapper. |
| [MORPH0031](diagnostics/MORPH0031.md) | Callbacks | Error | A `Construct`, `Resolve`, or `Members` lambda uses an unsupported statement. |
| [MORPH0032](diagnostics/MORPH0032.md) | Callbacks | Error | A `Construct`, `Resolve`, or `Members` lambda modifies `previous` or `result`. |
| [MORPH0033](diagnostics/MORPH0033.md) | Callbacks | Error | A declarative API such as `Auto`, `Ignore`, or `Map` is used in an unsupported position. |
| [MORPH0034](diagnostics/MORPH0034.md) | Declaration | Error | A mapper member conflicts with generated `Supports(Type, Type)`. |
| [MORPH0035](diagnostics/MORPH0035.md) | Construction | Error | Create or Update without an existing destination has no way to create one. |
| [MORPH0036](diagnostics/MORPH0036.md) | Construction | Error | `ConstructorSelection` cannot select one usable constructor. |
| [MORPH0037](diagnostics/MORPH0037.md) | Construction | Error | A rule for the selected constructor parameter is invalid. |
| [MORPH0038](diagnostics/MORPH0038.md) | Construction | Error | `previous` is used where no existing destination is available. |
| [MORPH0039](diagnostics/MORPH0039.md) | Construction | Error | `Construct` or `Resolve` returns `null` or `default` where destination creation is required. |
| [MORPH0040](diagnostics/MORPH0040.md) | Members | Error | An explicit destination-member rule is invalid. |
| [MORPH0041](diagnostics/MORPH0041.md) | Members | Error | A required destination member is left uninitialized on some operation. |
| [MORPH0042](diagnostics/MORPH0042.md) | Members | Error | A member rule cannot be applied during the required Create or Update operation. |
| [MORPH0043](diagnostics/MORPH0043.md) | Members | Error | `Members` returns `null` or `default` on some operation. |
| [MORPH0044](diagnostics/MORPH0044.md) | NestedMapping | Error | The source or destination type of a nested mapping cannot be determined. |
| [MORPH0045](diagnostics/MORPH0045.md) | NestedMapping | Error | A nested mapping result cannot be assigned to its destination. |
| [MORPH0046](diagnostics/MORPH0046.md) | NestedMapping | Error | A nested Update has no valid destination value. |
| [MORPH0047](diagnostics/MORPH0047.md) | MappingCompleteness | Warning | A supported source member is not used by the mapping. |
| [MORPH0048](diagnostics/MORPH0048.md) | MappingCompleteness | Warning | A supported destination member is not mapped. |
| [MORPH0049](diagnostics/MORPH0049.md) | IncludeMembers | Error | An `IncludeMembers` selector is invalid or duplicated. |
| [MORPH0050](diagnostics/MORPH0050.md) | IncludeMembers | Error | Two included scopes expose the same source-member name. |
| [MORPH0051](diagnostics/MORPH0051.md) | Flattening | Error | More than one compatible nested source path matches the same target name. |
| [MORPH0052](diagnostics/MORPH0052.md) | Polymorphism | Error | A mapping links its exact source type as a derived branch. |
| [MORPH0053](diagnostics/MORPH0053.md) | Polymorphism | Error | The same derived source branch is configured more than once. |
| [MORPH0054](diagnostics/MORPH0054.md) | Polymorphism | Error | A branch source or destination is incompatible with the base pair. |
| [MORPH0055](diagnostics/MORPH0055.md) | Polymorphism | Error | A branch type is inaccessible to generated code. |
| [MORPH0056](diagnostics/MORPH0056.md) | Registration | Error | The same underlying mapping pair is registered with conflicting tuple presentations. |
| [MORPH0057](diagnostics/MORPH0057.md) | Generator | Error | Morphant caught an unexpected internal generator exception. |
| [MORPH0058](diagnostics/MORPH0058.md) | Declaration | Error | A mapper closes `TypeMapper<TMapper>` with an invalid self type. |
| [MORPH0059](diagnostics/MORPH0059.md) | Declaration | Error | A mapper or containing type is inaccessible to generated namespace-level code. |

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
