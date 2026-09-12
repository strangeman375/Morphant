# Changelog

Morphant follows Semantic Versioning. Patch releases within a `0.x` minor
line preserve compatibility. Until `1.0`, minor releases may contain
documented breaking changes.

See [current limitations](docs/limitations.md) for features not yet included.

## [Unreleased]

### Added

- Diagnose unsupported `Construct` and `Resolve` results with `MORPH0062`,
  directing factories and cached objects to the corresponding `...Using` method.
- Add first-class `ValueTuple` and `System.Tuple` mappings, including named,
  unnamed, long and nullable forms, Create and Update behavior, typed
  composition of multiple inputs and outputs, call-specific state, and a
  diagnostic for conflicting tuple presentations.
- Report unexpected generator failures as `MORPH0057`, with a generated
  report to attach when reporting the problem.
- Report an invalid mapper self type as `MORPH0058` and an inaccessible mapper
  declaration as `MORPH0059`.
- Report a reusable mapper-family parameter that is absent from a declared
  mapping pair as `MORPH0060`.

### Changed

- Convert from `Destination` to the generated construction type, instead of
  `Option<Destination>`. In `Resolve`, return guarded `previous.Value` or the
  value from `TryGetValue`; unchanged local aliases are supported.
- Simplify null checks in generated mappings where conditional access preserves
  the original behavior.
- Preserve user-written expressions and locals in generated mappings. Add
  supporting variables only where mapping semantics require them.
- Publish a Git snapshot for every successful compilation's target framework.
  Remove `MorphantGitSnapshotTargetFrameworks`; use MSBuild conditions on
  `MorphantGitSnapshot` to opt individual compilations in or out.
- For existing Git snapshots, delete the old `.morphant` file once and rebuild
  to update the snapshot format.
- Use short, uniform namespaces for generated construction and member types,
  while preserving readable type names. Update explicit imports and aliases
  to the namespaces shown by the IDE.
- Replace the non-generic `TypeMapper` and two-argument mapping builder with
  the self-typed `TypeMapper<TMapper>` and
  `MappingBuilder<TMapper, TSource, TDestination>` API.
- Allow independent mappers to configure the same pair without competing
  configuration methods, including different tuple names and nullability.

### Fixed

- Correct `previous` availability checks through local guards and switches,
  including reads in arguments and initializers before a guard.
- Accept reuse of struct aliases after calls on separate copies or
  reference-type members, while rejecting calls that can modify the alias.
- Clarify documentation, shorten IntelliSense, and verify mapping examples.
- Package the correct generator and build task assemblies with custom build
  output paths.
- Keep generated mappings current after IDE edits and project-reference
  changes, including changes to `InternalsVisibleTo`.
- Correct Git snapshot updates after renames and with custom output paths;
  improve build messages and preserve existing compilation hooks.
- Correct mapping and source-usage validation for long tuple elements.
- Preserve inherited callback behavior, including tuple field reads after
  generic substitution, conditional method calls, element names and nullability.
- Preserve null propagation and operator precedence in callback expressions
  that combine conditional access with extension methods, including deferred
  lambdas and local functions. Accept explicit static extension-method calls.
- Preserve arithmetic and overload selection when simplifying constant
  conditions. Accept `context.Operation` inside constructor argument expressions.
- Preserve independent explicit constructor and member evaluations, even for
  identical expressions. Share member values only with automatic constructor
  arguments, and diagnose circular dependencies on `result`.
- Preserve reads of the initial `result` in member expressions. Avoid an
  intermediate tuple when member rules do not need it.
- Avoid generated-name conflicts across mappers, assemblies and nested types.
  IntelliSense offers aliases for reserved destination-member names.
- Accept `Resolve` branches guarded by `previous.TryGetValue`.
- Report invalid callback overloads and inaccessible mapping types as Morphant
  diagnostics instead of producing invalid C#.
- Diagnose generic `ForDerived` branches whose ordering depends on unknown
  type arguments with `MORPH0061`.

### Migrating from 0.4.0

Replace `return previous` in `Resolve` with `return previous.Value` after the
availability check, or return the variable from `TryGetValue`. For a mixed
conditional expression, name the construction type explicitly in its creation
branch. Use `ConstructUsing` or `ResolveUsing` for arbitrary destination objects.

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
