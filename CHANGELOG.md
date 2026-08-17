# Changelog

Morphant follows Semantic Versioning. Patch releases within a `0.x` minor
line preserve compatibility. Until `1.0`, minor releases may contain
documented breaking changes.

## [Unreleased]

### Added

- Add a supported, opt-in Git snapshot lifecycle through
  `MorphantGitSnapshot`, with target-framework isolation, automatic compiler
  exclusion, a current-file manifest, post-success publication, self-healing
  missing files, and stale-file cleanup enabled by default.

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

[Unreleased]: https://github.com/strangeman375/Morphant/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/strangeman375/Morphant/releases/tag/v0.1.0
