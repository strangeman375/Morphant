# Changelog

Morphant follows Semantic Versioning. Patch releases within a `0.x` minor
line preserve compatibility. Until `1.0`, minor releases may contain
documented breaking changes.

See [current limitations](docs/limitations.md) for features not yet included.

## [Unreleased]

### Added

- Support `ValueTuple` and `System.Tuple` mappings, including named, unnamed,
  long and nullable tuples, multiple inputs and outputs, and call-specific data.
- Report unexpected generator failures as `MORPH0057`, with a generated report
  to attach when reporting the problem.
- Diagnose invalid mapper declarations and unsupported polymorphic or
  construction rules instead of producing invalid C#.

### Changed

- Introduce the self-typed `TypeMapper<TMapper>` and
  `MappingBuilder<TMapper, TSource, TDestination>` API. Independent mappers can
  configure the same pair separately.
- Return the existing destination directly from `Resolve` after checking its
  availability. Use `ConstructUsing` or `ResolveUsing` for factories and
  arbitrary destination objects; see the migration instructions below.
- Let nested `Map` in `Members` update a child supplied by construction or a
  factory, including during outer Create, and retain the nested result.
- Reduce generator work and memory use for large projects. Keep generated code
  compact and preserve the readability of user-written expressions.
- Use file-scoped namespaces in generated files for C# 10 and newer while
  retaining C# 9 compatibility.
- Use stable generated filenames and shorter namespaces for callback result
  types. Publish Git snapshots for each enabled target framework.

### Fixed

- Keep generated mappings, declarations and diagnostic locations current after
  source edits and project-reference changes.
- Preserve independent generated output after generator failures or filename
  conflicts, and keep failure reports available.
- Preserve callback behavior, including evaluation order, side effects,
  nullability, overload selection, caller information and interpolated strings.
- Keep relevant C# warnings visible without unnecessary generated duplicates.
- Evaluate explicit constructor and member expressions independently; share
  values only with automatic constructor arguments. Preserve reads of the
  initial `result` and diagnose circular construction dependencies.
- Correct tuple mapping and source-usage validation, inherited callbacks and
  checks for existing-destination reuse.
- Correct Git snapshot updates after renames and with custom output paths;
  preserve compilation hooks and package the correct assemblies.
- Clarify documentation, shorten IntelliSense and verify mapping examples.

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

For Git snapshots:

- Replace `MorphantGitSnapshotTargetFrameworks` with MSBuild conditions on
  `MorphantGitSnapshot`; see [snapshot configuration](docs/generated-code.md).
- Delete the old `.morphant` file once and rebuild to update the snapshot
  format.
- A successful compilation replaces old snapshot filenames with the new ones.
  Commit those renames together with your mapping changes.

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
