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
| Direct | Пользователь напрямую получает базовый result без constructor-plan, но сохраняется нормализованный pipeline, включая null handling и применимые member rules |
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

- `Не начат` — согласованное решение по этапу ещё не зафиксировано;
- `Согласован` — решение перенесено в целевую спецификацию;
- `Отложен` — граница и причина отсрочки явно зафиксированы.

## 3. Очерёдность

| Этап | Узел дизайна | Горизонт | Статус |
|---:|---|---|---|
| 1 | Creation model и выбор previous | До реализации нового API | Согласован |
| 2 | Direct `Create` и capability-based surface | До реализации нового API | Согласован |
| 3 | Nullability, `Previous<T>` и null-result | До реализации нового API | Согласован |
| 4 | `MappingContext` и call frames | До реализации нового API | Согласован |
| 5 | Полная семантика nested `Map` | До реализации нового API | Согласован |
| 6 | Порядок вычислений и declarative control flow | До реализации нового API | Согласован |
| 7 | Допустимость mapping-пар и capability model | До реализации нового API | Согласован |
| 8 | Scope mapping-а и несколько вариантов одной пары | До заморозки runtime architecture | Не начат |
| 9 | Коллекции | После v0 | Отложен |
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

**Согласовано:**

- structured `DestinationCreation` имеет закрытый набор веток: explicit
  constructor, convention, factory и previous;
- generated implicit conversion существует от
  `Previous<TDestination>` к `DestinationCreation`, но не от произвольного
  `TDestination`;
- для выбора existing result в structured `Create` пользователь возвращает сам
  `previous`; direct `Create` возвращает настоящий destination и потому
  использует `previous.Value`; `AsResult()` и `UsePrevious()` не вводятся;
- готовый или cached instance выражается явной factory-веткой
  `new(ByFactory(() => instance))`; непосредственный возврат `ByFactory(...)`
  не является допустимой generated shape;
- constructor/convention result допускает creation-time `init` и `required`
  rules, а factory/previous уже существует и получает только применимые
  setter-rules; factory сама отвечает за `init`/creation-time `required`;
- никакого скрытого fallback между creation-ветками нет;
- точный nullable-контракт `Previous<T>` и поведение `null` factory-result
  зафиксированы на этапе 3.

**Результат этапа:** generated shape, creation-ветки и нормативные примеры
перенесены в `MAPPING_API_DESIGN.md`.

### Этап 2. Direct `Create` и capability-based surface

**Проблема.** Scalar, opaque value object, factory-only type, interface без
writable members и многие third-party immutable types сейчас вынуждены
использовать `MapManually`. При этом они теряют стандартное null handling,
хотя их алгоритм не является по смыслу manual.

**Согласовано:**

- каждая поддерживаемая mapping-пара получает ровно одну форму `Create`;
- если после общей destination-type policy существует хотя бы один
  поддерживаемый constructor surface, генерируется structured `Create`,
  возвращающий `DestinationCreation`;
- если поддерживаемого constructor surface нет, генерируется direct `Create`,
  возвращающий настоящий `TDestination`; наличие body-members на этот выбор не
  влияет;
- built-in, enum и отдельно определённые scalar-категории используют direct
  surface, даже если metadata типа технически содержит public constructors;
- обе формы имеют source-only и previous-aware перегрузки с одинаковой
  семантикой arity: source-only lambda не вызывается при существующем previous,
  previous-aware lambda сама выбирает result в обеих публичных операциях;
- structured previous-aware `Create` возвращает сам `previous` через generated
  conversion, а direct `Create` после проверки `HasValue` возвращает
  `previous.Value`; вводить ради косметической симметрии `DirectCreation<T>`,
  implicit unwrap или marker-метод не нужно;
- direct result семантически соответствует уже созданному result из
  `new(ByFactory(...))`: после него выполняются применимые `Members` и member
  conventions, а `init`/creation-time `required` должен заполнить direct код;
- у direct surface нет default creation для no-previous ветки: если такая ветка
  достижима, direct `Create` обязан быть настроен;
- отдельного mode нет, structured и direct surface одновременно не
  генерируются, `MapManually` остаётся raw alternative для обеих категорий.

**Результат этапа:** формы и перегрузки `Create`, capability-таблица, точный
declarative алгоритм и примеры для scalar, opaque value object, factory-only
destination и interface перенесены в `MAPPING_API_DESIGN.md`.

### Этап 3. Nullability, `Previous<T>` и null-result

**Проблема.** Сейчас не закреплены точные nullable contracts и поведение, если
constructor/factory/direct `Create` фактически возвращает `null`.
`Previous<Customer?>` также вводит в заблуждение: `Some(null)` запрещён, но
`Value` выглядит nullable.

**Согласовано:**

- public `IMapper` и generated `ITypeMapper` принимают nullable source и
  destination inputs, но возвращают ровно `TDestination`, а не безусловный
  `TDestination?`; nullability обычного result выбирает пользователь типом
  destination, чтобы non-null mapping не создавал предупреждение на каждом
  вызове;
- runtime policy может фактически вернуть `null` при non-nullable
  `TDestination`; это неизбежный компромисс для настроек, которые нельзя
  выразить условной generic-аннотацией;
- `Previous<T>` имеет `where T : notnull` и всегда использует destination без
  корневой nullability: `Customer? -> Previous<Customer>`,
  `MyStruct? -> Previous<MyStruct>`; nested generic nullability сохраняется;
- `Value` имеет non-null `T`, а `TryGetValue` использует
  `[MaybeNullWhen(false)] out T`: успешное извлечение всегда non-null,
  `Some(null)` не существует;
- declarative `Create` и `Members` получают source после
  `NullSourceHandling` как non-null underlying type; `MapManually` получает
  исходное nullable runtime-значение;
- direct `Create` и structured `ByFactory` могут вернуть `null` независимо от
  destination annotation; такой result авторитетен, немедленно возвращается и
  short-circuit-ит `Members` и conventions;
- Morphant не генерирует для этого exception, fallback или повторное
  применение `NullDestinationHandling`; previous-aware `Create`, вернувший
  `null`, намеренно заменяет previous;
- C# nullability warning остаётся основной статической защитой для
  non-nullable destination, но пользователь может сознательно подавить его
  либо получить `null` из oblivious API;
- `MapManually` возвращает пользовательский result без generated guard и без
  вмешательства pipeline;
- `null` вместо generated `DestinationCreation` или `DestinationMembers`
  является ошибкой DSL-plan, а не допустимым destination-result.

**Результат этапа:** nullable-контракты public/manual/declarative surface,
root-normalization `Previous<T>` и полная семантика `null` creation-result
перенесены в `MAPPING_API_DESIGN.md`.

### Этап 4. `MappingContext` и call frames

**Проблема.** Временная mutation `MappingContext.Operation` с последующим
восстановлением небезопасна при exception, recursion, reentrancy и параллельных
nested calls. В будущем тот же context должен нести общий reference cache и
другой chain state.

**Согласовано:**

- `MappingContext` является `readonly struct`: immutable call frame передаётся
  по значению, не имеет значимой reference identity и содержит текущую
  `MappingOperation` и scoped `IMapper`;
- отдельный `IContextualMapper` не вводится: root mapper и scoped mapper имеют
  один публичный контракт `IMapper`, но являются разными экземплярами с разным
  lifetime;
- каждый root `IMapper.Map(...)` создаёт скрытый reference-type
  `MappingScope`; он хранит scoped mapper и в будущем принимает общий reference
  cache и per-call state;
- каждый outer или nested call получает новый `MappingContext`, а все frame
  одной chain разделяют scope; `Operation` определяется выбранной перегрузкой
  `IMapper` и никогда не мутируется либо восстанавливается;
- one-argument scoped `Map` создаёт `MapNew` frame, two-argument —
  `MapExisting` frame даже для explicit `null` destination;
- nested exception не повреждает outer frame; recursion и последовательная
  reentrancy безопасны относительно operation state, и пойманный exception не
  мешает продолжить outer manual mapping;
- scope завершается в `finally` после root-вызова; сохранённый
  `context.Mapper` нельзя использовать позже, и scoped implementation обязана
  проверять lifetime;
- независимые root scopes допускают параллельное выполнение, но параллельные
  nested-вызовы внутри одного scope не поддерживаются и не получают
  thread-safety guarantee;
- пользователь получает `MappingContext` пока только в `MapManually`;
  declarative pipeline использует frame внутренне, но не добавляет context в
  `Create` или `Members`.

**Результат этапа:** runtime-модель frame/scope, scoped `IMapper`, lifetime и
thread-safety contract, а также псевдокод root/nested dispatch перенесены в
`MAPPING_API_DESIGN.md`.

### Этап 5. Полная семантика nested `Map`

**Проблема.** Автоматический nested mapping по совпавшим именам требует знать
полный набор зарегистрированных пар. Для source generator-а этот набор может
быть неполным: mapping способен находиться в другой либо в потребляющей сборке.
Попытка угадать пару сделала бы conventions зависимыми от места объявления,
а безусловный runtime lookup переносил бы забытую регистрацию на runtime.

**Согласовано:**

- nested mapping выполняется только через четыре явные формы:
  `Map(source)`, `Map(source, destination)`,
  `Map<TDestination>(source)` и
  `Map<TDestination>(source, destination)`;
- no-argument форм `Map()` и `Map<TDestination>()` нет; они дублировали бы
  `Auto()` и скрывали выбор source/previous за convention;
- обычные conventions и `Auto()` используют совпавший source-member только при
  warning-free implicit C#-преобразовании; они не ищут, не предполагают и не
  создают nested mapping-пару;
- explicit `Map(...)` требует mapping даже при наличии прямого implicit
  conversion;
- one-argument формы всегда вызывают nested `MapNew`, two-argument формы —
  nested `MapExisting`, включая explicit `null`; outer operation на этот выбор
  не влияет;
- source-тип пары определяется по статическому типу первого аргумента; destination
  выводится из целевого member/constructor parameter либо задаётся generic-
  аргументом, а generic-result должен warning-free неявно преобразовываться в
  целевое место;
- child previous пользователь передаёт явно. Автоматической связи constructor
  parameter с одноимённым member внешнего previous нет. Когда ветка зависит от
  наличия previous, пользователь явно выбирает между one- и two-argument
  формами и при existing-вызове читает исходный outer previous, а не replacement
  result;
- аргументы одного `Map(...)` вычисляются ровно один раз слева направо в порядке
  записи, включая named arguments;
- nested null handling выполняет вложенная пара без внешних проверок, fallback
  или смены operation; возвращённый result авторитетен;
- scoped `IMapper` создаёт новый immutable call frame для вложенной operation и
  сохраняет общий mapping scope.

Возможный assembly manifest или composition-root validator требований
`requires/provides` можно добавить позднее без изменения DSL. Он не является
основанием вводить неявный nested mapping сейчас.

**Результат этапа:** четыре overload laws и единая явная семантика nested
mapping перенесены в `MAPPING_API_DESIGN.md`; неоднозначность automatic source
и child previous устранена для body-members и constructor parameters.

### Этап 6. Порядок вычислений и declarative control flow

**Проблема.** Observable evaluation semantics из прежнего DSL не перенесена в
новый дизайн. Последовательная mutation result может изменить значение более
позднего выражения, читающего previous, и сломать даже обычный swap.

**Согласовано:**

- каждое выполняемое пользовательское выражение вычисляется ровно один раз;
  порядок observable side effects следует порядку записи. Невыбранные ветки,
  неприменимые rules и значения, нужные только другой mapping operation, не
  вычисляются;
- explicit constructor arguments вычисляются слева направо в порядке записи,
  затем вызывается constructor. В `ByConvention` явно записанные arguments
  идут первыми в пользовательском порядке, оставшиеся automatic arguments — в
  порядке параметров выбранного конструктора;
- для нового result из structured constructor/convention сохраняется
  естественный порядок object initializer: constructor, затем очередное
  explicit member value и его assignment в пользовательском порядке, затем
  неуказанные conventions в порядке destination-members;
- previous, factory-result и direct-result считаются уже существующими и
  потенциально aliased. Для них все применимые explicit member values сначала
  вычисляются в типизированные locals в пользовательском порядке и только
  затем выполняются generated outer assignments в том же порядке. Это
  shallow snapshot независимо от того, возвращает factory/direct code новый,
  cached, source или previous instance;
- snapshot включает обычные explicit expressions, явный `Auto()` и nested
  `Map(...)`. Неуказанные conventions выполняются после explicit assignments и
  в snapshot не входят; если конкретное convention-value нужно прочитать
  заранее, пользователь делает rule явным через `Auto()`;
- nested `Map(...)` выполняется в позиции соответствующего rule. Его вызовы и
  любые пользовательские side effects видны последующим выражениям. Snapshot
  откладывает только outer assignments, генерируемые Morphant, и не является
  deep snapshot либо транзакцией object graph; нужную более раннюю точку
  чтения пользователь явно задаёт declarative local;
- declarative structured `Create` и `Members` сохраняют конечный анализируемый
  control flow прежнего DSL: expression-lambda, locals с initializer-ом,
  `const`, вложенные blocks, `if`/`else`, несколько `return`, `throw`,
  statement `switch`, conditional- и switch-expressions. `Auto()`, `Ignore()`
  и `Map(...)` могут участвовать в поддерживаемых условных формах. Ветви
  планируются отдельно, а утратившие значение условия и их зависимости не
  выполняются;
- во внешнем declarative block не поддерживаются изменяемые или
  неинициализированные locals, assignments, standalone side-effect statements,
  loops, `try`, Configure/local functions и остальные императивные формы.
  Direct `Create`, тело `ByFactory` и `MapManually` переносятся как обычный
  синхронный C# block и могут использовать соответствующий обычный control
  flow;
- переносимые expressions могут обращаться к instance/static members mapper-а,
  static API, method groups и compile-time constants. Обычные Configure-locals,
  `builder` и local functions из внешнего `Configure` не захватываются;
  переиспользуемая логика выносится в обычный member mapper-а. Local functions,
  объявленные внутри переносимого direct/factory/manual block, сохраняются;
- generated `DestinationMembers` использует обычные `set`-properties как
  совместимую точку будущего расширения, но текущая declarative grammar не
  поддерживает mutation уже созданного plan-а;
- динамический whole-plan no-op и отдельный `Skip()` сейчас не вводятся.
  Статический no-op выражается `MemberMatching.Explicit`, специальный
  динамический алгоритм — `MapManually`; first-class решение повторно
  рассматривается вместе с patch/merge на этапе 10.

**Результат этапа:** нормативные evaluation phases, shallow snapshot и граница
declarative/ordinary C# control flow перенесены в `MAPPING_API_DESIGN.md`.

### Этап 7. Допустимость mapping-пар и capability model

**Проблема.** Pair eligibility смешивалась с наличием template/declarative
surface, из-за чего capability конкретного destination могла молча запрещать
всю registration. Одновременно root tuples и collections требуют отдельной
продуктовой семантики, которой не должно быть в v0 даже под видом raw escape
hatch.

**Согласовано:**

- eligibility и capabilities разделены. Pair допустима, если оба root-типа
  являются legal C# 9 generic arguments, могут быть названы из generated
  mapper-а и не входят в явно отложенную root-категорию;
- tuple roots (`System.ValueTuple`, tuple syntax, `System.Tuple`) и collection
  roots полностью исключены из v0 в обеих mapping-позициях, включая direct и
  manual mapping. Collection означает array либо любой `IEnumerable`, кроме
  `string`, включая dictionaries и custom collection types;
- запрет относится только к root mapping-позиции. Tuple/collection member,
  constructor parameter или generic argument внешнего non-collection root
  остаётся обычным единым C#-значением; element mapping не выполняется;
- технически исключены `void`, pointers/function pointers, ref-like, error,
  anonymous/unnameable и недоступные generated lexical context типы;
- допустимы scalars, enums, nullable forms, custom class/struct/record,
  abstract/interface, delegates, constructed generics и mapper type
  parameters. Delegate destination использует direct/manual surface, delegate
  source не меняет capabilities другого destination, `dynamic` канонически
  совпадает с `object`, а root nullable reference annotation не создаёт
  отдельную runtime pair;
- любая eligible pair получает обе runtime operations и `MapManually`.
  Destination независимо получает ровно одну форму `Create`: structured при
  наличии поддерживаемого constructor surface, direct при его отсутствии или
  для opaque category. `Members` генерируется независимо при наличии
  поддерживаемых body-members;
- collection и projection capabilities отсутствуют в v0. Collections и tuples
  рассматриваются только после v0 на собственных продуктовых этапах;
- `MappingMode` применяется к declarative и manual models. Null handling,
  member/constructor matching, boxing и validation settings применяются только
  к соответствующим стадиям declarative pipeline; direct creation body и
  manual lambda остаются обычным пользовательским C#;
- общий precedence остаётся
  `map -> mapper root -> assembly -> library default`, `Default` наследует.
  `TemplateMode` удалён. `TreatAsMissing` окончательно заменяет имя
  `NullDestinationHandling.CreateNew`;
- неприменимая inherited setting является допустимым no-op, поскольку внешний
  уровень обслуживает много пар. Неприменимая explicit map-level setting —
  configuration error: у manual pair разрешён только `MappingMode`, а direct
  pair не принимает `ConstructorSelection`;
- частичная capability не включает fallback к manual, другой creation-ветке
  или runtime discovery. Ошибка должна диагностироваться на фактической
  недоступной operation/rule/setting.

**Результат этапа:** eligibility rules, capability/settings matrix и generated
pair surface перенесены в `MAPPING_API_DESIGN.md`. Фундаментальные этапы 1–7
образуют согласованную основу; tuple/collection support вынесен за v0.

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
- выбор mapping variant для explicit nested `Map(...)`;
- конфликт двух доступных вариантов в одном scope;
- связь variants с inheritance, reuse и DI registration;
- нужна ли явная named mapping возможность либо достаточно mapper-level scope;
- поведение mappings из разных assemblies.

**Предварительное направление:** как минимум ограничить uniqueness одним
mapper/profile graph, а не всей compilation. Named variants добавлять только
если mapper-level scope не покрывает реальные сценарии без искусственных DTO.

**Результат этапа:** формальная identity mapping-а и deterministic lookup law.

### Этап 9. Коллекции

**Статус:** полностью отложен до выпуска v0. До этого root collection pair не
регистрируется даже как direct/manual, а member collection доступна только как
единое значение без element mapping.

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

Tuple/multi-source часть этого этапа отложена до после v0 вместе со специальной
tuple support. Сохранение уже поддерживаемых constructed generics и mapper type
parameters не зависит от этой отсрочки.

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
рассматривать как отдельные opt-in capabilities; после v0 отдельно решить,
достаточна ли direct/manual tuple pair или нужен first-class multi-source DSL.

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
- `null` вместо structured creation/member plan;
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
| Root tuple/array/collection pair | Unsupported в v0; 9 и 17 после v0 |
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

**Этап 8 — scope mapping-а и несколько вариантов одной пары.** Определить
identity mapper/profile graph, границы runtime `IMapper` и deterministic lookup
для одинаковой type pair в разных scopes до заморозки runtime architecture.
