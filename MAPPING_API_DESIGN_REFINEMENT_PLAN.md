# План доработки дизайна mapping API Morphant

Статус документа: план последовательного обсуждения. Он фиксирует найденные
проблемы, предложения и порядок принятия решений, но сам по себе не меняет
контракт Morphant. Согласованные решения переносятся в
[`MAPPING_API_DESIGN.md`](MAPPING_API_DESIGN.md), после чего соответствующий
этап здесь отмечается завершённым.

Текущий [`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) описывает реализацию
прежнего `Template()`-дизайна. Перестраивать его под новый API следует только
после согласования фундаментальных этапов этого плана.

## 1. Цель

Нужно не только сделать API внутренне непротиворечивым, но и проверить, что
Morphant удобно покрывает реальные пользовательские сценарии. Формальное
наличие `MapManually` не считается достаточным покрытием, если ручной mapping
требуется для распространённой структурной задачи: коллекции, value objects,
patch/update, второго варианта одной пары или polymorphic dispatch.

Для каждого сценария отдельно определяется уровень поддержки:

| Уровень | Значение |
|---|---|
| Declarative | Morphant строит mapping plan и применяет conventions/settings |
| Direct | Пользователь задаёт итоговое значение, но сохраняется нормализованный pipeline, включая null handling |
| Manual | Пользователь полностью управляет алгоритмом и сам обрабатывает null, mutation и nested mapping |
| Unsupported | Пару нельзя зарегистрировать либо операция намеренно запрещена |

Manual остаётся обязательным escape hatch для специальных алгоритмов, но не
должен скрывать отсутствие первоклассной поддержки массового сценария.

При доработке сохраняются уже удачные инварианты нового дизайна:

- две публичные операции mapping-а остаются единственными: `MapNew` и
  `MapExisting`;
- возвращаемое значение обеих операций всегда авторитетно, включая structs,
  records и replacement destination;
- `Members` имеет одну общую перегрузку для обеих операций, а не отдельные
  правила для `MapNew` и `MapExisting`;
- `MapManually` также имеет одну общую перегрузку и остаётся альтернативой
  declarative pipeline, а не его дополнительной стадией.

## 2. Порядок работы

1. Обсуждать по одному этапу, не смешивая независимые продуктовые решения.
2. В начале этапа разбирать конкретные пользовательские сценарии и только
   затем выбирать API.
3. Считать все приведённые ниже направления предложениями, а не уже
   согласованными решениями.
4. После решения этапа сразу обновлять целевую спецификацию и отмечать здесь:
   принятое решение, сознательно отложенные возможности и следующий этап.
5. Не позволять удобству текущего generator implementation молча определять
   публичную семантику.
6. Для отложенной возможности всё равно проверять, что принятый фундамент не
   блокирует её дальнейшее добавление.
7. Не начинать переработку production-кода под новый API до завершения
   фундаментальных этапов 1–7 и обновления implementation roadmap.

Статусы этапов:

- `Не начат` — вопрос ещё не обсуждался;
- `Обсуждается` — варианты рассматриваются, решения нет;
- `Согласован` — решение перенесено в целевую спецификацию;
- `Отложен` — граница и причина отсрочки явно зафиксированы.

## 3. Очерёдность

| Этап | Узел дизайна | Горизонт | Статус |
|---:|---|---|---|
| 1 | Creation model и выбор previous | До реализации нового API | Не начат |
| 2 | Direct `Create` и capability-based surface | До реализации нового API | Не начат |
| 3 | Nullability, `Previous<T>` и null-result | До реализации нового API | Не начат |
| 4 | `MappingContext` и call frames | До реализации нового API | Не начат |
| 5 | Полная семантика nested `Map` | До реализации нового API | Не начат |
| 6 | Порядок вычислений и declarative control flow | До реализации нового API | Не начат |
| 7 | Допустимость mapping-пар и capability model | До реализации нового API | Не начат |
| 8 | Scope mapping-а и несколько вариантов одной пары | До заморозки runtime architecture | Не начат |
| 9 | Коллекции | До general-purpose release | Не начат |
| 10 | Patch/merge и conditional no-op | До general-purpose release | Не начат |
| 11 | Immutable `MapExisting` | До general-purpose release | Не начат |
| 12 | Per-call data и пользовательский context | До заморозки public contract | Не начат |
| 13 | Runtime polymorphism и inheritance | До general-purpose release | Не начат |
| 14 | Cycles и shared references | До general-purpose release | Не начат |
| 15 | `IQueryable` projection | До заморозки внутренней plan model | Не начат |
| 16 | Переиспользование и композиция конфигурации | До general-purpose release | Не начат |
| 17 | Generic, runtime-type и multi-source mapping | До фиксации support boundary | Не начат |
| 18 | Hooks, result-dependent logic и граница manual mapping | До фиксации support boundary | Не начат |
| 19 | Diagnostics и observable failures | После определения возможностей API | Не начат |
| 20 | Финальный сценарный аудит и новый implementation roadmap | После этапов 1–19 | Не начат |

## 4. Фундаментальные этапы

### Этап 1. Creation model и выбор previous

**Проблема.** Generated implicit conversion
`TDestination -> DestinationCreation` незаконен для interface destination и
одновременно слишком широк для остальных типов. Он не отличает previous от
произвольного уже созданного или cached instance, из-за чего невозможно точно
определить применимость `init` и `required` rules.

**Нужно согласовать:**

- исчерпывающий набор creation-веток: explicit constructor, convention,
  factory, previous и, возможно, отдельный готовый result;
- представление выбора previous без conversion от `TDestination`;
- нужен ли явный marker `UsePrevious()` или достаточно conversion
  `Previous<TDestination> -> DestinationCreation`;
- как generator классифицирует каждую ветку до генерации кода;
- что означает factory/cached instance для обычных setters, `init` и
  `required`;
- допустим ли arbitrary ready instance в structured `Create` и как он должен
  быть выражен, если действительно нужен.

**Предварительное направление:** заменить conversion от `TDestination` на
conversion от `Previous<TDestination>`. Тогда возврат `previous` означает
только сохранение фактического previous, а factory или cache остаются явно
обозначенной веткой. Пользователь возвращает сам `previous`; извлекать
`previous.Value` или вызывать отдельный `AsResult()` для выбора result не
нужно.

**Результат этапа:** точная generated shape `DestinationCreation`, таблица
creation-веток и нормативные примеры source-only/previous-aware `Create`.

### Этап 2. Direct `Create` и capability-based surface

**Проблема.** Scalar, opaque value object, factory-only type, interface без
writable members и многие third-party immutable types сейчас вынуждены
использовать `MapManually`. При этом они теряют стандартное null handling,
хотя их алгоритм не является по смыслу manual.

**Нужно согласовать:**

- direct `Create`, возвращающий настоящий `TDestination`, для destination без
  осмысленного structured plan;
- source-only и previous-aware формы direct `Create`;
- правило выбора между structured и direct generated surface;
- нужны ли conventions после direct result или direct всегда является
  окончательным значением;
- место static factory, parser, enum/string conversion и opaque value object;
- поведение direct `MapExisting`: сохранить previous по умолчанию, заменить
  его или требовать явного решения;
- должна ли одна пара когда-либо одновременно иметь structured и direct
  surface.

**Предварительное направление:** generated surface определяется возможностями
destination, а не пользовательским mode. Structured destination получает
creation/member plans; direct destination получает нормализованный `Create`
с `TDestination` result; `MapManually` остаётся raw alternative для обеих
категорий.

**Результат этапа:** capability-таблица destination form -> доступные
`Create`/`Members`/`MapManually` и примеры для scalar, value object,
factory-only class и interface.

### Этап 3. Nullability, `Previous<T>` и null-result

**Проблема.** Сейчас не закреплены точные nullable contracts и поведение, если
constructor/factory/direct `Create` фактически возвращает `null`.
`Previous<Customer?>` также вводит в заблуждение: `Some(null)` запрещён, но
`Value` выглядит nullable.

**Нужно согласовать:**

- использование non-null underlying destination в `Previous<T>`:
  `Customer? -> Previous<Customer>` и `MyStruct? -> Previous<MyStruct>`;
- nullable-аннотации `Value`, `TryGetValue`, declarative source и manual
  source;
- отличия `Map(source)`, declarative `Map(source, null)` после normalization и
  raw manual `Map(source, null)`;
- допустимость `null` как финального результата direct `Create` и
  `MapManually`;
- поведение `null` из structured constructor/factory перед `Members`;
- вид ошибки: compile-time diagnostic, generated guard и тип exception;
- взаимодействие с `NullSourceHandling` и `NullDestinationHandling` без
  повторного применения этих policies к factory result.

**Предварительное направление:** `Previous<T>` всегда использует non-null
underlying type. Structured pipeline требует non-null result перед member
stage и явно проверяет factory; nullable final result разрешается только там,
где контракт direct/manual mapping действительно его допускает.

**Результат этапа:** полная null-state matrix для обеих public operations и
всех creation modes.

### Этап 4. `MappingContext` и call frames

**Проблема.** Временная mutation `MappingContext.Operation` с последующим
восстановлением небезопасна при exception, recursion, reentrancy и параллельных
nested calls. В будущем тот же context должен нести общий reference cache и
другой chain state.

**Нужно согласовать:**

- разделение общего mapping scope и immutable call frame;
- identity и lifetime пользовательского `MappingContext`;
- создание nested frame с собственной `Operation` при разделяемом mapper и
  общем chain state;
- поведение при exception, recursion и нескольких nested вызовах;
- требования к thread safety manual mapping;
- какие данные принадлежат frame, а какие всей mapping chain;
- остаётся ли context пока пользовательски доступен только в `MapManually`.

**Предварительное направление:** nested call получает новый immutable frame,
который разделяет общий scope. `Operation` не мутируется на ранее переданном
пользователю object instance.

**Результат этапа:** runtime-модель context/scope/frame и псевдокод outer и
nested dispatch.

### Этап 5. Полная семантика nested `Map`

**Проблема.** В новом документе остался только абстрактный `Map()` с
автоматическим child previous. Потеряны четыре уже реализованные формы и не
определён nested mapping для constructor parameters.

**Нужно согласовать:**

- сохранение форм `Map(source)`, `Map(source, destination)`,
  `Map<TDestination>(source)` и
  `Map<TDestination>(source, destination)`;
- закон: one-argument form всегда вызывает nested `MapNew`, two-argument form
  — nested `MapExisting`, включая explicit `null`;
- явные one-argument и two-argument формы не зависят от operation внешнего
  mapping-а;
- no-argument `Map()` как shorthand с automatic source и automatic previous;
- target-inferred и explicit generic destination;
- связь constructor parameter с readable member внешнего previous;
- поведение, когда у constructor parameter нет однозначного previous-member;
- явное задание child previous пользователем;
- использование outer previous, а не replacement result, как истории;
- порядок вычисления arguments, включая named arguments;
- передача call frame и null semantics во вложенную pair.

**Предварительное направление:** сохранить все четыре явные формы, а
no-argument `Map()` добавить только как convention shorthand. Для
constructor parameter automatic previous допустим лишь при однозначной связи;
иначе нужен explicit вызов или diagnostic.

**Результат этапа:** таблица всех overload laws для body-member и constructor
parameter с `MapNew`, `MapExisting`, nullable child и replacement outer result.

### Этап 6. Порядок вычислений и declarative control flow

**Проблема.** Observable evaluation semantics из прежнего DSL не перенесена в
новый дизайн. Последовательная mutation result может изменить значение более
позднего выражения, читающего previous, и сломать даже обычный swap.

**Нужно согласовать:**

- snapshot semantics: какие explicit values вычисляются до первой mutation;
- порядок plan locals, explicit member expressions, nested calls,
  constructor arguments, assignments и convention rules;
- exactly-once semantics и сохранение пользовательского порядка побочных
  эффектов;
- aliasing source/result/previous и различия reference/value destinations;
- ветки, выражения которых неприменимы и потому не должны вычисляться;
- поддерживаемые expression- и block-lambdas, locals, `if`/`else`, `switch`,
  несколько `return` и `throw`;
- conditional и switch expressions;
- conditional `Auto()`, `Ignore()` и `Map()`;
- допустимые references/captures declarative lambdas: mapper members, static
  API, constants, method groups, Configure-locals и local functions;
- отдельная граница references/captures для `MapManually`, включая то, что
  generator может безопасно перенести из `Configure` в generated method;
- нужна ли динамическая whole-plan no-op операция, не сводимая к статическому
  `MemberMatching.Explicit`;
- остаются ли generated member-plan properties `init`-only.

**Предварительное направление:** сначала вычислять все применимые explicit
values в типизированные locals в исходном порядке, затем выполнять mutation,
после неё — оставшиеся conventions. Сохранить уже достигнутый control-flow
уровень и отдельно решить whole-plan no-op вместо автоматического возврата
`Skip()`.

**Результат этапа:** нормативный declarative algorithm и таблица
поддерживаемых/неподдерживаемых language constructs.

### Этап 7. Допустимость mapping-пар и capability model

**Проблема.** Текущая общая type policy запрещает arrays, tuples и collections
даже для `MapManually`. Поэтому заявленный универсальный escape hatch не
является универсальным. Одновременно разные операции над одной парой требуют
разных generator capabilities.

**Нужно согласовать:**

- раздельные понятия pair eligibility и declarative surface eligibility;
- независимые capabilities: structured, direct, manual, collection и
  projectable;
- минимальные ограничения для регистрации manual pair;
- root arrays, tuples, `IEnumerable`, dictionaries, scalar, abstract и
  interface types;
- root collection eligibility отдельно для sequence source и collection
  destination: `IEnumerable<Order> -> OrderSummary`,
  `Order -> List<OrderLineDto>` и collection-to-collection pair;
- осмысленность или сознательный запрет delegate/dynamic-like types;
- inaccessible/unnameable types и границы generated code placement;
- сохранение nullable, constructed generic, mapper type parameter и generic
  constraints;
- применимость `MappingMode`, `NullSourceHandling`,
  `NullDestinationHandling`, `MemberMatching`, `ConstructorSelection`,
  boxing policy, `UnmappedMemberValidation` и
  `NullabilityMismatchValidation` к каждой capability;
- defaults, inheritance и precedence settings после удаления `TemplateMode`;
- окончательное имя и семантику `NullDestinationHandling.CreateNew` /
  `TreatAsMissing`;
- считать ли неприменимую явную setting ошибкой, warning или допустимым no-op
  в direct/manual mapping;
- поведение при частично поддерживаемой pair без скрытого fallback.

**Предварительное направление:** разрешать регистрацию любой statically
nameable pair с определённым runtime contract, а ограничивать только
конкретные generated capabilities. Collections и tuples должны быть доступны
как минимум direct/manual mapping до появления automatic support.

**Результат этапа:** единая capability/settings matrix и правила генерации
pair API.

После этапа 7 нужно сделать отдельную checkpoint-проверку согласованности
фундамента и только затем обновить порядок миграции production-кода.

## 5. Продуктовые этапы

### Этап 8. Scope mapping-а и несколько вариантов одной пары

**Проблема.** Compilation-wide uniqueness пары запрещает разные
`User -> UserDto` mappings для public/admin, summary/details, bounded contexts
или версий API. Эту потребность невозможно выразить даже вручную.

**Нужно согласовать:**

- identity mapping-а: canonical type pair, mapper/profile graph, имя/variant
  или их комбинация;
- допустимость одной пары в разных `TypeMapper`;
- какой набор mappings представляет runtime `IMapper`;
- выбор mapping-а при automatic nested `Map()`;
- конфликт двух доступных вариантов в одном scope;
- связь variants с inheritance, reuse и DI registration;
- нужна ли явная named mapping возможность либо достаточно mapper-level scope;
- поведение mappings из разных assemblies.

**Предварительное направление:** как минимум ограничить uniqueness одним
mapper/profile graph, а не всей compilation. Named variants добавлять только
если mapper-level scope не покрывает реальные сценарии без искусственных DTO.

**Результат этапа:** формальная identity mapping-а и deterministic lookup law.

### Этап 9. Коллекции

**Проблема.** Без collection mapping пользователь вынужден материализовать и
перебирать DTO вручную даже при наличии element pair. Особенно важны collection
members и getter-only mutable collections в entity mappings.

**Нужно согласовать:**

- root collection и collection-member mapping;
- source sequences и baseline destination forms: array, list, set,
  collection interfaces и immutable collections;
- использование зарегистрированной element pair;
- null source collection policy и nullable elements;
- writable collection replacement;
- `MapExisting`: preserve, replace, clear-and-fill либо настраиваемая policy;
- заполнение getter-only mutable collection;
- dictionaries и mapping key/value;
- fixed-size/read-only destinations;
- element order, duplicates и set semantics;
- key-based reconciliation existing elements;
- polymorphic elements и reference handling;
- какие части входят в минимальный выпуск, а какие сознательно откладываются.

**Предварительное направление:** сначала поддержать `MapNew` основных sequence
targets, element mapping, replacement writable members и явно выбранную
existing policy. Getter-only fill нужен в базовом наборе; универсальный
key-based reconciliation не должен иметь скрытого default и может быть
следующим отдельным расширением.

**Результат этапа:** collection capability matrix, null/existing policies и
минимальный конкурентоспособный scope.

### Этап 10. Patch/merge и conditional no-op

**Проблема.** Для обычного partial update пользователь должен повторять
conditional `Ignore()` для десятков members. Nullable source value также не
различает «поле отсутствовало» и «явно присвоить null».

**Нужно согласовать:**

- настройку уровня map/root наподобие `IgnoreNullSourceValues` или более общую
  member-assignment policy;
- precedence global, map и explicit member rule;
- reference nullable, nullable value type и oblivious source;
- явный assignment `null` при включённой ignore-null policy;
- представление отсутствующего patch field (`Optional<T>` или source-owned
  contract), не смешивая его с обычным `T?`;
- conditional `Ignore()` для отдельного member;
- whole-plan runtime no-op без перехода в полный manual mapping;
- nested patch и collection patch behavior;
- влияние на `UnmappedMemberValidation` и nullability diagnostics.

**Предварительное направление:** добавить first-class ignore-null assignment
policy для распространённого merge, но не пытаться угадать field presence по
nullable type. Explicit absence требует отдельного source contract.

**Результат этапа:** точная patch matrix для absent/non-null/null values и
решение по whole-plan no-op.

### Этап 11. Immutable `MapExisting`

**Проблема.** Для record или init-only destination существующий result может
молча вернуться без изменений. Ручная полная reconstruction делает
`MapExisting` заметно менее полезным и легко теряет сохраняемые поля.

**Нужно согласовать:**

- минимально допустимое поведение immutable existing mapping;
- replacement через previous-aware `Create` как явный baseline;
- generated record clone/`with` strategy;
- возможность применять `Members` к clone для init-only properties;
- constructor-based reconstruction non-record immutable types;
- сохранение не затронутых members;
- factory и derived runtime type;
- diagnostic для silent no-op или неполного replacement plan.

**Предварительное направление:** до появления безопасного clone/reconstruction
как минимум диагностировать фактически пустой immutable `MapExisting`.
Отдельно оценить `with`-based result как declarative creation strategy, не
возвращая старый универсальный template overlay.

**Результат этапа:** поддерживаемые immutable update strategies и явная граница
между declarative replacement и manual algorithm.

### Этап 12. Per-call data и пользовательский context

**Проблема.** Mapper instance с DI покрывает сервисы, но не request-specific
tenant, user, culture, timezone, flags и formatting parameters. Текущий public
`IMapper` не принимает такие данные, а declarative DSL context не видит.

**Нужно согласовать:**

- различие injected dependencies, chain state и per-call arguments;
- public overload/options object либо typed mapping arguments;
- compile-time safety вместо string/object dictionary;
- declarative view context без возможности обойти нормализованный null
  pipeline через `Operation`;
- доступ из `Create`, `Members`, direct и manual mappings;
- propagation в nested mappings;
- lifetime, allocation и thread safety;
- влияние на mapping identity, caching, projections и generated interfaces.

**Предварительное направление:** не передавать raw call frame в declarative
lambdas. Если per-call data входит в contract, предоставить отдельное read-only
typed view/state, автоматически распространяемое по chain.

**Результат этапа:** public invocation contract либо явно зарезервированный
extension path, который не потребует ломать основные overloads.

### Этап 13. Runtime polymorphism и inheritance

**Проблема.** Base pair сама по себе не выбирает `Dog -> DogDto` для runtime
`Dog`, а ручной switch заставляет пользователя писать dispatcher. Это также
затрагивает collection elements и existing destination runtime type.

**Нужно согласовать:**

- различие configuration inheritance (`IncludeBase`) и runtime dispatch;
- регистрацию derived source/destination pairs;
- выбор наиболее конкретной pair;
- ambiguity между interfaces и несколькими inheritance paths;
- поведение, если runtime source известен, а destination derived type выбрать
  нельзя;
- preservation/replacement existing destination с derived runtime type;
- polymorphic collection elements;
- closed-world generated dispatcher без runtime reflection;
- взаимодействие с mapper scopes/variants.

**Предварительное направление:** explicit opt-in relationship между base и
derived pairs и generated most-specific dispatcher с diagnostic на
неоднозначность.

**Результат этапа:** dispatch table laws и API регистрации derived mappings.

### Этап 14. Cycles и shared references

**Проблема.** Без reference handling cyclic graph приводит к бесконечной
recursion, а повторно встреченный source instance создаёт разные destination
instances.

**Нужно согласовать:**

- opt-in или default reference tracking;
- ключ cache: source identity, mapping identity и destination type;
- scope и lifetime cache;
- момент регистрации result относительно constructor и `Members`;
- `MapExisting`, replacement, factory, direct и manual branches;
- shared reference с разными mapping variants;
- взаимодействие с polymorphic dispatch;
- cycles через immutable constructor graph;
- пользовательский/custom reference handler;
- allocation и performance при выключенной поддержке.

**Предварительное направление:** opt-in chain-level reference scope,
разделяемый call frames. Mutable result регистрируется до nested member
mapping; неразрешимые immutable constructor cycles должны давать понятный
diagnostic, а не stack overflow.

**Результат этапа:** lifecycle reference cache и список разрешимых/неразрешимых
cycle forms.

### Этап 15. `IQueryable` projection

**Проблема.** Без projection EF/query-provider пользователь либо загружает
лишние данные, либо повторно пишет mapping в `.Select(...)`. Если внутренний
plan сразу проектировать только как imperative code, добавить projection позже
может оказаться архитектурно дорого.

**Нужно согласовать:**

- public `Project(IQueryable<TSource>)` contract;
- отдельную projectable capability mapping-а;
- expression representation constructor/member plan;
- source-only semantics без previous и mutation;
- допустимые conversions, conditionals и nested maps;
- inline expansion nested projectable pair;
- запрет либо специальное представление factory, manual mapping, runtime
  context, reference cache и non-expression-compatible methods;
- diagnostic для непроецируемой pair без runtime client-side fallback;
- поддержка runtime parameters query provider-ом;
- связь projection с variants и polymorphism.

**Предварительное направление:** сохранить декларативный plan независимо от
emit-кода и заранее маркировать его capabilities. Projection принимает только
expression-compatible подмножество и никогда молча не переключается на
client-side mapping.

**Результат этапа:** projection capability rules и требования к внутренней
модели, даже если сам `Project` реализуется позднее.

### Этап 16. Переиспользование и композиция конфигурации

**Проблема.** `IncludeBase()` покрывает inheritance, но не общие fragments для
unrelated mappings, внешние configuration packages и variants. Generated plan
types неудобно возвращать из обычных helper methods, а discovery не может
неограниченно следовать за пользовательским builder code.

**Нужно согласовать:**

- root-level inheritance через `base.Configure(builder)`;
- map-level inheritance через `IncludeBase()`;
- reusable member/constructor fragments unrelated pairs;
- precedence fragment, convention и local explicit rule;
- generic fragments;
- external assembly configurations;
- discoverability source generator-ом без выполнения arbitrary code;
- взаимодействие с mapper scopes и mapping variants;
- является ли обычный method call mapper-а достаточным reuse для direct/manual
  logic, но не для declarative plan.

**Предварительное направление:** не пытаться интерпретировать произвольные
builder helper calls. Нужен явно распознаваемый composition primitive либо
сознательно ограниченный compile-time fragment contract.

**Результат этапа:** минимальный reuse model и перечень форм, оставленных
обычному C#.

### Этап 17. Generic, runtime-type и multi-source mapping

**Проблема.** Новый дизайн должен сохранить достигнутые generic scenarios и
не смешивать их с более сложными open-generic/runtime dispatch. Multi-source
mapping сейчас требует wrapper, потому что tuple pair запрещена общей policy.

**Нужно согласовать:**

- constructed generic types и nullable generic arguments;
- generic mapper type parameters и constraints;
- reusable open-generic registration;
- resolution closed pair из generic definition;
- runtime source/destination `Type` только среди generated known pairs;
- reflection-free registry и поведение неизвестной pair;
- root tuple как direct/manual multi-source input;
- нужны ли специальные overloads до трёх source или wrapper/tuple достаточно;
- ambiguity с mapping variants и polymorphic dispatch;
- generated surface для inaccessible generic arguments.

**Предварительное направление:** сначала гарантированно сохранить constructed
generics и mapper type parameters. Open-generic и runtime-type dispatcher
рассматривать как отдельные opt-in capabilities; tuple разрешить как минимум
для direct/manual pair, не создавая преждевременно отдельный multi-source DSL.

**Результат этапа:** support matrix для generic и runtime-resolved pairs и
решение по multi-source boundary.

### Этап 18. Hooks, result-dependent logic и граница manual mapping

**Проблема.** `Members` видит source и previous, но не result. Не определено,
нужны ли first-class before/after hooks, result-dependent rules и другие
imperative extension points либо это сознательная область manual mapping.

**Нужно согласовать:**

- member expression, зависящий от только что созданного result;
- `BeforeMap`/`AfterMap` и side effects;
- post-processing с authoritative replacement result;
- валидация после mapping;
- вызовы services и внешнего I/O;
- async mapping;
- private-state mutation, dynamic/`ExpandoObject` и runtime-only shapes;
- reverse mapping;
- бизнес-валидация и enrichment;
- критерий, когда ручной код является нормальной границей продукта, а когда
  свидетельствует о пробеле core API.

**Предварительное направление:** оставить I/O, async enrichment, business
validation, private-state bypass и fully dynamic mapping вне core. До
добавления generic hooks проверить, можно ли редкий result-dependent сценарий
понятно выразить explicit factory/helper или `MapManually` без дублирования
обычного structural mapping.

**Результат этапа:** явный список first-class scenarios, manual scenarios и
осознанных non-goals.

## 6. Завершающие этапы

### Этап 19. Diagnostics и observable failures

**Проблема.** Новый API вводит несколько capability boundaries. Без единого
правила ошибка может проявляться как пропущенная generation, неожиданный
`NotSupportedException`, compiler error generated code или скрытый fallback.

**Нужно согласовать:**

- категории compile-time configuration errors, analyzer warnings и допустимых
  runtime failures;
- duplicate mapping с учётом нового scope;
- смешивание manual/direct/declarative models;
- невозможный creation/member/nested marker;
- null structured result;
- неприменимый `init`/`required` rule;
- immutable existing no-op;
- unsupported control flow/capture;
- ambiguous polymorphic/generic/variant dispatch;
- non-projectable mapping;
- reference cycle, который нельзя построить;
- проигнорированный authoritative result `MapExisting`;
- отсутствие скрытых fallback во всех ошибочных конфигурациях;
- точные diagnostic IDs и формулировки можно назначать при реализации, но
  user-visible condition нужно определить в дизайне.

**Предварительное направление:** известная ошибочная конфигурация должна
диагностироваться во время compilation. Runtime `NotSupportedException`
остаётся для сознательно отключённой `MappingMode` operation или сценария,
который нельзя доказать статически; generated-code compiler errors не должны
быть частью нормального UX.

**Результат этапа:** таблица condition -> diagnostic/runtime behavior.

### Этап 20. Финальный сценарный аудит и новый implementation roadmap

После решений нужно повторно пройти не по API-методам, а по пользовательским
историям и убедиться, что ни одна массовая задача не провалилась в manual по
случайности.

**Контрольные сценарии:**

| Сценарий | Основной этап |
|---|---:|
| Mutable POCO, constructor, `init`, `required` | 1–3, 6 |
| Optional/`params` constructor parameters | 1, 5–6 |
| Factory/cached instance и interface destination | 1–3 |
| Scalar, enum/string и opaque value object | 2, 7 |
| Explicit reuse/replacement destination | 1, 3 |
| Nullable root и все null-handling branches | 3, 7 |
| Nested new/existing/nullable child | 4–6 |
| Side effects, swap и source/destination aliasing | 6 |
| Rename, calculated member и source flattening | 6 |
| Boxing/conversion и settings precedence | 7, 16 |
| Root tuple/array/collection manual pair | 7, 9, 17 |
| Public/admin или shallow/deep вариант одной pair | 8 |
| Collection root/member/getter-only/existing | 9 |
| Nullable patch и absent patch field | 10 |
| Immutable record update | 11 |
| Tenant/culture/request flag | 12 |
| Derived runtime source и polymorphic element | 13 |
| Cyclic graph и shared child | 14 |
| EF/query-provider projection | 15 |
| Base, fragment и external configuration reuse | 16 |
| Constructed/open generic и runtime destination type | 17 |
| Multi-source mapping | 17 |
| Полностью специальный synchronous algorithm | 2, 4, 7, 18 |
| Result-dependent rule, hooks и async boundary | 18 |
| Ошибочная конфигурация и проигнорированный result | 19 |

**Финальные действия:**

1. Переписать `MAPPING_API_DESIGN.md` как цельную спецификацию без следов
   отвергнутых вариантов.
2. Повторно проверить все design laws, null/evaluation/identity semantics и
   согласованность примеров.
3. Явно разделить минимальный release scope, следующие возможности и non-goals.
4. Обновить `IMPLEMENTATION_PLAN.md`: миграция с `Template()`, порядок TDD-
   срезов, settings, diagnostics, actualization и incrementality.
5. Запланировать удаление `TemplateMode`, старых template surfaces и
   устаревшей документации только в соответствующем implementation slice.
6. Для каждой отложенной возможности проверить, что текущий public/runtime
   contract оставляет совместимый путь расширения.

**Критерий готовности:** для каждого контрольного сценария указан
преднамеренный уровень Declarative/Direct/Manual/Unsupported, семантика не
зависит от догадок implementation, а следующий roadmap можно выполнять без
повторного проектирования фундамента.

## 7. Следующий этап

**Этап 1 — Creation model и выбор previous.** Начать с проверки всех веток
создания на class, struct, record, interface и nullable destination, затем
сравнить conversion от `Previous<TDestination>` с явным `UsePrevious()` и
зафиксировать одну непротиворечивую generated shape.
