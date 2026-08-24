# Testing guidelines

These rules apply when changing tests, test infrastructure, generated-output
expectations or verification policy. They are intentionally kept outside
`AGENTS.md` so unrelated work does not load detailed test guidance.

## Test design

- Test only the current documented contract. Historical behavior may inspire a
  scenario, but rewrite it against the current API instead of preserving it as
  a compatibility test.
- Treat the generator as a black box. Start from supported user scenarios, not
  branches in the implementation.
- Production pipelines may be referenced only by the minimal test generators
  that invoke the code under test. Expected values and assertions must use
  literal or test-owned data, never generator helpers, models, emitters or
  constants.
- Each category must completely specify its own concern and remain useful if
  other categories are removed. Do not omit a scenario merely because another
  category also exercises it.
- Create a category subdirectory only when it contains more than one test file.

## Generated-source unit tests

- Tests for generated declaration surfaces must trigger generation with bare
  `Map<TSource, TDestination>()` registrations. Calls to generated
  `Construct`, `Convert`, `Members` or similar methods belong only in an
  explicit Usage category.
- Compare the complete generation result: the exact hint-name set and complete
  content of every generated file. Do not use substring presence, absence,
  occurrence counts or relative positions as substitutes.
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
- Run the full Release build and both test projects before a release or after
  changes spanning multiple categories. Run a dedicated integration project
  directly when only that slice is affected.
- `MorphantRoslynVersion` defaults to the minimum supported Roslyn host and is
  shared by the generator and unit-test host. For Roslyn-facing changes, run
  affected categories with the default and newest validated Roslyn version.
