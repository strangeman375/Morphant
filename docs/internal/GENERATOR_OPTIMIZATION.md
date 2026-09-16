# Generator optimization checkpoint

Authorized scope: implement all six findings from the generator review.
Preserve public behavior, complete generated sources and hint names, diagnostic
content and locations, evaluation order, and incremental invalidation boundaries.
DSL lambdas do not introduce additional runtime value-copy boundaries.

Planned stages:

- Reuse equivalent registration analyses and collect shared type dependencies once.
- Reuse constructor parameter resolution for planning and diagnostics; batch
  independent constructor and flattening compatibility probes.
- Replace repeated dependency suffix scans with indexed later-use queries.
- Prepare path-independent mapping inputs once before Create/Update specialization.
- Run affected tests on supported Roslyn hosts, full Release build and both test
  suites; compare generated output and measure representative scaling scenarios.

Keep caches local to an analysis/compilation. Keep actual changed-argument
constructor rebinding, nullable checks and warning handling intact. Preserve
separate analysis where generic substitutions or execution paths change inputs.

Status: registration reuse and shared dependency traversal passed 200 focused
tests. Constructor parameter analysis is shared with diagnostics; its focused
validation and probe batching are pending. Baseline local commit: `5d72659c`;
corresponding remote main: `b41cd7e6663b4f8e06e31b4752d9d858e4ddef27`.
This temporary checkpoint will be removed after validation.
