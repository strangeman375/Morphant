# Compile-time diagnostics

Morphant's core v0 source generator publishes the following project-owned
diagnostics. Every rule is enabled and configurable through the standard
`dotnet_diagnostic.<ID>.severity` setting. Changing or suppressing a diagnostic
changes compiler presentation only: generation gates and typed recovery remain
the same. The two mapping-completeness rules are warnings; every other rule is
an error by default.

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

## Ownership and precedence

Morphant reports the earliest project-specific reason after which downstream
analysis would be unreliable. A mapper, pair, callback, or path gate suppresses
only diagnostics derived from the unavailable information; independent legal
pairs and independently provable warnings remain. Within a category,
diagnostics have deterministic ID and source order.

Ordinary C# binding and declaration errors remain compiler-owned. Morphant
does not repeat them merely to attach a `MORPH` ID. Compiler preflight can turn
a failure introduced only by transferred generated code into `MORPH0030`,
while preserving a source-owned compiler warning and suppressing only its
generated duplicate.

The `MORPH` prefix is specific to this project and follows Roslyn's
`<PREFIX><number>` guidance. The published IDs form the exact, gapless range
`MORPH0001` through `MORPH0048`; they do not use the C# compiler's `CS` prefix
or .NET analyzer `CA`/`IDE` families.

## Recovery and runtime boundary

When C# can declare a mapping contract, an error keeps the complete generated
`ITypeMapper<TSource, TDestination>` surface and replaces only the unavailable
mapper, pair, operation, branch, or leaf with typed recovery. Suppression and
severity overrides never select a fallback mapping algorithm. Structurally
impossible contracts are omitted while independent legal contracts remain.

Application-wide service lookup cannot be proven by a source generator.
Missing, ambiguous, or `null` registrations and a completed mapping scope are
therefore runtime failures, not compile-time diagnostics. Failures that stop
the analyzer host before Morphant loads are host diagnostics. Usage analyzers,
including a warning for ignoring an authoritative Update result, are outside
the core v0 source-generator catalog.

See [Observable failures](observable-failures.md) for typed runtime exceptions,
[Unmapped member validation](settings/unmapped-member-validation.md) for the
warning policy, and the
[diagnostics contract](../DIAGNOSTICS_PLAN.md) for exact messages, locations,
deduplication, and recovery rules.
