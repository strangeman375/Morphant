# First-class tuple mapping design

Статус: реализовано; contract закреплён analyzer-backed integration,
generated-source, diagnostics и incrementality tests.

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
  independently selected value для element из explicit construction,
  factory result или existing destination, это value сохраняется. На
  generator-owned Create без такого value `Ignore` явно выбирает
  `default(TElement)`;
- unmentioned element получает convention только когда это разрешает
  `MemberSelection`. Если ни convention, ни existing value нет, element
  остаётся missing. `MemberSelection.Explicit` и
  `UnmappedMemberValidation.None` не превращают missing element в скрытый
  `default`.

На generator-owned Create и declarative replacement branch applicable rules
для обоих BCL tuple собираются в один final logical element plan. Когда initial
result не требуется пользовательскому expression, construction values сразу
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
они предоставляют values на generator-owned Create и на construction branch
declarative `Resolve`, но не вычисляются для Update branch, который reuse
existing destination. Поэтому scalar element rules сами по себе не вызывают
неявную реконструкцию.

`ConstructUsing` и `ResolveUsing` возвращают уже созданный authoritative result.
Generator не заменяет его новым tuple. Value-producing scalar rule для
`System.Tuple` после такого callback неприменим и получает existing diagnostic
`MORPH0042`. `Ignore` разрешён и сохраняет returned value. Explicit nested
`Update` readable reference element также разрешён, если обычные правила
Morphant допускают его для публично readable reference member.

### Composition

Tuple не вводит API composition restrictions. `Construct`/`Resolve` и
`ConstructUsing`/`ResolveUsing` можно комбинировать с `Members`. Lowering
подчиняется следующим rules:

Чтение `result` в `Members` подчиняется обычному lifecycle Morphant и не
вводит tuple-specific правила валидности. Selected result должен сначала
существовать, после чего выполняется member phase. Если какой-то
lifecycle path не может предоставить такой result, это обычная
construction/lifecycle error. Отдельной tuple completeness diagnostic
для этого случая нет.

1. Convention, `Construct` или declarative construction branch `Resolve`
   задаёт generator-owned initial construction plan.
2. Construction values и `Members` объединяются в final logical element plan.
   Explicit member rule имеет обычный precedence над earlier rule того же
   element. Automatic member convention не дублирует element, который уже занят
   construction rule на текущем lifecycle path.
3. Если ни одно surviving rule не читает `result`, generator-owned Create или
   replacement construction сразу понижается в один canonical construction.
   Overridden generator-owned element expression удаляется и не вычисляется.
4. Если surviving rule читает `result`, initial result сначала материализуется:
   пользователь явно сделал его состояние observable. Затем `ValueTuple`
   получает applicable field assignments, а `System.Tuple` при необходимости
   реконструируется один раз из generator-owned initial result и final element
   values. Initial construction expressions в этой форме могут вычисляться,
   даже если corresponding element позднее заменён.
5. `ConstructUsing`/`ResolveUsing` callback всегда выполняется, а его non-null
   result остаётся выбранным result. Generator не переписывает callback и не
   создаёт вместо returned instance другой tuple.
6. После factory result применяются только physically applicable post-result
   rules: assignments к mutable `ValueTuple` fields и explicit nested `Update`.
   Creation-only scalar rule получает `MORPH0042`; `Ignore` сохраняет returned
   element value.
7. На branch, который reuse existing `ValueTuple`, applicable `Members` rules
   обновляют mutable fields переданной по значению копии.
8. `System.Tuple` Update без generator-owned replacement не получает
   construction path только из-за scalar `Members` rules.
9. `Convert` остаётся final/exclusive алгоритмом.

| Result path | Creation-only scalar rule | Writable rule | Explicit nested `Update` |
|---|---|---|---|
| Generator-owned `Construct` / construction branch `Resolve` | Понижается в construction | Применяется | Разрешён |
| `previous` branch `Resolve` | Не вычисляется; current value сохраняется | Применяется | Разрешён |
| Non-null `ConstructUsing` / `ResolveUsing` result | `MORPH0042`; returned value сохраняется | Применяется к returned result | Разрешён |

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
частью контракта, а не упущенной оптимизацией. Factory result в такую fusion не
входит: callback возвращает уже созданный authoritative result.

Для `System.Tuple` branch, который reuse existing destination, сохраняет scalar
elements и не вычисляет их `Members` rules. Только generator-owned construction
branch declarative `Resolve` может создать replacement с новыми scalar values.
Ни один factory result не реконструируется.

`IncludeMembers`, flattening, `IncludeBase`, runtime polymorphism и declarative nested
markers применяют обычные contracts и не получают tuple-specific
исключений.

## Create и Update

### `ValueTuple`

Generator-owned Create собирает новое tuple value из complete final
construction/member plan, когда каждый required destination element получил
значение. Значения могут прийти из name-based convention, explicit
`Construct`/`Members` либо explicit `Ignore`. `Ignore` без existing value
выбирает `default(TElement)`; просто отсутствующий rule этого не делает. Bare
mapping использует только convention. Отсутствующие semantic names не приводят
к скрытому positional copy или к тихому выбору `default` вместо маппинга.

`ConstructUsing`/`ResolveUsing` вместо element plan предоставляет готовый tuple
value. Его mutable fields могут получить обычные post-result assignments из
`Members`; callback при этом не заменяется и не выполняется повторно.

Когда `result` не нужен, generator-owned plan понижается в один canonical
construction, а overridden expressions не вычисляются. Если rule читает
`result` или selected value пришёл из runtime callback, value сначала
материализуется, после чего generator изменяет его mutable fields. Поэтому
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

Generator-owned Create всегда знает canonical logical constructor
`System.Tuple` и сам понижает в него complete final element plan. Каждый
required element должен получить value или explicit `Ignore`; ignored element
без existing value получает `default(TElement)`. У `System.Tuple` elements
всегда имеют только technical `ItemN`, поэтому они не получают automatic
convention и задаются явно. Это не требует от пользователя factory или
`Convert`: whole-result rules остаются необязательными альтернативами.

`ConstructUsing`/`ResolveUsing` вместо element plan возвращает уже созданный
whole result. Completeness его constructor values является ответственностью
callback, а returned instance остаётся authoritative mapping result.

Existing `System.Tuple` не пересоздаётся автоматически. Assignable element
rules для него физически невозможны, поэтому default Update возвращает existing
destination. Scalar `Members` rules, использованные для Create, не меняют этот
Update contract. Applicable read-only nested member rule при этом может
обновить сам referenced object.

Declarative `Resolve` уже разделяет lifecycle paths структурно: `return
previous` означает reuse, scalar creation-only rules не вычисляются; logical
construction означает replacement и получает final element plan.

Для `ConstructUsing`/`ResolveUsing` действует тот же contract, что для других
destination types:

- `null` result остаётся terminal, `Members` не выполняется;
- любой non-null result возвращается как тот же instance; identity с `previous`
  не классифицируется и не меняет lifecycle;
- value-producing scalar `Members` rule для `ItemN` получает `MORPH0042`, потому
  что read-only element нельзя установить после возврата callback;
- `Ignore` сохраняет callback value;
- applicable explicit nested `Update` readable reference element выполняется и
  не требует реконструкции внешнего tuple.

Новая diagnostic для этого не нужна: `MORPH0042` уже описывает member rule,
который нельзя применить после `ConstructUsing`/`ResolveUsing`. Tuple-aware
message указывает logical element, например `element #2 (Item2)`, и affected
operations. Это error, а не молчаливый пропуск rule.

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
presentation во всех registrations одной compilation. Сравниваются
рекурсивные layouts обеих сторон pair:

- если source и destination presentations совпадают, registrations без
  diagnostic используют один generated DSL surface;
- если presentation различается хотя бы на одной стороне, generator
  выдаёт `MORPH0056` вместо молчаливого выбора canonical aliases.

Destination presentation определяет construction/member plan, а source
presentation — contextual type и доступные имена в callback. Поэтому
конфликт source aliases так же неразрешим для текущего pair-specific DSL,
как конфликт destination aliases: generated extension signatures не могут
отличаться только tuple names.

Внутри одной pair source и destination могут иметь разные presentations;
именно это делает возможным name-based reorder. Construction/member plan types
генерируются для destination presentation, а не только для underlying
`ValueTuple` original definition.

### Naming declarative surface

Tuple plan types находятся под `Morphant.Generated.Tuples`. Namespace
кодирует tuple kind, logical arity и recursive type-argument contract:

```text
Morphant.Generated.Tuples.ValueTuple2_Int32_String
Morphant.Generated.Tuples.SystemTuple2_Int32_String
```

Для `ValueTuple` имена элементов текущего уровня входят в user-facing type
name:

```text
Tuple_Key_Item2_ConstructorParameters
Tuple_Key_Item2_Construction
Tuple_Key_Item2_Members
```

Для `System.Tuple`, у которого нет semantic aliases, используются fixed names:

```text
TupleConstructorParameters
TupleConstruction
TupleMembers
```

Presentation вложенного `ValueTuple` является частью contract внешнего type
argument и поэтому рекурсивно входит в namespace. Например,
`Tuple<(int X, int Y)>` получает namespace, оканчивающийся на
`SystemTuple1_ValueTuple2_Int32_Int32_Tuple_X_Y`, и по-прежнему использует
`TupleMembers`. Это позволяет нескольким independently valid physical pairs
иметь разные nested presentations без hash в template type name.

C# predefined scalar types используют короткие CLR names (`Int32`, `String`,
`Object` и остальные keyword-backed scalars). `dynamic` использует
`Dynamic`. Все прочие named type arguments получают префикс `Type_` и полное
qualified type name; правило не зависит от Roslyn `SpecialType` или assembly,
в котором объявлен тип. Поэтому `DateTime` получает `Type_System_DateTime`,
`IEnumerable<string>` —
`Type_System_Collections_Generic_IEnumerable1_String`, а global user type
`Int32` — `Type_Int32` и не совпадает с CLR `int`.

Nullable contracts, generic arguments и array shape также входят в type
contract.
Long tuple namespace использует logical arity и flat elements, без public
`Rest` representation.

Обычные readable names не сокращаются. Если namespace contract или plan type
identifier длиннее 480 UTF-16 code units, generator сохраняет readable prefix,
добавляет stable 64-bit hash полного имени и оставляет semantic plan suffix
(`ConstructorParameters`, `Construction` или `Members`) в конце. Два
ограниченных identifier вместе с `Morphant.Generated.Tuples` дают fully
qualified type name короче C#-лимита в 1024 символа.

Hint names повторяют readable tuple contract, пока полное имя файла занимает
не более 220 UTF-8 bytes. При переполнении identity сокращается на границе
Unicode scalar и получает stable hash полного несокращённого hint name. Запас
до распространённого 255-byte filesystem component limit оставлен для
инструментов, которые могут дополнять filename. Case-insensitive collision
после sanitization по-прежнему получает собственный stable suffix.

Эта схема разделяет plan declarations, но не extension methods: их receiver
по-прежнему определяется physical pair. Поэтому она не отменяет `MORPH0056`.
Mapper-scoped receiver остаётся отдельным отложенным API.

### Отложенный mapper-scoped escape hatch

Первая реализация не добавляет новый public API ради редкого
presentation conflict. Если real-world usage покажет, что `MORPH0056`
мешает полезным scenarios, preferred escape hatch — compile-time mapper scope,
а не overload-resolution tricks:

```csharp
builder.Map<(int X, int Y), (int Left, int Top)>()
    .ForMapper(this)
    .Members(source => new()
    {
        Left = source.X,
        Top = source.Y
    });
```

`ForMapper` — рабочее имя, а не утверждённый API. Mapper type,
выведенный из `this`, войдёт только в compile-time builder identity,
например `MapperBuilder<TMapper, TSource, TDestination>`. Это даст
каждому mapper свой exact tuple presentation и generated DSL surface.
Runtime pair identity, `ITypeMapper<,>`, DI lookup и mapping behavior не
изменятся.

Такой scope понадобится только conflicting registrations. Обычный
`Map<TSource, TDestination>()` и общий surface останутся default API.
После добавления scope `MORPH0056` должен означать только
conflicting *unscoped* presentations и предлагать этот direct fix.

Отдельно исследован source-generic surface, где `TSource` выводится
из receiver и поэтому сохраняет tuple names текущего call site.
Это тоже не входит в первую реализацию: C# не позволяет
ограничить generic parameter одной physical tuple shape, поэтому
метод становится destination-wide и применим к другим source pairs.
Он также должен точно воспроизвести declarative/manual source
normalization для nullable reference types, `Nullable<T>` и `dynamic`. Это
отдельный рефакторинг generated surface architecture, а не часть
tuple mapping contract.

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

`MORPH0056` (`Registration`, `Error`) сообщает о conflicting tuple
presentation одной physical pair между иначе независимо допустимыми
registrations. Прямая повторная регистрация physical pair внутри одного
mapper по-прежнему получает только `MORPH0013`, даже если tuple aliases
в declarations различаются. Одна registration не получает обе diagnostics
за один conflict.

Остальные failures по возможности используют уже существующие
construction, member, conversion, nullability и completeness diagnostics с
tuple-aware target names.

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
  раз. Для generator-owned path затем `ValueTuple` изменяется assignments, а
  `System.Tuple` при необходимости получает одну final canonical
  reconstruction.
- `ConstructUsing`/`ResolveUsing` callback всегда выполняется. Generator не
  оптимизирует его внутренние вычисления и не заменяет returned result другим
  tuple. Применяются только допустимые post-result assignments/nested Updates;
  creation-only scalar rule получает `MORPH0042`.
- `System.Tuple` не требует explicit factory, если все required values доступны.
- Long tuples понижаются в required nested BCL representation, но
  generated declarative surface и diagnostics остаются плоскими.
- Generated tuple-plan namespaces и type names используют readable
  source-level contracts и не зависят от того, предоставил тип reference или
  runtime assembly. Stable hashes не входят в template type names.
- Value tuple Update изменяет текущий selected tuple value и возвращает его.
  Обычно это сам by-value `destination` parameter; отдельный local создаётся
  только при необходимости. Caller-owned value не изменяется по ссылке.
- На fused path только surviving final element expressions вычисляются, каждое
  ровно один раз и в обычном declarative order. Если constructor argument order
  отличается, generator сохраняет observable order через readable locals.
  Result-dependent paths сохраняют observable initial construction; overridden
  initial expressions поэтому могут вычисляться. Whole-result selectors всегда
  сохраняют свой lifecycle и evaluation.
- Generated `ResolveUsing` для `System.Tuple` не содержит `ReferenceEquals`,
  equality checks или hidden canonical reconstruction.
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
- Source и destination tuples являются каноническим typed composition для
  статически известных multi-source, multi-destination и per-call user-state
  сценариев. Mapping при этом остаётся одной зарегистрированной парой типов.
- Existing element mappings не объединяются и не запускаются автоматически:
  composition задаётся explicit nested rules. State передаётся в nested
  mapping как часть его source tuple, а не через ambient mutable context.
- Отдельные public APIs для этих сценариев сейчас не требуются. Их имеет смысл
  рассматривать только если понадобится дополнительная ergonomics или
  automatic state propagation с отдельным публичным контрактом.

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
- authoritative `ConstructUsing`/`ResolveUsing` results без hidden
  reconstruction;
- explicit `Ignore`, completeness, source/destination validation и diagnostics;
- применимость обычных settings без tuple-specific public setting;
- root, nested, inherited, standalone и application/DI paths;
- deterministic incremental generation и readable C# 9 output.

Не входят runtime positional mapping arbitrary `ITuple`, projection semantics,
collection element mapping, отдельная tuple matching setting, mapper-scoped
DSL surface и source-generic surface refactoring.

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
15. `Ignore` without an existing Create value chooses element `default`; with an
    explicit construction value, factory result or reused Update it preserves
    that value. Unmentioned/missing element never gets implicit `default`,
    including with `MemberSelection.Explicit` or
    `UnmappedMemberValidation.None`.
16. `System.Tuple` default Update is a no-op for scalar slots; scalar `Members`
    alone do not reconstruct it; applicable read-only nested Update still runs.
17. `System.Tuple` declarative `Resolve` covers reuse and replacement branches;
    only replacement evaluates scalar creation rules.
18. `ConstructUsing`/`ResolveUsing` for `System.Tuple` preserve every non-null
    callback result by identity, regardless of whether it equals `previous`.
    Terminal null skips `Members`; no identity classification or hidden
    reconstruction is generated.
19. Value-producing scalar `Members` after a `System.Tuple` factory produces
    `MORPH0042` with the logical element and affected operations. `Ignore` and
    applicable explicit nested Update remain valid.
20. `ConstructUsing`/`ResolveUsing` whole-result callbacks execute exactly once
    and their internal values are never optimized away. Writable `ValueTuple`
    fields can still receive post-result assignments.
21. Update with unavailable/null destination and `NullDestinationHandling.Create`
    uses a complete tuple construction path while preserving operation context.
22. Nullable root tuples, nullable elements, null source/destination policies and
    nullability diagnostics.
23. `MemberSelection`, `MappingMode` and every `UnmappedMemberValidation` mode;
    explicit pair-level `ConstructorSelection` is inapplicable and inherited
    `ConstructorSelection` has no effect.
24. `IncludeMembers`, flattening, explicit nested mappings, inheritance and runtime
    polymorphism boundaries.
25. Concrete custom `ITuple` maps through declared static surface; `ITuple`
    interface indexing remains explicit/manual.
26. Identical recursive source and destination presentations for one physical
    pair share one DSL surface. A difference on either side produces one stable,
    actionable `MORPH0056`; presentations on different physical pairs remain
    independent. A direct duplicate in one mapper produces only `MORPH0013`.
27. Mapper contracts, DI ambiguity and standalone lookup continue to use physical
    CLR pair identity.
28. Generated snapshots preserve aliases, friendly tuple plan namespaces and
    type names, logical long-tuple arity, recursive presentations, evaluation
    order, readable locals, authoritative factory identity, C# 9 syntax and
    deterministic CRLF output.
29. Adding, removing or renaming a tuple element invalidates only affected
    incremental outputs and diagnostics.

## Documentation completion

Выполнено при реализации:

- [x] tuples убраны из opaque/manual-only ограничений;
- [x] обновлены `conventions.md`, `create-and-update.md`, API pages и
  `limitations.md`;
- [x] добавлен user-facing `tuple-mapping.md` с named, unnamed, legacy и
  Update examples;
- [x] добавлена страница `MORPH0056`, обновлены связанные diagnostics и index;
- [x] добавлены exact generated-code snapshots для tuple plans и mapper
  lowering.
