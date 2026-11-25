# Documentation guidelines

These rules apply to public Markdown, XML IntelliSense, diagnostic help,
changelog, release or package text and internal documents. Follow the general
principles in [AGENTS.md](../../AGENTS.md).

## Audience and placement

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
- Keep navigation task-oriented, with basic usage before composition and
  specialized features. Remove superseded pages and update their incoming
  links. Keep diagnostic help URLs shipped in released packages working.
  For a removed diagnostic, retain its published help page at the original
  URL with the removal version and migration guidance. Use version numbers
  only for actual compatibility requirements or history.

## Content

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
- Changelog entries describe observable improvements, fixes and migrations.
  Group related fixes, omit internal mechanisms and test inventories, and
  preserve breaking-change instructions. Do not rewrite published releases.
  Update package metadata and release notes together when selecting a release
  version.
- Date internal research and mark assumptions superseded by current behavior.
  Preserve historical measurements as historical evidence, not current promises;
  unselected research remains a proposal, not a roadmap commitment.

## Updating and publishing

- For a user-visible change, review the relevant guides, API, settings,
  diagnostics, limitations, index and changelog. Edit only the pages that need
  it, removing stale statements instead of layering exceptions onto them.
- Before publishing, reread changed pages in full, review the beginner route,
  search for contradictory wording and repeated explanations, and validate
  links and anchors.
- Publish intermediate checkpoints under the
  [repository workflow](../../AGENTS.md#repository-workflow). Complete the
  [checks for the change](TESTING_GUIDELINES.md#running-verification) before
  reporting the stage as verified.
