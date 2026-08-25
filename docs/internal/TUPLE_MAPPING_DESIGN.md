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

`Resolve` сохраняет обычный lifecycle: он может вернуть `previous` либо
целый replacement tuple. Elements без semantic names не получают convention
rules, поэтому их значения должны прийти из explicit element rules или из
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
получают automatic rule, но могут быть явно заняты value, `Ignore`,
`Map`, `Create` или `Update` rule.

На Create и explicit replacement branch applicable value rules для обоих BCL
tuple сначала объединяются в final element plan, а затем понижаются прямо в
canonical construction. На branch, который reuse existing `ValueTuple`,
member rule понижается в assignment к mutable field. Для `System.Tuple` тот же
declarative slot предоставляет значение constructor, хотя соответствующее CLR
property read-only:

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

Tuple не вводит composition restrictions. Действуют общие rules Morphant,
пониженные через final logical element plan:

1. `Construct`, `Resolve`, `ConstructUsing` или `ResolveUsing` задаёт initial
   construction/result source текущего lifecycle branch.
2. Construction/convention values и `Members` объединяются в final element
   plan. Explicit member rule имеет обычный precedence над earlier rule того же
   element.
3. На Create/replacement branch final plan обоих BCL tuple понижается сразу в
   один canonical construction. Overridden element expression удаляется из
   generated plan и не вычисляется.
4. На branch, который reuse existing `ValueTuple`, applicable `Members` rules
   обновляют mutable fields переданной по значению копии.
5. Automatic member convention не дублирует corresponding element,
   который уже занят construction rule на текущем lifecycle path.
6. `System.Tuple` Update без explicit whole-result replacement не получает
   construction path только из-за scalar `Members` rules.
7. Whole-result `Resolve`/factory остаётся lifecycle operation и вычисляется,
   даже если subsequent `Members` rules задают все tuple elements.
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

При Create final plan сразу понижается в эквивалент одного construction:

```csharp
return (Id: source.Id, Name: source.DisplayName);
```

`source.Name` проиграл по precedence, поэтому не попадает в generated code и
не вычисляется. При Update с reused `ValueTuple` result `Construct` не
выполняется, а `Members` обновляет его mutable fields. Тот же precedence
действует для `Resolve`.

Для `System.Tuple` действует то же lowering final plan в canonical constructor.
Branch, который reuse existing destination, сохраняет scalar elements и не
вычисляет их `Members` rules. Без `Resolve` один только `Members` не
реконструирует `System.Tuple` во время Update.

`IncludeMembers`, flattening, `IncludeBase`, runtime polymorphism и declarative nested
markers применяют обычные contracts и не получают tuple-specific
исключений.

## Create и Update

### `ValueTuple`

Create собирает новое tuple value одним canonical construction из final
construction/member plan, когда каждый required destination element получил
значение. Значения могут прийти из name-based convention либо explicit
`Construct`/`Members` rules. Rules, overridden при merge final plan, не
вычисляются. Bare mapping использует только convention. Отсутствующие semantic
names не приводят к скрытому positional copy или к тихому выбору `default`
вместо маппинга.

Явный `Members` означает, что пользователь владеет member plan. В нём
можно явно задать или игнорировать unnamed elements; unmentioned
elements дальше следуют обычному `MemberSelection` и
`UnmappedMemberValidation`.

Update не считает `ValueTuple` immutable. Branch, который reuse existing
destination, применяет applicable assignments к mutable fields и возвращает
его. Parameter `destination` уже является value-type copy, поэтому generator
может записать поля прямо в него и вернуть `destination`. Create/replacement
branch вместо промежуточной construction и последующих writes сразу понижает
final element plan в canonical construction. Отдельный result local нужен
только если этого требует result selection или control flow, а не является
частью tuple contract.

Unmatched element сохраняет existing value в Update. Explicit `Resolve` может
вернуть целый replacement. Вызывающий код, как и для любого value-type
Update, обязан сохранить returned value.

### `System.Tuple`

Create всегда знает canonical logical constructor `System.Tuple` и сам
понижает в него final element plan. Каждый required element должен получить
значение из convention или explicit `Construct`/`Members` rule. У
`System.Tuple` elements всегда имеют только technical `ItemN`, поэтому они не
получают automatic convention и задаются явно. Это не требует от
пользователя `Resolve`, factory или `Convert`: whole-result rules остаются
необязательными альтернативами.

Existing `System.Tuple` не пересоздаётся автоматически. Assignable element
rules для него физически невозможны, поэтому default Update возвращает existing
destination. Scalar `Members` rules, использованные для Create, не меняют этот
Update contract. Explicit `Resolve` или `ResolveUsing` может вернуть
replacement; applicable read-only nested member rule может обновить сам
referenced object.

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
- `ConstructorSelection` видит один flattened logical element constructor у
  non-empty tuple; empty `ValueTuple` использует parameterless construction;
- `UnmappedMemberValidation` проверяет logical elements, а не aliases и
  physical fields по отдельности;
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
- Create/replacement обоих BCL tuple получает constructor arguments из final
  logical element plan. Overridden element expressions не генерируются;
  промежуточный tuple с проигравшими values не создаётся.
- `System.Tuple` не требует explicit factory, если все required values доступны.
- Long tuples понижаются в required nested BCL representation, но
  generated declarative surface и diagnostics остаются плоскими.
- Value tuple Update изменяет текущий selected tuple value и возвращает его.
  Обычно это сам by-value `destination` parameter; отдельный local создаётся
  только при необходимости. Caller-owned value не изменяется по ссылке.
- Только surviving final element expressions вычисляются, каждое ровно один
  раз и в обычном declarative order. Если constructor argument order отличается,
  generator сохраняет observable order через readable locals. Overridden
  element expressions не вычисляются. Whole-result selectors сохраняют свой
  lifecycle и evaluation.
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
- normal composition `Construct`/`Resolve` с `Members`;
- mutable `ValueTuple` Create/Update и immutable-container semantics `System.Tuple`;
- tuple-aware settings, completeness, source/destination validation и diagnostics;
- root, nested, inherited, standalone и application/DI paths;
- deterministic incremental generation и readable C# 9 output.

Не входят runtime positional mapping arbitrary `ITuple`, projection semantics,
collection element mapping и отдельная tuple matching setting.

## Black-box acceptance matrix

1. Named value tuple -> named value tuple: same order, reorder и different arity.
2. Named value tuple <-> class, struct и record through constructor/member conventions.
3. Fully unnamed и partially named tuples: no ordinal convention; explicit
   `Construct`, `Resolve` и `Members` work.
4. Named alias и underlying `ItemN` count as one source element for usage
   validation.
5. Same underlying `ValueTuple` с different name layouts does not use direct
   assignment and follows names.
6. Empty, singleton, 2-, 7-, 8- и long `ValueTuple`; flat public surface without
   `Rest`.
7. 1-, 7-, 8- и long `System.Tuple`; canonical construction from complete
   convention/explicit `Construct`/`Members` element plans, missing-value
   diagnostics, no required factory и no public `Rest`.
8. `ValueTuple` Create through convention, `Construct`, `Resolve`, `Members` и
   runtime factories.
9. `ValueTuple` Update mutates the by-value destination/current selected result,
   unmatched preservation, explicit overrides, `Ignore`, nested markers и
   replacement; no mandatory redundant result copy.
10. `Construct`/`Resolve` combined with `Members`, including constructor/member
    overlap, non-evaluation of overridden element expressions, surviving-rule
    evaluation order and previous/new `Resolve` branches.
11. `System.Tuple` no-op Update, no implicit reconstruction from scalar
    `Members`, explicit replacement и applicable read-only nested Update.
12. Nullable root tuples, nullable elements, null source/destination policies and
    nullability diagnostics.
13. `MemberSelection`, `ConstructorSelection`, `MappingMode` and every
    `UnmappedMemberValidation` mode.
14. `IncludeMembers`, flattening, explicit nested mappings, inheritance and runtime
    polymorphism boundaries.
15. Concrete custom `ITuple` mapped through declared static surface; `ITuple`
    interface indexing remains explicit/manual.
16. Conflicting tuple presentations for one physical pair produce one stable,
    actionable compile-time diagnostic.
17. Mapper contracts, DI ambiguity and standalone lookup continue to use physical
    CLR pair identity.
18. Generated snapshots preserve aliases, evaluation order, readable locals,
    C# 9 syntax and deterministic CRLF output.
19. Adding, removing or renaming a tuple element invalidates only affected
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
