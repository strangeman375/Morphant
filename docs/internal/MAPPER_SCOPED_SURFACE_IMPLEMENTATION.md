# Mapper-scoped surface implementation

Status: approved contract; implementation in progress.

This document is a temporary implementation checklist. Remove it after the
observable contract has been transferred to the public documentation and the
migration is complete.

## Runtime API

- A mapper derives from `TypeMapper<TMapper>` with the concrete mapper as its
  self type.
- There is one `Configure(MapperBuilder builder)` override. `MapperBuilder` is
  the protected nested root builder inherited from `TypeMapper<TMapper>`.
- `Map<TSource, TDestination>(MappingMode)` returns
  `MappingBuilder<TMapper, TSource, TDestination>`.
- Shipped configuration operations are instance methods. Generated
  construction and member surfaces are extension methods.
- `MappingMode` is available only on the root builder. A pair override is
  expressed only by the optional `Map` argument.
- Other common settings preserve the current fluent type through
  `MapperBuilderBase<TBuilder>`.
- `IncludeMembers`, `IncludeBase`, and `ForDerived` are instance methods on
  `MappingBuilder<TMapper, TSource, TDestination>`.
- The old non-generic `TypeMapper` and two-argument `MapperBuilder` are removed
  without a compatibility layer.
- `IMapperDeclaration` is hidden runtime metadata used for exact-pair lookup;
  it is not a configuration entry point.

## Stable generated-surface rule

The rule is evaluated from the declared `Map` pair before inheritance or
generic substitution. It never depends on other mappers in the compilation.

| Declared pair | Surface |
|---|---|
| Fully closed and contains no `ValueTuple` | Shared |
| Fully closed and contains `ValueTuple` at any depth | Mapper-scoped |
| Contains a type parameter | Mapper-family-scoped |

An open pair remains mapper-family-scoped after a leaf mapper closes its type
parameters. `System.Tuple` does not itself require mapper scope, but a nested
`ValueTuple` or type parameter does. An unnamed `ValueTuple` still requires
mapper scope because another declaration can give the same CLR type element
names.

Shared extensions are generic directly over `TMapper` and use
`MappingBuilder<TMapper, TSource, TDestination>` as their receiver. There is no
`IMappingBuilder<,>` marker. Mapper-scoped extensions name an accessible mapper
type. Mapper-family-scoped extensions constrain `TMapper` to the declaring
CRTP family.

Overlapping base and derived mapper scopes produce one most-general applicable
surface. Unrelated mapper families remain independent. Adding or removing an
unrelated mapper must not change an existing surface or hint name.

## Tuple presentation

Presentation contains recursive element names, nullable annotations, and
`dynamic` distinctions on both sides of a physical pair. Extension scope and
tuple-plan sharing are separate decisions:

- different mapper scopes may use different presentations of one CLR pair;
- one effective mapper may use only one presentation of that pair;
- identical plan presentations may share `TupleConstructorParameters`,
  `TupleConstruction`, and `TupleMembers` even when extensions are scoped;
- runtime registration and `ITypeMapper<,>` identity remain the physical CLR
  source and destination types.

Within one effective mapper, a differing presentation reports `MORPH0056` and
an identical duplicate reports `MORPH0013`; one registration must not receive
both diagnostics.

## Mapper declarations

- Valid direct form: `Mapper : TypeMapper<Mapper>`.
- Valid reusable form:
  `CommonMapper<TMapper> : TypeMapper<TMapper>` with
  `where TMapper : CommonMapper<TMapper>`.
- A mismatched self type reports `MORPH0058` at the incorrect type argument.
- Mapper types and their containing types must be accessible to generated
  namespace-level code in the same assembly, normally `public` or `internal`.
  Private/protected-only mapper declarations are outside the support boundary;
  no generated scope markers are added for them.

## Implementation checkpoints

1. Introduce the runtime API and persist this contract.
2. Migrate declaration discovery, runtime contract validation, builder-flow
   analysis, and mapper hierarchy modelling.
3. Generate shared, mapper-scoped, and mapper-family-scoped extensions by the
   stable rule above.
4. Scope tuple-presentation coordination and `MORPH0056` to one effective
   mapper.
5. Migrate all settings, `IncludeBase`, `IncludeMembers`, polymorphism,
   callbacks, mapper emission, and standalone dispatch.
6. Add full generated-source snapshots, compiler diagnostics, and runtime
   scenarios for ordinary, tuple, generic, inherited, and nested declarations.
7. Cover actualization and incrementality with one reused generator driver,
   including edits, full syntax-tree replacement, broken-state recovery, and
   unrelated cached outputs.
8. Update public documentation and API inventory, remove obsolete prototype
   material, run final verification, and delete the prototype branch.

Every completed checkpoint is committed and published to remote `main` before
the next checkpoint begins, even when the solution is temporarily not
buildable.
