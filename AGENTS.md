# Morphant repository instructions

## Scope and decisions

- Preserve the user's agreed scope and contracts. Current documentation and
  tests describe those contracts; a passing test or existing output does not
  justify a behavior that contradicts them. Git history is historical context.
- Make routine implementation choices within the agreed scope. Ask about
  unresolved decisions that materially affect scope, public API, behavior or
  generated-code form; explain alternatives and tradeoffs before implementing
  the affected work. Continue independent work whose intent is clear.
- Ask before adding unrequested refactoring, optimization, cleanup or redesign.
  Do not reinterpret earlier agreements without an explicit user instruction.
- Define the contract and support boundary of a new feature before implementing
  it. Unselected ideas are not roadmap commitments. Superseded designs are not
  compatibility targets; remove obsolete code, tests and documents.

## Required reading

- Before changing generation, DSL semantics, runtime mapping helpers or
  generated-source expectations, read
  [Generator contracts](docs/internal/GENERATOR_CONTRACTS.md).
- Before changing tests, test infrastructure, generated-output expectations or
  verification policy, read
  [Testing guidelines](docs/internal/TESTING_GUIDELINES.md).

## Code quality

- Simplicity, optimality, readability and conciseness of generated code are
  first priorities, equal to correctness. Keep changes within the requested
  behavior and justified readability improvements.
- Preserve the user's computation structure, independent evaluations and
  observable behavior under the generator contracts. Do not add generated
  surface area or compatibility branches without a user-facing need.
- Runtime reflection is unsupported. Diagnose polymorphic relationships that
  cannot be established at generation time.
- Ordinary project source namespaces follow their directories. Polyfills and
  deliberate compiler fixtures may require different namespaces; generated
  namespaces follow the generator contracts.

## Documentation

- Public Markdown, XML IntelliSense, diagnostic help and release or package
  text describe user tasks and observable contracts. Before adding text,
  identify the reader's task and the minimum information needed to complete it.
  An internal fix or new regression test does not by itself require public prose.
- Keep implementation details in `docs/internal`: planner, lowering, emitter,
  caching, hashing and generated namespace or layout decisions. Public docs
  must not become an implementation log or a catalogue of past regressions.
  `docs/generated-code.md` explains how to view and snapshot generated code.
- Place information by audience and purpose:
  - README and quick start: the shortest ordinary path to a working mapping.
  - Guides: common tasks and the behavior needed to use them correctly.
  - API and settings references: choices, defaults and essential conditions.
  - Diagnostic help: the cause, a practical fix and relevant uncommon cases.
  - Internal docs: architecture, development rules and research.
- Give each detailed contract one canonical explanation. Link to it from
  related pages instead of repeating it. Do not add a general-guide paragraph
  for every edge case; update diagnostic help when troubleshooting needs it.
- Keep XML IntelliSense short: purpose, decision-critical conditions and a
  Markdown link. Describe behavior in public API terms, without internal terms
  such as plans, lowering or physical representations.
- Preserve facts users rely on, including nullability, reuse, evaluation and
  side effects, settings precedence, and the difference between an ignored
  setting and an invalid configuration. Check claims against implementation
  and tests; do not generalize a diagnostic beyond the cases that produce it.
- For settings changes, review public XML and `docs/settings`. Explain the
  default, applicable operations, inheritance and a minimal example; keep the
  shared precedence rules on the settings overview. Do not silently change the
  agreed behavior to simplify its description.
- Keep navigation task-oriented, with basic usage before composition and
  specialized features. Remove superseded pages and repair incoming links.
  Use version numbers only for actual compatibility requirements or history.
- Changelog entries describe observable improvements, fixes and migrations.
  Group related fixes, omit internal mechanisms and test inventories, and
  preserve breaking-change instructions. Do not rewrite published releases.
- Date internal research and mark assumptions superseded by current behavior.
  Preserve historical measurements as historical evidence, not current promises;
  unselected research remains a proposal, not a roadmap commitment.
- For a user-visible change, review the relevant guides, API, settings,
  diagnostics, limitations, index and changelog. Edit only the pages that need
  it, removing stale statements instead of layering exceptions onto them.
- Before publishing, reread changed pages in full, review the beginner route,
  search for contradictory wording and repeated explanations, validate links
  and anchors, and run `git diff --check`. For changed executable examples or
  generated XML, run the relevant documentation-example tests and review full
  generated-source snapshots under `docs/internal/TESTING_GUIDELINES.md`.

## Repository workflow

- Work in the selected canonical checkout and preserve unrelated changes.
  Environment-specific paths and tools belong in workspace instructions.
- **Hard checkpoint rule:** commit and publish every completed implementation
  stage and meaningful intermediate checkpoint directly to remote `main`
  before starting the next stage. This includes states that do not build or
  pass tests; describe that state in the commit message. Do not postpone
  publication until validation, cleanup or the end of the task. Verify each
  published checkpoint on remote `main`. Only an explicit user instruction
  changes this rule.
- Commit only actual file changes. Report validation and review results in the
  conversation; never create empty checkpoint or validation commits.
- Split long work into small coherent checkpoints. Preserve progress before
  long builds or external operations. Persist new agreed design decisions and
  completion criteria before a long implementation.
- Before publishing, verify the exact committed file set. Update shared
  branches only by ordinary fast-forward; never force-push.
- Follow the testing guidelines for verification scope. Reuse completed checks
  while the tested content is unchanged; rerun only affected validation after
  a change. Update package metadata and release notes together when selecting
  a release version.
- If an operation stalls, inspect or stop it and report a blocker when recovery
  is unclear. Keep the user informed at meaningful milestones.
- Keep tool output and review records focused. Remove temporary reports and
  probes after lasting decisions are captured in maintained instructions,
  documentation and tests.

## Maintaining instructions

- Record durable rules once, in the narrowest applicable document. Keep this
  file focused on general principles and required reading; keep implementation
  contracts in the linked internal documents and machine setup outside the
  repository.
- When updating instructions, check them against the current code and tools,
  repair links, consolidate duplicates and remove completed-stage wording.
  Use Git history for approval dates and past decisions; retain the substance
  of every still-active agreement.
