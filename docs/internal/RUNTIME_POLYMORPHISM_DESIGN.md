# Runtime polymorphism design

Статус: согласованный дизайн, реализация начата 2026-08-21.

## Public API

Runtime dispatch задаётся явно и принадлежит exact base pair:

```csharp
builder.Map<Animal, AnimalDto>()
    .ForDerived<Dog, DogDto>()
    .ForDerived<Cat, CatDto>();

builder.Map<Dog, DogDto>()
    .IncludeBase<Animal, AnimalDto>(); // optional rule reuse only
```

```csharp
public MapperBuilder<TSource, TDestination>
    ForDerived<TDerivedSource, TDerivedDestination>()
    where TDerivedSource : TSource
    where TDerivedDestination : TDestination;
```

`ForDerived` добавляет link на отдельно зарегистрированную mapping pair. Он не
регистрирует derived pair, не наследует её rules и не сканирует mappings.
`IncludeBase` независимо переиспользует configuration rules и не включает
runtime dispatch.

Ограничения `class` нет: допустимы class-, interface- и value-type branches,
если выполняется обычная CLR assignability.

## Unknown derived source

Настройка использует обычную precedence Morphant
assembly -> mapper -> pair:

```csharp
public enum UnknownDerivedTypeHandling
{
    Default = 0,
    UseBaseMapping,
    Throw
}
```

```csharp
builder.Map<Animal, AnimalDto>()
    .ForDerived<Dog, DogDto>()
    .UnknownDerivedTypeHandling(UnknownDerivedTypeHandling.Throw);
```

MSBuild property: `MorphantUnknownDerivedTypeHandling`.

`Default` разрешается в `UseBaseMapping`. Поэтому добавление одной
специализации не превращает открытую hierarchy в closed union. `Throw`
выбрасывает `UnmatchedPolymorphicMappingException` только для derived runtime
source без matching link. Exact runtime `TSource` всегда использует base plan.

## Dispatch algorithm

Для `Map<TBaseSource, TBaseDestination>`:

1. Application `IMapper` разрешает ровно base pair по существующему закону
   `0 / 1 / 2+` registrations.
2. Effective `MappingMode` base pair обязан разрешать запрошенную operation.
3. `null` source продолжает по null policy base pair.
4. Exact runtime `TBaseSource` выполняет base mapping.
5. Иначе рассматриваются только explicit links этой exact base pair.
6. Link подходит, если runtime source является instance candidate source.
7. Выбирается единственный наиболее конкретный candidate по assignability.
8. Без candidate применяется `UnknownDerivedTypeHandling`.
9. Несколько несравнимых максимальных candidates приводят к
   `AmbiguousPolymorphicMappingException`; declaration order не является
   приоритетом.
10. Выбранная derived pair вызывается через тот же scoped `context.Mapper` с
    той же operation и обычным exact-pair законом `0 / 1 / 2+`.
11. Missing, ambiguous или disabled matched derived pair не откатывается к
    base mapping.

Destination не участвует в выборе source branch. Если base pair имеет только
`Dog` link, `ProxyDog` считается match. Если `Dog` pair сама содержит
`ServiceDog` link, её dispatcher может продолжить транзитивный dispatch.

Potential overlap несравнимых interfaces легален на compile time. Runtime
объект, matching несколько максимальных interface branches, получает точную
ambiguity ошибку с requested base pair, actual source type и maximal branches.

## Strict Update contract

После выбора derived branch:

| Existing destination | Поведение |
|---|---|
| Runtime-compatible | Вызвать derived Update и передать destination |
| `null`, derived destination допускает `null` | Вызвать derived Update с `null` |
| `null`, derived destination — non-nullable value type | `PolymorphicDestinationTypeMismatchException` |
| Non-null incompatible | `PolymorphicDestinationTypeMismatchException` |

Dispatcher не отбрасывает несовместимый destination, не вызывает derived
Create вместо Update и не откатывается к base Update. Derived Update может
вернуть replacement в рамках обычного контракта Morphant.

Compatible boxed value destination unbox-ится для derived Update, а result
возвращается boxed как base interface/object. Nullable value destination может
представить отсутствие; non-nullable value destination — нет.

## Interaction with existing features

- Matching branch выполняет только собственный plan. Base `Construct`,
  `Resolve`, `Members`, `Convert`, `IncludeMembers` и flattening не оборачивают
  и не дополняют derived plan.
- Base `Convert` обрабатывает exact base и unmatched source при
  `UseBaseMapping`.
- `null` source всегда обрабатывается base pair. После выбора non-null branch
  действуют null policies derived pair.
- Explicit nested `Map`/`Create`/`Update` определяет requested exact pair; её
  links применяются без отдельного nested API.
- `TypeMapper.Supports` остаётся exact. Link не является registration.
- Standalone и application/DI paths соблюдают один контракт. В standalone
  matched derived pair должна быть доступна concrete mapper так же, как
  обычная nested pair.
- Projection не получает неявной runtime semantics. Будущая capability должна
  либо выразить dispatch в expression tree, либо сообщить compile-time error.
- Будущий collection mapper вызывает element base pair и сам определяет
  collection replacement semantics.

## Failures and diagnostics

Новые runtime exceptions:

- `AmbiguousPolymorphicMappingException` — несколько несравнимых наиболее
  конкретных branches;
- `UnmatchedPolymorphicMappingException` — unknown derived source при `Throw`;
- `PolymorphicDestinationTypeMismatchException` — Update destination нельзя
  передать выбранной branch.

Existing exceptions сохраняют точный смысл: missing/ambiguous/disabled
derived pair сообщает соответственно `MappingNotFoundException`,
`AmbiguousMappingException` или `MappingOperationNotSupportedException` именно
для derived pair.

Compile-time diagnostics требуются для self-link, duplicate effective link с
тем же derived source, incompatible/inaccessible types, invalid fluent flow,
unknown enum value или non-constant setting argument и недостижимого base plan,
когда fallback возможен по effective policy. Nullable annotations не образуют
отдельную runtime pair identity.

## First implementation scope

В scope первой реализации входят:

- `ForDerived` и separately registered pairs;
- generated most-specific Create/Update dispatch без reflection;
- `UnknownDerivedTypeHandling` с default `UseBaseMapping` и `Throw`;
- строгий Update;
- class/interface/value-type branches;
- root, nested, standalone и application/DI paths;
- settings precedence, MSBuild property, diagnostics, nullable и incremental
  behavior;
- взаимодействие со всеми существующими mapping rules.

Не входят scanning/`IncludeAllDerived`, derived-side auto-registration,
registration-order priorities, discriminator callbacks, automatic destination
replacement, projection semantics, special collection API и keyed variants.

## Black-box acceptance matrix

1. Exact base, exact derived, unknown fallback и `Throw`.
2. Concrete, abstract и interface base sources.
3. Most-specific class chain, proxy match и транзитивный dispatch.
4. Unrelated interfaces, interface inheritance и runtime diamond ambiguity.
5. `null` source со всеми base null policies и base `Convert`.
6. Create/Update modes base и derived pair независимо.
7. Compatible, null и incompatible Update destinations.
8. Derived Update, возвращающий replacement.
9. Struct source/destination, boxing, nullable value и null/non-nullable mismatch.
10. Same mapper, different mappers, standalone и application paths.
11. `0 / 1 / 2+` registrations сначала base, затем matched derived pair.
12. Полная независимость `IncludeBase` и `ForDerived`.
13. `Convert`, `Members`, `IncludeMembers`, flattening и explicit nested mapping.
14. Assembly/mapper/pair precedence, `Default`, invalid values и MSBuild property.
15. Duplicate/self/inaccessible/invalid-flow diagnostics.
16. Nullable warnings, incremental invalidation, public API baseline и exception
    properties/messages.
