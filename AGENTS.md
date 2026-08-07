# Morphant repository instructions

## Implementation plan

- `MAPPING_API_IMPLEMENTATION_PLAN.md` is the current agreed roadmap and
  progress record. Read it before selecting the next work slice.
- `IMPLEMENTATION_PLAN.md` is the archived roadmap for the superseded
  `Template()` design. Keep it for history; do not use it to select work.
- The superseded `Template()` design is not a compatibility target. Do not
  keep its production code compiling, update its snapshots, or run its tests.
  Its complete pre-cleanup reference slice lives under
  `reference/legacy-template-design`; keep that slice outside all projects and
  do not maintain it. Before deleting future obsolete code, preserve anything
  with plausible algorithmic, testing, or historical value in a similarly
  excluded reference location.
- Use the next incomplete plan item by default, but treat the plan as a working
  document rather than an immutable specification. If new information suggests
  a better sequence or scope, explain the proposed change and agree it with the
  user before deviating from the current roadmap.
- The plan may be clarified, extended, split, combined, reordered, or reduced
  after that agreement. Update it when the direction changes so future work
  does not have to reconstruct the decision from conversation history.
- When the plan says that a support boundary must be agreed separately, agree
  it with the user before writing tests or production code for that slice.
- Update the plan in the same coherent change that completes a work item or
  applies an agreed plan revision. Mark completed items and identify the next
  item. Do not duplicate changing plan status in this file.

## Test design

- Only the current `Construct` / `Members` / `Convert` contract belongs in
  production tests. Historical behavior may inform a new test scenario, but
  the scenario must be rewritten against the current contract rather than
  preserved as a compatibility test.
- Treat the generator as a black box when designing tests. Start from supported
  user scenarios, not from branches in the current implementation.
- Production pipelines may only be referenced by the minimal test generators
  that invoke the code under test. Expected values and assertions must use
  literal or test-owned data; never compute them with generator helpers,
  models, emitters, or constants.
- Every test category must be a complete specification of its own concern. It
  must remain useful and sufficient if all other test categories are removed.
- Do not omit a scenario merely because another category happens to exercise
  it. Deliberate overlap between categories is acceptable.
- Tests that specify generated declaration surface must derive it from bare
  `Map<TSource, TDestination>()` registrations. Do not call generated
  `Construct`, `Convert`, `Members`, or similar methods in their input code;
  consumer calls belong only in an explicit Usage category. This keeps the
  generation trigger independent from use of the generated API.
- Optimize expected test code for human review, not for reducing repeated
  lines. Keep complete generated sources visible as local raw string literals
  in the test that specifies them. A large expected source may be split into
  clearly named, test-owned literal sections, but shared builders, emitters,
  or parameterized helpers must not synthesize the expected API or generated
  file structure. Test infrastructure may only normalize and register an
  already readable expected source.
- Create a category subdirectory only when it contains more than one test
  file. Keep single-file categories directly in their parent test directory.
- Keep focused model/emitter behavior, exact composed-generator output and the
  reflection-based public API inventory in the unit-test project. End-to-end
  runtime scenarios belong in the dedicated integration slice, but their
  source must be compiled by MSBuild as ordinary consumer code. Define the
  mapper and scenario in an analyzer-backed consumer assembly, instantiate the
  generated mapper normally, cast it to the exact `ITypeMapper<,>` contract and
  call `Create` / `Update` directly. The integration test host may call the
  already compiled scenario method; it must not create a `CSharpCompilation`,
  run a `GeneratorDriver`, emit/load an assembly, or invoke the scenario through
  reflection. Keep unit-test helpers limited to exact generated output and
  focused compiler/model verification; do not reintroduce general user-scenario
  runtime execution there. The test-owned actualization harness may emit and
  execute a step only to prove that one preserved `GeneratorDriver` applies the
  newly generated semantics after an edit; it remains a focused incremental
  test and is not a substitute for integration scenario coverage. Real consumer
  assemblies under `Morphant.Generator.IntegrationTests.CSharp9`,
  `Morphant.Generator.IntegrationTests.CSharp11` and
  `Morphant.Generator.IntegrationTests.Latest` use analyzer-style project
  references and define the package-consumer boundary.

## Generated code

- A source file's namespace must match its directory under the owning project.
  This is a repository law, not a stylistic preference. For example, files in
  `src/Morphant/Context` use `Morphant.Context`; do not move a file while
  preserving its previous namespace.
- The minimum supported user language version is C# 9. Tests may additionally
  cover newer syntax by selecting the required `LanguageVersion` explicitly.
- Generated construction/member plans mirror the destination input contract.
  Preserve nullable value/reference types, `AllowNull` / `DisallowNull`,
  oblivious annotations, optional parameters, and default values exactly as
  specified by the current mapping API design.
- Generated files use deterministic CRLF line endings, start with
  `// <auto-generated />`, and contain `#nullable enable`.
- Generated hint names use
  `Morphant.Generated.<ArtifactKind>.<StableIdentity>.g.cs`, with a singular
  artifact kind.
- A destination in the global namespace uses the plan namespace
  `Morphant.Generated`, referenced as `global::Morphant.Generated`. Do not
  synthesize a `Global` namespace segment: it is not the C# `global::` alias
  and would collide with destinations in a real namespace named `Global`.
- Add a stable hash suffix only when two generated files of the same artifact
  kind have an actual case-insensitive hint-name collision after sanitization.
- Keep generated surface and binary size small. Do not add generated members,
  attributes, or compatibility branches without a concrete user-facing need.
- Diagnostics are a separate future scope. Unsupported cases currently skip
  generation unless a test explicitly specifies otherwise.

## Tests and verification

- Use NUnit and `Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing`.
- Generated-source assertions must compare the complete generation result:
  the exact set of hint names and the full content of every generated file.
  Never validate generated output through substring presence, absence,
  occurrence counts, or relative substring positions. An expected result may
  be assembled from test-owned literal parts when that improves readability,
  but the final assertion must remain complete. Tests are executable project
  documentation and should make the resulting API understandable.
- Include compiler warnings in verification. Test harnesses must use
  `CompilerDiagnostics.Warnings` or a stricter level and must not expose an
  "allow warnings" path. Mark warnings that are an intentional part of a
  scenario at their exact span, including warnings in expected generated
  sources. Fix unrelated warnings instead of accepting them wholesale. A
  narrow `CS1591` suppression is allowed only for undocumented input-fixture
  declarations when XML documentation is unrelated to the scenario; it does
  not apply to generated files.
- Use `#nullable enable` in test inputs by default. Use
  `#nullable disable annotations` only when a test explicitly specifies an
  oblivious input contract, and keep it scoped to that type or member. It
  preserves the distinction between oblivious and non-nullable reference
  types; it is not a way to silence nullable flow warnings.
- Run only focused tests for the changed category. The user runs the full test
  suite periodically and reports failures; do not run the full suite, including
  before committing or pushing. Use the focused-test command supplied by the
  enclosing workspace instructions when available. For the dedicated stage-22
  integration slice, run its test project directly; it is one focused category
  and is not included by the unit-test helper.

## Settings documentation

- Every settings implementation slice must update the public XML
  documentation and the user-facing documentation in `docs/settings`.
- Document the default, inheritance and precedence rules, behavior at each
  supported configuration level, disabled or unsupported operations, and a
  minimal usage example.
- Treat the documented design as revisable under the implementation-plan
  rules above. Propose improvements when implementation reveals a clearer
  contract, and agree user-visible changes before applying them.

## Repository workflow

- Preserve unrelated user changes.
- Standing authorization: when the user asks to implement or change something
  in this repository, work directly in local `main`, commit each completed
  coherent change, and publish it to remote `main` without asking again in
  later turns. This authorization persists across Work sessions.
- Before publishing, verify the exact committed file set and update remote
  `main` only by ordinary fast-forward. Never force-push. Stop for direction if
  the user explicitly requests a branch or pull request, if remote `main` has
  conflicting concurrent changes, or if fast-forward publication is not
  possible.
- Keep publication proportional to the change. The normal direct-to-`main`
  connector flow is:
  1. In one local pass, verify the intended file set, create the commit, and
     record its tree SHA.
  2. Read the remote `main` head once and use its cached or returned tree as the
     base tree.
  3. Create one remote tree. Put changed UTF-8 text content directly in its tree
     entries; create separate blobs only for binary, oversized, or
     connector-incompatible files.
  4. Compare the resulting remote tree SHA with the local commit tree SHA.
  5. Create one remote commit with the previously read head as its sole parent,
     then update `main` with `force: false`.
  6. Update the local publication cache and report the result once.
- Do not probe known-unavailable `git push` or `gh` transports on every
  publication, invoke a branch/PR workflow for normal Morphant changes, verify
  every text blob separately, repeatedly reread an unchanged remote head, or
  publish per-file progress reports. The final non-force ref update is the
  concurrency and fast-forward guard.
- Do not rerun tests or repeat a completed diff review merely because
  publication is starting. Run the focused validation once for the final tree;
  if the tree changes afterward, rerun only the affected validation.
- Keep tool output focused: use filtered test runs and avoid returning entire
  files or build logs when a smaller excerpt establishes the result.
- Prefer one repository audit, one coherent batch edit, one focused category
  run, and one final diff review. Expand or refresh large deterministic
  snapshots mechanically, then review and commit the readable literals; do
  not spend model turns reconstructing boilerplate or fixing one snapshot at
  a time when the same work can be batched without reducing coverage.
- Do not attach or link repository files in user-facing progress or final
  messages. The user reviews files only through commits published to `main`;
  report the concise change summary and remote commit instead.
