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
- Generate stable shared, mapper-scoped, and mapper-family-scoped fluent
  surfaces so unrelated mappers can use different erased C# presentations of
  one CLR mapping pair.
- Report an invalid mapper self type as `MORPH0058` and an inaccessible mapper
  declaration as `MORPH0059`.

### Changed

- Give generated tuple templates compact identity-based namespaces and the
  fixed leaf names `TupleConstructorParameters`, `TupleConstruction` and
  `TupleMembers`.
- Replace the non-generic `TypeMapper` and two-argument mapping builder with
  the self-typed `TypeMapper<TMapper>` and
  `MappingBuilder<TMapper, TSource, TDestination>` API.
- Scope conflicting tuple presentations to one effective mapper. Unrelated
  mapper families may now use different tuple names, nullable annotations, or
  `dynamic`/`object` presentations for the same physical CLR pair.

### Fixed

- Prevent incremental generator crashes when IDEs replace syntax trees,
  expose referenced projects as source-backed compilations, or ask newer
  Roslyn hosts to filter a cached diagnostic whose source tree was replaced,
  so live generated documents remain available after solution and editor
  updates.

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
