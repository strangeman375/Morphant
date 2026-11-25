# Testing guidelines

To select checks for a change and report their results, see
[Running verification](#running-verification).

## Test design

- When a review needs behavior not covered by existing tests, add permanent
  tests to the normal suite. Temporary probes are exploratory aids, not a
  substitute for regression coverage committed to main and run by CI.
- Test only the current documented contract. Historical behavior may inspire a
  scenario, but rewrite it against the current API instead of preserving it as
  a compatibility test.
- Treat the generator as a black box. Start from supported user scenarios, not
  branches in the implementation.
- When a new or changed test exercises source generation, inspect all code
  generated for that scenario against its input and the
  [generator contracts](GENERATOR_CONTRACTS.md). Check bindings, evaluation
  order, unnecessary locals or renaming, readability and formatting, including
  behavior outside the test's immediate assertion. Handle discovered defects
  within the agreed scope and add coverage for fixes; never accept an incorrect
  output merely because the generator currently produces it.
- Production pipelines may be referenced only by the minimal test generators
  that invoke the code under test. Expected values and assertions must use
  literal or test-owned data, never generator helpers, models, emitters or
  constants.
- Each category must completely specify its own concern and remain useful if
  other categories are removed. Do not omit a scenario merely because another
  category also exercises it.
- Check shared DSL rules through their applicable entry points, including
  `Construct` / `Resolve` and `ConstructUsing` / `ResolveUsing`. For callbacks
  with `previous`, include both reuse and replacement. Compiler-only and runtime
  checks do not replace full-source snapshots of evaluation order, binding or
  lifecycle behavior.
- For transferred caller information, cover all applicable callback surfaces
  with reviewed full-source snapshots and runtime checks of caller values,
  overloads and effects. Construction/member changes need runtime coverage of
  evaluation order and reuse; structured results also need valid and invalid
  provenance, aliases and branches, including their diagnostics.
- Create a category subdirectory only when it contains more than one test file.

## Generated-source unit tests

- Test generated declarations with bare `Map<TSource, TDestination>()`
  registrations. Put tests of how consumer calls bind to those declarations
  in an explicit Usage category. In tests of generated mapping behavior,
  configure the mapping with the DSL calls the scenario requires.
- Compare the complete generation result: the exact hint-name set and complete
  content of every generated file. Do not use substring presence, absence,
  occurrence counts or relative positions as substitutes.
- Generated-name tests must cover permanent IDs, stability across adjacent
  edits, sanitization/case/overflow collisions, suffix preservation, Unicode
  UTF-8 accounting, actual filesystem writes and GitSnapshot migration.
- Expand repetitive snapshot changes mechanically, then review their readable
  literals against the user input.
- Keep expected generated sources visible as local, test-owned raw string
  literals. Large sources may be split into clearly named literal sections,
  but shared builders, emitters or parameterized helpers must not synthesize
  expected APIs or file structure. Infrastructure may only normalize and
  register already readable expected source. Expected sources are executable
  documentation and should make the resulting API understandable.
- Verify final production-observable generated output, diagnostics, compiler
  results, incremental behavior and the reflection-based public API inventory
  in the unit-test project. Do not snapshot intermediate models, emitters or
  planner observations when final output exposes the same behavior.
- Incremental caching and invalidation are a separate build-time concern and
  may inspect tracked-step reasons through one preserved production generator
  driver. A test-owned actualization harness may emit and execute a step only
  to prove that newly generated semantics apply after an edit; it is not a
  substitute for integration coverage.
- For pipeline and cache changes, compare incremental sources and diagnostics
  with a clean production run after relevant edits and recovery. Verify current
  diagnostic locations and reuse of unaffected work.
- Failure-isolation tests must reach the affected production stage, preserve an
  independent mapper and verify recovery after a failure and requested
  cancellation. Tests of the guard through small synthetic generators alone
  do not protect its production wiring.
- Keep unit-test helpers limited to exact generated output and focused compiler
  or incrementality verification. Do not reintroduce general runtime user
  scenarios into the unit-test project.

## Compiler and nullability verification

- Use NUnit and
  `Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing`.
- Include compiler warnings in verification with
  `CompilerDiagnostics.Warnings` or stricter. Do not provide an
  allow-all-warnings path.
- Mark intentional warnings at their exact spans, including warnings in
  generated sources. Fix unrelated warnings. A narrow `CS1591` suppression is
  allowed only for undocumented input fixtures when XML documentation is
  unrelated to the scenario; it does not apply to generated files.
- Use `#nullable enable` in test inputs by default. Use
  `#nullable disable annotations` only for an explicitly oblivious type or
  member; do not use it merely to silence flow warnings.

## Runtime integration tests

- End-to-end runtime scenarios belong in the dedicated integration slice and
  must be compiled by MSBuild as ordinary consumer code.
- Define each mapper and scenario in an analyzer-backed consumer assembly.
  To test a generated implementation, instantiate the mapper and call
  `Create` or `Update` through its exact `ITypeMapper<,>` contract.
  To test application dispatch, including DI lookup, registration and scope
  lifetime, call `IMapper.Map`.
- The integration host may call an already compiled scenario method. It must
  not create a `CSharpCompilation`, run a `GeneratorDriver`, emit or load an
  assembly, or invoke the scenario through reflection.
- Consumer assemblies `Morphant.Generator.IntegrationTests.<Variant>`, one per
  language version or set of assembly-level settings, use analyzer-style project
  references and must not reference one another. Only the aggregating
  integration host references all of them.
- Each scenario owns its DTOs, mappers and domain fixtures and must not
  reference another scenario. Copy small fixtures instead of sharing them.
- Reusable infrastructure and cross-assembly fixture data belong in
  `Morphant.Generator.UnitTests.TestAssets`, under a folder identifying the
  owning scenario. Do not add a project when this assembly supplies the
  required boundary.
- Runtime DI tests use `Microsoft.Extensions.DependencyInjection`, including
  real scopes when scope behavior matters. Do not replace it with a custom
  `IServiceProvider` stub.
- Package tests share immutable package preparation, but each independent
  scenario owns its consumer workspace. Keep related build/edit/failure/recovery
  transitions in one test; split unrelated contracts into separately reported
  tests. Bound child `dotnet` processes and include their command and output
  when reporting a timeout.

## Running verification

- Run focused tests for the affected category while iterating.
- Select verification by the effect of the change. File or category counts
  alone do not require the full suite.
- Reuse completed checks while the tested content is unchanged; after a
  change, rerun only the affected validation.
- A stage is verified when its applicable checks pass. In the validation
  report, identify which checks passed locally or in CI and which failed
  or remain pending.
- Before publishing changes to instructions or documentation, including
  executable examples, XML documentation and release or package text, check
  the diff for whitespace errors. Use `git diff --cached --check` for the
  final staged changes or `git show --first-parent --check HEAD` for the
  checkpoint already committed at `HEAD`.

| Change | Required verification |
| --- | --- |
| Instructions or prose without executable examples | Review the complete changed text, references and anchors. |
| Executable documentation examples | Run the affected documentation-example and link checks. Review the full generated-source snapshots for the affected examples. |
| XML documentation or mechanical snapshot updates | Review the affected full generated-source snapshots and run the relevant documentation/compiler unit tests. For generated XML documentation, also run the affected documentation-example tests. Run runtime checks when observable behavior changes. |
| Mapping, generation or diagnostic behavior | Run affected unit tests and applicable MSBuild runtime scenarios. Run the full Release build and both test projects for broad behavioral changes or shared build/test-infrastructure changes. |
| Packaging or MSBuild integration | Run affected ordinary consumer and package tests, including the minimum-SDK checks when that compatibility is affected. |

- Before a release, run the full Release build and both test projects, then
  inspect the final NuGet artifacts.

### Roslyn compatibility

- For Roslyn-facing changes, verify affected scenarios on the minimum supported
  host and the additional hosts validated by CI. The baseline is declared in
  [Directory.Build.props](../../src/Directory.Build.props); all tested versions
  are listed in the [shared CI/release matrix](../../.github/workflows/validation.yml).
  Every row runs the same unit suite. The baseline row also builds the full
  solution, runs integration tests and enforces combined coverage; do not repeat
  its Linux test runs in the calling CI or release workflow. Run
  [test-shipped-generator.sh](../../eng/test-shipped-generator.sh) with the host
  version as its first argument and optional `dotnet test` arguments after it.
- Select the unit-test host with `MorphantTestRoslynVersion`; it defaults to
  `MorphantRoslynVersion`, the minimum version used to compile the generator.
  Restore and build the normal project graph once per host. The script compares
  the generator output with the copy loaded by the tests, and binary compatibility
  tests verify its Roslyn assembly references remain at the supported minimum.
  Never upgrade the generator's compile-time dependencies to select a test host.
- Use the script with `--filter` for focused verification. When shared
  output directories were used, restore baseline dependencies and rebuild
  affected outputs before resuming ordinary `--no-restore` / `--no-build` runs,
  including after a failed compatibility check. A metadata-only restore cache
  cannot detect a command-line Roslyn-version override.
