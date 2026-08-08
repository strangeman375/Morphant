# Core v0 scope and non-goals

This page describes the agreed core v0 target. The callback result-policy and
read-only proxy revisions are not yet implemented in the generated API;
current production progress is tracked in the
[mapping API roadmap](../MAPPING_API_IMPLEMENTATION_PLAN.md).

Core v0 validates Morphant's object-mapping architecture. It is an
architectural preview, not yet a general-purpose mapper release: collection
mapping is deliberately not included.

## Included

- application-wide exact-pair `IMapper` dispatch over manual registrations;
- generated `ITypeMapper.Create` and `ITypeMapper.Update` operations;
- generated structured `Construct` / `Resolve` for destinations with a
  supported constructor, including a sole parameterless constructor;
- pair-specific generated short and context-aware `ConstructUsing` /
  `ResolveUsing` runtime result policies for every eligible pair; the maximum
  overload ends in the real `MappingContext`;
- destination `Members`, `Auto`, `Ignore`, and exact-name conventions;
- fully manual synchronous `Convert` algorithms;
- `Option<T>`-based previous-destination presence;
- authoritative result, identity preservation, and explicit replacement;
- null-source/null-destination policies and mapping modes;
- explicit nested adaptive/Create/Update mappings;
- standalone nested Update for eligible read-only reference members through
  generated get-only proxies;
- nullable, value, record, interface/abstract Update, and constructed generic
  destinations within their documented capabilities;
- mapper-root and typed base-pair configuration inheritance;
- deterministic generated artifacts, actualization, and incremental cache
  isolation;
- typed observable runtime failures and complete generated exception stubs;
- C# 9 and newer consumers.

## Scenario audit

The main scenarios from section 13 of `MAPPING_API_DESIGN.md` map to the v0
surface as follows:

| Design scenario | Core v0 path |
|---|---|
| Fully conventional mapping | Bare `Map<TSource, TDestination>()` |
| Explicit constructor plus members | Structured `Construct` followed by `Members` |
| Conditional reuse or replacement | Structured `Resolve` or runtime `ResolveUsing` using `Option<T>` |
| Always create a replacement | `Resolve` / `ResolveUsing` that intentionally ignores previous |
| Factory plus members | `ConstructUsing` followed by writable `Members` |
| Runtime factory-only destination plus members | `ResolveUsing` with post-construction writable rules |
| Scalar or opaque value object | `ConstructUsing` / `ResolveUsing` or manual `Convert` |
| Immutable or complex manual mapping | `Convert`, including ordinary record `with` expressions |
| Immutable Update | Explicit `Resolve` replacement, manual `Convert`, or a deliberate no-op reuse |

The table above is an audit of the agreed target surface. Compiled consumer and
executable integration scenarios cover the current pre-revision implementation;
the callback and read-only proxy migration must update that coverage before the
target is considered implemented. The audit's less mature areas are listed
below as explicit non-goals rather than implied fallbacks.

## Intentionally outside v0

- collections, dictionaries, buffers, reconciliation, and clear/fill policies;
- projection and expression-tree lowering;
- convention flattening and `IncludeMembers`;
- patch/merge presence policies;
- automatic immutable Update reconstruction;
- tuple roots, multi-source mapping, and per-call state;
- keyed variants and runtime polymorphism links;
- reference tracking, shared identity, and cycles;
- open-generic or runtime-type lookup;
- cross-assembly configuration composition;
- automatic DI registration, manifests, or assembly scanning;
- hooks, middleware, and result post-processing;
- first-class enum policies and opt-in name normalization;
- automatic reverse mapping;
- async/I/O mapping, runtime dynamic shapes, and private-state bypass.

These boundaries are not silent fallbacks. Morphant does not switch to a
different runtime mapping algorithm when a capability is unavailable. Use
explicit code or `Convert` for a supported synchronous special case, or wait
for the separately designed post-v0 capability.

Compile-time diagnostics remain a separate follow-up plan. In the meantime,
every C#-legal mapping contract has deterministic executable behavior:
unsupported or invalid paths throw a typed Morphant exception instead of
leaving the mapper partial implementation incomplete. Only contracts that
cannot be declared in C# are omitted. See [Observable failures](observable-failures.md).
