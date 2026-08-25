# First-class tuple mapping design

Статус: итоговый дизайн на ревью; реализация не начата.

## Цель

Кортежи должны участвовать в обычном declarative mapping Morphant:

- как source и destination root;
- в `Construct`, `Resolve` и `Members`;
- в Create и Update;
- в tuple-to-tuple, tuple-to-object и object-to-tuple mappings;
- с обычными settings, diagnostics, inheritance и nested mapping rules.

Сам tuple не вводит отдельную matching policy. Morphant по-прежнему
маппит по именам, не начинает nested mapping автоматически и не
придумывает positional fallback.

## Поддерживаемые формы

| Форма | Контракт |
|---|---|
| C# `(T1 Name1, T2 Name2)` | First-class `System.ValueTuple` с tuple names |
| `System.ValueTuple<T...>` | First-class, включая один элемент |
| `System.ValueTuple` | Пустой value tuple без element surface |
| `System.Tuple<T...>` | First-class legacy reference tuple |
| Well-formed tuple длиннее семи элементов | Логически плоский tuple; `Rest` скрыт |
| Concrete custom implementation of `ITuple` | Обычный user type: declared members и constructors участвуют в статическом mapping |
| `ITuple` interface | Обычный interface source/destination; универсальная element convention отсутствует |

First-class flattening применяется только к корректной BCL encoding chain.
Нестандартный `TRest` не разворачивается как tuple tail: такой concrete type
может участвовать только через свою реальную declared surface.

`ITuple` сам по себе не даёт статическую element model: он содержит только
runtime `Length` и `object? this[int]`, но не содержит element names, типы и
construction contract. Morphant не добавляет runtime casts и positional access
только из-за факта реализации этого interface. При этом explicit expression
может обратиться к индексатору и выполнить нужное приведение.

## Логическая element surface

BCL tuple представляется как плоская последовательность logical elements.
Каждый element хранит:

- one-based ordinal;
- static element type с nullability;
- semantic name, если оно задано tuple presentation;
- technical access path к BCL storage;
- mutability и допустимые Create/Update operations.

Для named value tuple semantic name равно alias, например `Id`. Для
unnamed value tuple и `System.Tuple` semantic name отсутствует. `Item1`,
`Item2` и так далее в этом случае являются technical ordinal names, а не
semantic names.

`Rest` никогда не попадает в generated construction/member plans, convention
lookup, diagnostics и unmapped-member validation. Например, element №8 всегда
отображается как `Item8` либо его semantic name.

Если named value tuple в C# допускает явный доступ и через alias, и
через underlying `ItemN`, оба выражения считаются использованием одного
logical element. Destination plan при этом показывает ровно одно имя:
semantic alias, если он есть, иначе `ItemN`.

## Convention matching

Автоматически участвуют только elements с semantic names.

- Tuple elements не сопоставляются по ordinal.
- Technical `ItemN` не является convention name.
- Named element не получает fallback к underlying `ItemN`.
- Разная tuple arity сама по себе не является ошибкой: destination
  elements ищутся по именам, лишние source elements остаются unused.
- Обычные member и constructor name rules Morphant сохраняются. Member
  convention требует exact case-sensitive name; logical constructor parameter
  ищет exact name, затем unique case-insensitive name.
- Все обычные conversion, nullability, flattening и ambiguity checks остаются
  в силе.
- Tuple-typed nested value не запускает nested tuple mapping неявно. Если
  direct warning-free conversion не сохраняет ту же tuple presentation, нужен
  explicit `Map`, `Create` или `Update`.

`Auto()` и `ByConvention()` применяют тот же name-based convention. Они не
превращают technical `ItemN` в имя и не включают positional mapping.
Для unnamed element нужно explicit expression.

```csharp
builder.Map<(int, string), (long, string)>()
    .Members(source => new()
    {
        Item1 = source.Item1,
        Item2 = source.Item2
    });
```

## Примеры matching

| Source | Destination | Автоматический результат |
|---|---|---|
| `(int Id, string Name)` | `(string Name, int Id)` | `Name -> Name`, `Id -> Id` |
| `(int Id, string Name, DateTime CreatedAt)` | `(string Name, int Id)` | Два match; `CreatedAt` unused |
| `(int X, int Y)` | `(int Y, int X)` | Перестановка по именам |
| `(int, string)` | `(int, string)` | Нет element matches |
| `(int X, int Y)` | `(int Left, int Top)` | Нет element matches |
| `Tuple<int, string>` | `(int Id, string Name)` | Нет element matches |

Отсутствие automatic match не запрещает mapping. Пользователь может
явно предоставить значения logical elements через `Construct` или `Members`,
выбрать целый result через `Resolve`/factory либо владеть всем алгоритмом через
`Convert`. Физический способ создания BCL tuple при этом остаётся обязанностью
generator, а не пользователя.

## Declarative API

Для tuple destination генерируются все применимые обычные surfaces.

### `Construct` и `Resolve`

Construction plan показывает один logical constructor с плоским списком
всех elements. Для named element parameter носит semantic name, для
unnamed element — `ItemN`. `Rest` в construction plan отсутствует.
Этот constructor является intrinsic tuple construction contract, а не одним
из CLR constructors, между которыми выбирает `ConstructorSelection`.

```csharp
builder.Map<Source, (int Id, string Name)>()
    .Construct(source => new(
        source.Id,
        source.DisplayName));
```

`ByConvention()` может дополнить named elements и принять explicit overrides:

```csharp
builder.Map<Source, (int Id, string Name)>()
    .Construct(source => new(
        ByConvention(),
        new()
        {
            Name = source.DisplayName
        }));
```

`Resolve` сохраняет обычный lifecycle Morphant: generator видит отдельно
ветку, возвращающую `previous`, и ветку logical construction. Первая reuse
existing destination, вторая создаёт replacement и получает applicable
creation rules. Elements без semantic names не получают convention rules,
поэтому их значения должны прийти из explicit element rules или из
whole-result rule.

### `Members`

Для first-class BCL tuple `Members` описывает logical elements, а не буквально
CLR setters. В generated member plan используется semantic name либо `ItemN`:

```csharp
builder.Map<Source, (int Id, string Name)>()
    .Members(source => new()
    {
        Name = source.DisplayName
    });
```

Unmentioned named elements следуют `MemberSelection`. Unnamed elements не
получают automatic rule, но могут быть явно заняты value, `Ignore`, `Map`,
`Create` или `Update` rule. Unmentioned element и explicitly ignored element —
разные состояния:

- `Ignore` подавляет automatic rule этого slot. Если current path уже имеет
  independently selected seed для element из explicit construction или
  whole-result value, seed сохраняется. На reused Update сохраняется current
  value. На Create без seed `Ignore` явно выбирает `default(TElement)`;
- unmentioned element получает convention только когда это разрешает
  `MemberSelection`. Если ни convention, ни seed нет, element остаётся missing.
  `MemberSelection.Explicit` и `UnmappedMemberValidation.None` не превращают
  missing element в скрытый `default`.

На Create и explicit replacement branch applicable rules для обоих BCL tuple
собираются в один final logical element plan. Когда initial result не требуется
пользовательскому expression, generator-owned construction values сразу
понижаются в canonical construction. На branch, который reuse existing
`ValueTuple`, member rule понижается в assignment к mutable field. Для
`System.Tuple` тот же declarative slot предоставляет значение canonical
constructor, хотя соответствующее CLR property read-only:

```csharp
builder.Map<Source, Tuple<int, string>>()
    .Members(source => new()
    {
        Item1 = source.Id,
        Item2 = source.DisplayName
    });
```

Generator сам создаёт `new Tuple<int, string>(...)` из final element plan.
Это не делает `Item1` и `Item2` writable CLR members.

С точки зрения lifecycle scalar slots `System.Tuple` являются creation-only:
они предоставляют values на Create и на explicit replacement branch, но не
вычисляются для Update branch, который reuse existing destination. Поэтому
scalar element rules сами по себе не вызывают неявную реконструкцию. Для
replacement нужен explicit `Resolve` или `ResolveUsing`. Readable reference
element может участвовать в explicit nested Update, если обычные правила
Morphant допускают nested mapping в публично readable reference member.

### Composition

Tuple не вводит API composition restrictions. `Construct`/`Resolve` и
`ConstructUsing`/`ResolveUsing` можно комбинировать с `Members`. Lowering
подчиняется следующим rules:

1. Convention, `Construct` или declarative construction branch `Resolve`
   задаёт generator-owned initial construction plan. `ConstructUsing` и
   `ResolveUsing` вместо этого возвращают opaque whole-result seed, который
   всегда должен быть вычислен.
2. Construction values, selected seed и `Members` объединяются в final logical
   element plan. Explicit member rule имеет обычный precedence над earlier rule
   того же element. Automatic member convention не дублирует element, который
   уже занят construction rule на текущем lifecycle path.
3. Если ни одно surviving rule не читает `result`, generator-owned Create или
   replacement construction сразу понижается в один canonical construction.
   Overridden generator-owned element expression удаляется и не вычисляется.
4. Если surviving rule читает `result`, initial result сначала материализуется:
   пользователь явно сделал его состояние observable. Затем `ValueTuple`
   получает applicable field assignments, а `System.Tuple` при необходимости
   реконструируется один раз из selected seed и final element values. Initial
   construction expressions в этой форме могут вычисляться, даже если
   corresponding element позднее заменён.
5. Whole-result callback всегда выполняется. Generator не пытается удалить или
   переписать вычисления внутри `ConstructUsing`/`ResolveUsing`; returned value
   становится selected seed для последующей member phase.
6. На branch, который reuse existing `ValueTuple`, applicable `Members` rules
   обновляют mutable fields переданной по значению копии.
7. `System.Tuple` Update без explicit whole-result replacement не получает
   construction path только из-за scalar `Members` rules.
8. `Convert` остаётся final/exclusive алгоритмом.

```csharp
builder.Map<Source, (int Id, string Name)>()
    .Construct(source => new(
        source.Id,
        source.Name))
    .Members(source => new()
    {
        Name = source.DisplayName
    });
```

Поскольку ни одно rule не читает `result`, при Create final plan сразу
понижается в эквивалент одного construction:

```csharp
return (Id: source.Id, Name: source.DisplayName);
```

`source.Name` проиграл по precedence, поэтому не попадает в generated code и
не вычисляется. При Update с reused `ValueTuple` result `Construct` не
выполняется, а `Members` обновляет его mutable fields. Тот же precedence
действует для declarative construction branch `Resolve`.

Если `Members` читает `result`, два observable этапа не склеиваются:

```csharp
builder.Map<Source, Tuple<int, string>>()
    .Construct(source => new(source.Id, source.Name))
    .Members((source, _, result) => new()
    {
        Item2 = result.Item1 + ":" + source.DisplayName
    });
```

Здесь initial `Tuple<int, string>` сначала существует как `result`, после чего
generator создаёт final tuple с сохранённым `Item1` и новым `Item2`. Это
тот случай, когда промежуточная generator-owned tuple construction является
частью контракта, а не упущенной оптимизацией. Runtime factory result также
всегда материализован, потому что его callback является observable.

Для `System.Tuple` branch, который reuse existing destination, сохраняет scalar
elements и не вычисляет их `Members` rules. Без `Resolve`/`ResolveUsing` один
только `Members` не реконструирует `System.Tuple` во время Update.

`IncludeMembers`, flattening, `IncludeBase`, runtime polymorphism и declarative nested
markers применяют обычные contracts и не получают tuple-specific
исключений.

## Create и Update

### `ValueTuple`

Create собирает новое tuple value из complete final construction/member plan,
когда каждый required destination element получил значение. Значения могут
прийти из name-based convention, explicit `Construct`/`Members`, selected
whole-result seed либо explicit `Ignore`. `Ignore` без seed выбирает
`default(TElement)`; просто отсутствующий rule этого не делает. Bare mapping
использует только convention. Отсутствующие semantic names не приводят к
скрытому positional copy или к тихому выбору `default` вместо маппинга.

Когда `result` не нужен, generator-owned plan понижается в один canonical
construction, а overridden expressions не вычисляются. Если rule читает
`result` или initial value пришёл из runtime callback, selected value сначала
материализуется, после чего generator изменяет его by-value copy. Поэтому
observable initial expressions и whole-result callbacks сохраняются.

Явный `Members` означает, что пользователь владеет member plan. В нём
можно явно задать или игнорировать unnamed elements; unmentioned
elements дальше следуют обычному `MemberSelection` и
`UnmappedMemberValidation`.

Update не считает `ValueTuple` immutable. Branch, который reuse existing
destination, применяет applicable assignments к mutable fields и возвращает
его. Parameter `destination` уже является value-type copy, поэтому generator
может записать поля прямо в него и вернуть `destination`. Generator-owned
replacement branch без `result` dependency сразу понижает final element plan в
canonical construction. Отдельный result local нужен только для selected
whole-result value, `result` dependency или control flow, а не является частью
tuple contract.

Unmatched element сохраняет existing value в Update. Explicit `Resolve` может
вернуть целый replacement. Вызывающий код, как и для любого value-type
Update, обязан сохранить returned value.

### `System.Tuple`

Create всегда знает canonical logical constructor `System.Tuple` и сам
понижает в него complete final element plan. Каждый required element должен
получить value или explicit `Ignore`; ignored element без seed получает
`default(TElement)`. У `System.Tuple` elements всегда имеют только technical
`ItemN`, поэтому они не получают automatic convention и задаются явно. Это не
требует от пользователя `Resolve`, factory или `Convert`: whole-result rules
остаются необязательными альтернативами.

Existing `System.Tuple` не пересоздаётся автоматически. Assignable element
rules для него физически невозможны, поэтому default Update возвращает existing
destination. Scalar `Members` rules, использованные для Create, не меняют этот
Update contract. Applicable read-only nested member rule при этом может
обновить сам referenced object.

Declarative `Resolve` уже разделяет lifecycle paths структурно: `return
previous` означает reuse, scalar creation-only rules не вычисляются; logical
construction означает replacement и получает final element plan.

Для `ResolveUsing` это разделение определяется во время выполнения:

- `null` остаётся terminal result, `Members` не выполняется;
- если при Update callback вернул тот же reference, который находился в
  `previous`, это reuse branch. Проверка выполняется через reference identity;
  scalar rules не вычисляются, но applicable nested Update может выполняться;
- другой non-null instance — replacement seed. Applicable scalar rules
  вычисляются, и при наличии изменений generator создаёт final
  `System.Tuple` через canonical constructor, сохраняя остальные elements из
  seed;
- на Create любой non-null callback result является replacement seed.

Replacement seed не обещает сохранить identity callback result, если scalar
plan требует canonical reconstruction. Без таких изменений generator возвращает
selected instance как есть. Если callback identity или дополнительное состояние
runtime subtype должно быть частью результата, пользователь выбирает `Convert`
либо не добавляет scalar `Members`.

Эта identity classification нужна именно для first-class `System.Tuple`.
Обычный declarative `Resolve` уже имеет такую branch semantics для других
immutable destinations. Но arbitrary `ResolveUsing` result нельзя безопасно
пересоздать в общем случае: callback может вернуть cached или derived instance,
а generator не знает clone contract. Поэтому existing diagnostic для
init-only/get-only scalar `Members` после arbitrary `ConstructUsing` или
`ResolveUsing` сохраняется; tuple support его не ослабляет для user types.

### Nullability

Nullable value tuple, nullable element types и nullable `System.Tuple` следуют
существующим `NullSourceHandling`, `NullDestinationHandling` и nullability
validation. Tuple feature не вводит скрытый element-level null policy.

## Одинаковый CLR type и tuple presentation

Tuple names не входят в CLR type identity. Например, оба type surface
ниже представлены `System.ValueTuple<int, int>`:

```csharp
(int X, int Y) -> (int Y, int X)
```

Для Morphant это не identity conversion: plan обязан сопоставить `X` и `Y`
по именам и переставить physical fields. Direct-assignment optimization может
применяться только после доказанной эквивалентности final logical plan,
а не по равенству underlying symbols.

Одинаковые source и destination types в Morphant в целом остаются
допустимыми: mapping может описывать clone, normalization, synchronization,
recursive transformation или polymorphic base pair. Но полностью unnamed
`(int, string) -> (int, string)` не получает tuple-specific identity shortcut:
без explicit rules его elements не маппятся.

## Presentation identity и generated surfaces

Runtime registration и `ITypeMapper<,>` identity по-прежнему определяются CLR
source/destination types. Tuple names не создают новую runtime mapping pair и не
устраняют application-level ambiguity между registrations.

При этом tuple presentation входит в declarative surface и mapping plan. Она
включает recursive layout semantic/unnamed element names отдельно для
source и destination.

Одна physical mapping pair должна иметь одну согласованную tuple
presentation во всех registrations одной compilation. Если два mapper типизируют
ту же physical pair разными tuple-name layouts, generator выдаёт
compile-time diagnostic вместо молчаливого выбора canonical aliases. Это
необходимо, поскольку generated extension signatures не могут отличаться
только tuple names.

Внутри одной pair source и destination могут иметь разные presentations;
именно это делает возможным name-based reorder. Construction/member plan types
генерируются для destination presentation, а не только для underlying
`ValueTuple` original definition.

## Settings и diagnostics

Tuple mapping не добавляет public setting. Existing settings действуют в
обычных границах:

- `MappingMode` решает, доступны ли Create и Update;
- `MemberSelection` управляет unmentioned logical tuple elements на applicable
  lifecycle path; technical `ItemN` всё равно не получает automatic convention;
- `ConstructorSelection` не участвует в first-class BCL tuple mapping:
  canonical tuple construction intrinsic. Explicit pair-level setting получает
  обычную diagnostic о неприменимости, inherited setting не имеет эффекта;
- `UnmappedMemberValidation` проверяет logical elements, а не aliases и
  physical fields по отдельности, но отключение validation не предоставляет
  missing constructor values;
- null settings следуют root nullability.

Diagnostic и generated documentation показывают semantic name. Для unnamed
element используется форма `element #N (ItemN)`. `Rest` или цепочка
physical nesting не показываются.

Нужна tuple-specific compile-time diagnostic для conflicting presentation
одной physical pair. Остальные failures по возможности используют
уже существующие construction, member, conversion, nullability и completeness
diagnostics с tuple-aware target names.

## Generated code

- BCL tuple mapping не использует reflection и runtime `ITuple` indexing.
- Value tuple Create предпочитает читаемый tuple literal, когда он
  доступен для данной form.
- Singleton/empty `ValueTuple` и legacy `System.Tuple` понижаются в
  explicit BCL construction.
- Generator-owned Create/replacement обоих BCL tuple получает constructor
  arguments из final logical element plan. Если `result` не требуется,
  overridden construction expressions не генерируются и промежуточный tuple с
  проигравшими values не создаётся.
- Если surviving rule читает `result`, initial tuple материализуется ровно один
  раз. Затем `ValueTuple` изменяется assignments, а `System.Tuple` при
  необходимости получает одну final canonical reconstruction.
- `ConstructUsing`/`ResolveUsing` callback всегда выполняется. Generator не
  оптимизирует его внутренние вычисления; returned tuple используется как
  selected seed.
- `System.Tuple` не требует explicit factory, если все required values доступны.
- Long tuples понижаются в required nested BCL representation, но
  generated declarative surface и diagnostics остаются плоскими.
- Value tuple Update изменяет текущий selected tuple value и возвращает его.
  Обычно это сам by-value `destination` parameter; отдельный local создаётся
  только при необходимости. Caller-owned value не изменяется по ссылке.
- На fused path только surviving final element expressions вычисляются, каждое
  ровно один раз и в обычном declarative order. Если constructor argument order
  отличается, generator сохраняет observable order через readable locals.
  Result-dependent paths сохраняют observable initial construction; overridden
  initial expressions поэтому могут вычисляться. Whole-result selectors всегда
  сохраняют свой lifecycle и evaluation.
- `ResolveUsing` для `System.Tuple` использует `ReferenceEquals` только когда
  Update действительно имел non-null previous. Value tuples не получают
  искусственной equality-based reuse classification.
- Generated code остаётся C# 9-compatible. Future projection support может
  выбрать другую construction form из-за expression-tree restrictions; это
  не входит в текущую feature.

## Interaction boundaries

- Tuple element соблюдает общее правило: nested mapping начинается
  только explicit `Map`, `Create` или `Update` marker.
- Runtime polymorphism привязан к physical mapping pair. Tuple names не
  образуют runtime branch identity.
- Automatic collection element mapping и projection остаются отдельными
  features.
- Tuple support не вводит positional mode, reverse mapping или runtime shape
  discovery.
- Multi-source mapping не становится first-class feature только из-за
  того, что tuple может выступить обычным source type.

## First implementation scope

В первую реализацию входят:

- все well-formed BCL `ValueTuple` и `System.Tuple` arities;
- recursive tuple-name presentation;
- name-only convention matching без ordinal fallback;
- flat logical construction/member plans;
- `Construct`, `Resolve`, applicable `Members`, `ConstructUsing`,
  `ResolveUsing` и `Convert`;
- normal composition destination methods с `Members`, включая fused и
  result-dependent paths;
- mutable `ValueTuple` Create/Update и immutable-container semantics `System.Tuple`;
- `System.Tuple` runtime callback classification по reference identity;
- explicit `Ignore`, completeness, source/destination validation и diagnostics;
- применимость обычных settings без tuple-specific public setting;
- root, nested, inherited, standalone и application/DI paths;
- deterministic incremental generation и readable C# 9 output.

Не входят runtime positional mapping arbitrary `ITuple`, projection semantics,
collection element mapping и отдельная tuple matching setting.

## Black-box acceptance matrix

1. Named value tuple -> named value tuple: same order, reorder и different arity.
2. Named value tuple <-> class, struct и record through constructor/member
   conventions.
3. Fully unnamed и partially named tuples: no ordinal convention; explicit
   `Construct`, `Resolve` и `Members` work.
4. Named alias и underlying `ItemN` count as one source element for usage
   validation.
5. Same underlying `ValueTuple` с different name layouts does not use direct
   assignment and follows names.
6. Recursive tuple-name layouts, включая nested tuple elements, сохраняются
   отдельно для source и destination.
7. Empty, singleton, 2-, 7-, 8- и long `ValueTuple`; flat public surface without
   `Rest`.
8. 1-, 7-, 8- и long `System.Tuple`; canonical construction from complete
   explicit element plans, missing-value diagnostics, no required factory и no
   public `Rest`.
9. Malformed/non-tuple `Rest` chain не flatten и участвует только через real
   declared surface.
10. `ValueTuple` Create through convention, `Construct`, `Resolve`, `Members`
    and runtime factories.
11. `ValueTuple` Update mutates the by-value destination/current selected result,
    unmatched preservation, explicit overrides, nested markers и replacement;
    no mandatory redundant result copy.
12. `Construct`/declarative `Resolve` combined with `Members`: overlap is fused,
    discarded expressions are not evaluated, surviving rules keep declarative
    order, previous branch reuses and construction branch receives creation-only
    rules.
13. A surviving `result` dependency materializes initial construction before the
    member phase; ValueTuple assignment and System.Tuple reconstruction preserve
    observable values, side effects and control flow.
14. Discarded rules do not count as source usage and do not retain generated
    mapping dependencies or downstream plan diagnostics; expressions required
    by an observable initial result do.
15. `Ignore` with no Create seed chooses element `default`; with selected seed or
    reused Update it preserves that value. Unmentioned/missing element never gets
    implicit `default`, including with `MemberSelection.Explicit` or
    `UnmappedMemberValidation.None`.
16. `System.Tuple` default Update is a no-op for scalar slots; scalar `Members`
    alone do not reconstruct it; applicable read-only nested Update still runs.
17. `System.Tuple` declarative `Resolve` covers reuse and replacement branches;
    only replacement evaluates scalar creation rules.
18. `ResolveUsing` for `System.Tuple` covers terminal null, same-reference reuse
    and different-reference replacement. Reference identity controls scalar
    rule evaluation and reconstruction; a replacement without scalar changes is
    returned unchanged, and nested Update remains applicable on reuse.
19. `ConstructUsing`/`ResolveUsing` whole-result callbacks execute exactly once
    and their internal values are never optimized away.
20. Update with unavailable/null destination and `NullDestinationHandling.Create`
    uses a complete tuple construction path while preserving operation context.
21. Nullable root tuples, nullable elements, null source/destination policies and
    nullability diagnostics.
22. `MemberSelection`, `MappingMode` and every `UnmappedMemberValidation` mode;
    explicit pair-level `ConstructorSelection` is inapplicable and inherited
    `ConstructorSelection` has no effect.
23. `IncludeMembers`, flattening, explicit nested mappings, inheritance and runtime
    polymorphism boundaries.
24. Concrete custom `ITuple` maps through declared static surface; `ITuple`
    interface indexing remains explicit/manual.
25. Conflicting tuple presentations for one physical pair produce one stable,
    actionable compile-time diagnostic; presentations on different physical
    pairs remain independent.
26. Mapper contracts, DI ambiguity and standalone lookup continue to use physical
    CLR pair identity.
27. Generated snapshots preserve aliases, evaluation order, readable locals,
    reference checks, C# 9 syntax and deterministic CRLF output.
28. Adding, removing or renaming a tuple element invalidates only affected
    incremental outputs and diagnostics.

## Documentation completion

При реализации нужно:

- убрать tuples из opaque/manual-only ограничений;
- обновить `conventions.md`, `create-and-update.md`, API pages и
  `limitations.md`;
- добавить user-facing tuple guide с named, unnamed, legacy и Update
  examples;
- добавить страницы для новых diagnostics и обновить diagnostics
  index;
- обновить generated-code snapshots.
