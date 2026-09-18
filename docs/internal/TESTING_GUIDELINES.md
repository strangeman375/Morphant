# Testing guidelines

These rules apply when changing tests, test infrastructure, generated-output
expectations or verification policy. They are intentionally kept outside
`AGENTS.md` so unrelated work does not load detailed test guidance.

## Test design

- When a review needs behavior not covered by existing tests, add permanent
  tests to the normal suite. Temporary probes are exploratory aids, not a
  substitute for regression coverage committed to main and run by CI.
- Test only the current documented contract. Historical behavior may inspire a
  scenario, but rewrite it against the current API instead of preserving it as
  a compatibility test.
- Treat the generator as a black box. Start from supported user scenarios, not
  branches in the implementation.
- For every new or changed test, inspect its generated code against the input
  and [generator contracts](GENERATOR_CONTRACTS.md). Check bindings, evaluation
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

- Tests for generated declaration surfaces must trigger generation with bare
  `Map<TSource, TDestination>()` registrations. Calls to generated
  `Construct`, `Convert`, `Members` or similar methods belong only in an
  explicit Usage category.
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
  Instantiate the generated mapper normally, cast it to the exact
  `ITypeMapper<,>` contract, and call `Create` or `Update` directly.
- The integration host may call an already compiled scenario method. It must
  not create a `CSharpCompilation`, run a `GeneratorDriver`, emit or load an
  assembly, or invoke the scenario through reflection.
- Consumer assemblies
  `Morphant.Generator.IntegrationTests.CSharp9`,
  `Morphant.Generator.IntegrationTests.CSharp11`, and
  `Morphant.Generator.IntegrationTests.Latest` use analyzer-style project
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

## Running verification

- Run focused tests for the affected category while iterating.
- Select verification by the effect of the change. File or category counts
  alone do not require the full suite.

| Change | Required verification |
| --- | --- |
| Instructions or prose without executable examples | Review the complete changed text, references and anchors; run `git diff --check`. |
| Executable documentation examples | Run the affected documentation-example and link checks. |
| XML documentation or mechanical snapshot updates | Review the full generated sources and run affected documentation/compiler unit tests. Runtime checks are needed when observable behavior changes. |
| Mapping, generation or diagnostic behavior | Run affected unit tests and applicable MSBuild runtime scenarios. Run the full Release build and both test projects for broad behavioral changes or shared build/test-infrastructure changes. |
| Packaging or MSBuild integration | Run affected ordinary consumer and package tests, including the minimum-SDK checks when that compatibility is affected. |

- Before a release, run the full Release build and both test projects, then
  inspect the final NuGet artifacts. Report which local and CI checks actually
  completed and which remain pending.

### Roslyn compatibility

- For Roslyn-facing changes, verify affected scenarios on the minimum supported
  host and the newer host validated by CI. The baseline is declared in
  [Directory.Build.props](../../src/Directory.Build.props); the newer host and
  executable procedure are in
  [test-shipped-generator.sh](../../eng/test-shipped-generator.sh).
- Build the generator against the minimum Roslyn version once. Keep that exact
  binary while building the newer test host without its project references;
  verify its checksum and the copy loaded by the tests before running them.
  Rebuilding the generator for each host does not verify the shipped binary.
- Use the script or the same sequence with a focused test filter. When shared
  output directories were used, restore baseline dependencies and rebuild
  affected outputs before resuming ordinary `--no-restore` / `--no-build` runs,
  including after a failed compatibility check. Follow any workspace-specific
  forced-restore procedure; a metadata-only restore cache cannot detect a
  command-line Roslyn-version override.
