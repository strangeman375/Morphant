# Morphant repository instructions

## Scope and decisions

- Preserve the user's agreed scope and contracts; do not reinterpret earlier
  agreements without an explicit user instruction. Current documentation and
  tests describe those contracts; a passing test or existing output does not
  justify a behavior that contradicts them. Git history is historical context.
- Make routine implementation choices within the agreed scope. Ask about
  unresolved decisions that materially affect scope, public API, behavior or
  generated-code form; explain alternatives and tradeoffs before implementing
  the affected work. Continue independent work whose intent is clear.
- Ask before adding unrequested refactoring, optimization, cleanup or redesign.
- Define the contract and support boundary of a new feature before implementing
  it. Unselected ideas are not roadmap commitments. Superseded designs are not
  compatibility targets; remove obsolete code, tests and documents.

## Required reading

- Before changing generation, DSL semantics, runtime mapping helpers or
  generated-source expectations, read
  [Generator contracts](docs/internal/GENERATOR_CONTRACTS.md).
- Before changing tests, test infrastructure, generated-output expectations or
  verification policy, and before verifying a change, read
  [Testing guidelines](docs/internal/TESTING_GUIDELINES.md).
- Before a user-visible change or a change to public documentation, XML
  IntelliSense, diagnostic help, changelog, release or package text or
  internal documents, read
  [Documentation guidelines](docs/internal/DOCUMENTATION_GUIDELINES.md).

## Code quality

- Simplicity, optimality, readability and conciseness of generated code are
  first priorities, equal to correctness. Limit generated-output changes to
  the requested behavior and justified readability fixes.
- Ordinary project source namespaces follow their directories. Polyfills and
  deliberate compiler fixtures may require different namespaces; generated
  namespaces follow the generator contracts.

## Repository workflow

- Work in the selected canonical checkout and preserve unrelated changes.
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
