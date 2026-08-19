# Changelog

Morphant follows Semantic Versioning. Patch releases within a `0.x` minor
line preserve compatibility. Until `1.0`, minor releases may contain
documented breaking changes.

## [Unreleased]

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

See [current limitations](docs/limitations.md) for functionality outside the
0.1 release.

[Unreleased]: https://github.com/strangeman375/Morphant/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/strangeman375/Morphant/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/strangeman375/Morphant/releases/tag/v0.1.0
