# Changelog

Morphant follows Semantic Versioning. Patch releases within a `0.x` minor
line preserve compatibility. Until `1.0`, minor releases may contain
documented breaking changes.

See [current limitations](docs/limitations.md) for functionality that remains
unsupported.

## [Unreleased]

### Added

- Add first-class `ValueTuple` and `System.Tuple` mappings, including named,
  unnamed, long and nullable forms, Create and Update behavior, typed
  composition of multiple inputs and outputs, call-specific state, and a
  diagnostic for conflicting tuple presentations.
- Report unexpected generator exceptions as `MORPH0057` in compiler and IDE
  diagnostics, with a generated failure report containing the full stack
  trace while independent generation continues where possible.
- Report an invalid mapper self type as `MORPH0058` and an inaccessible mapper
  declaration as `MORPH0059`.
- Report a reusable mapper-family parameter that is absent from a declared
  mapping pair as `MORPH0060`.

### Changed

- Replace the non-generic `TypeMapper` and two-argument mapping builder with
  the self-typed `TypeMapper<TMapper>` and
  `MappingBuilder<TMapper, TSource, TDestination>` API.
- Specialize generated fluent configuration for every mapper or reusable
  mapper family. Independent mappers may configure the same pair without
  competing extensions, including when their assemblies expose internals to
  one another. Tuple names, nullable annotations and `dynamic` remain local
  to the declaring configuration.

### Fixed

- Keep generated construction and member type names distinct across assemblies
  that expose their internals to one another.
- Explain that standalone nested `Update` requires a member selected through
  the generated `Members` callback result, including in `MORPH0046`.
- Keep generated destination plans under the reserved `Morphant.Generated`
  root so generation cannot introduce a nested `Morphant` namespace that
  shadows runtime API references in user code.
- Keep generated plan type names distinct for otherwise ambiguous nested
  destination shapes such as `Outer<T>.Destination` and
  `Outer1.Destination<T>`.
- Reject callback bindings to another mapper family or assembly with
  `MORPH0018`, including an unintended fallback to a base-family overload.
  Preserve potentially competing callback calls until generated overload
  resolution can select Morphant's method or explain the invalid configuration.
- Report `extern alias`-only mapping types and required constraints as
  inaccessible instead of emitting invalid `global::` references and cascaded
  compiler errors; reject globally ambiguous aliased types and namespace/type
  path collisions for the same reason.
- Prevent incremental generator crashes when IDEs replace syntax trees,
  expose referenced projects as source-backed compilations, or ask newer
  Roslyn hosts to filter a cached diagnostic whose source tree was replaced,
  so live generated documents remain available after solution and editor
  updates.
- Reject loosely constrained reusable mapper bases with `MORPH0058` before
  their mapper-family configuration methods can leak into unrelated scopes.
- Preserve tuple element names and nullable annotations when generic mapper
  family types are closed, avoiding false `MORPH0056` conflicts during
  configuration inheritance.

### Migrating from 0.4.0

Give each concrete mapper its own self type:

```csharp
// Before
public partial class OrderMapper : TypeMapper

// After
public partial class OrderMapper : TypeMapper<OrderMapper>
```

Keep `[MorphantMapper]` and the `Configure(MapperBuilder builder)` override.
If you use a reusable base, pass the final mapper type through every layer:

```csharp
public abstract class CommonMapper<TMapper> : TypeMapper<TMapper>
    where TMapper : CommonMapper<TMapper>
```

The concrete mapper then inherits `CommonMapper<OrderMapper>`. Keep the
`base.Configure` and `IncludeBase` calls described in
[Configuration inheritance](docs/configuration-inheritance.md).

If your configuration explicitly names builder types, replace
`Morphant.MapperBuilder` with the inherited `MapperBuilder`, and
`MapperBuilder<TSource, TDestination>` with
`MappingBuilder<TMapper, TSource, TDestination>`. For explicit generated
callback result types such as `OrderMembers`, update imports to the namespace
shown by the IDE for the assembly containing the mapper. Short type names
remain unchanged.

Every additional generic family parameter must participate in each declared
pair ([`MORPH0060`](docs/diagnostics/MORPH0060.md)). Nested mappers and their
containing types must be accessible to generated code
([`MORPH0059`](docs/diagnostics/MORPH0059.md)).

Application-side `IMapper.Map`, direct `Create`/`Update` calls and DI
registration keep their existing form.

## [0.4.0]

### Added

- Add explicit pair-local runtime polymorphism with `ForDerived`,
  most-specific class/interface selection, strict Update destination checks,
  value-type support, nested and DI routing, and typed runtime failures.
- Add `UnknownDerivedTypeHandling` at assembly, mapper and mapping levels,
  including strict closed-hierarchy handling and dedicated diagnostics.

### Changed

- Omit the redundant `ByConvention()` construction overload when a destination
  has no supported constructor parameters; parameterless construction uses
  `new()` directly.

## [0.3.0]

### Added

- Add `IncludeMembers` for opting selected nested source objects into
  constructor and destination-member conventions, including nullable paths,
  one-call multi-scope selection, `IncludeBase` composition, source-validation
  discards, and dedicated diagnostics for invalid or ambiguous selections.
- Add automatic source flattening for convention mappings, including nullable
  path handling, constructor and `IncludeMembers` support, configurable
  `Flattening` defaults, and an ambiguity diagnostic that never guesses a
  source path.

## [0.2.0]

### Added

- Add opt-in Git snapshots of generated mapper implementations with
  `MorphantGitSnapshot`. Snapshots update after successful builds, preserve the
  last successful output after failed builds, and are excluded from
  compilation.
- Add settings for snapshot detail, location, and target frameworks. Mapper
  implementations are saved by default; multi-target projects save only the
  last declared target framework unless configured otherwise.

## [0.1.0]

Initial stable release.

### Added

- Compile-time Create and Update mappings generated from explicit
  configuration.
- Convention and explicit destination construction and member mapping.
- Manual whole-value mappings with `Convert`.
- Explicit nested mappings and runtime dispatch through DI and `IMapper`.
- Mapper settings, mapping inheritance and configuration composition.
- Forty-eight documented compile-time diagnostics and typed runtime
  exceptions.
- C# 9 and newer consumer support on Roslyn 4.4.0 or later.
- Strong-named runtime and generator assemblies with public key token
  `ba27fb6be8f80649`.

[Unreleased]: https://github.com/strangeman375/Morphant/compare/v0.4.0...HEAD
[0.4.0]: https://github.com/strangeman375/Morphant/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/strangeman375/Morphant/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/strangeman375/Morphant/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/strangeman375/Morphant/releases/tag/v0.1.0
