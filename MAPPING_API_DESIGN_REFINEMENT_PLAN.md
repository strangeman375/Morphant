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
- правила `Members` являются общими для обеих операций, а не разделяются на
  независимые `MapNew`- и `MapExisting`-конфигурации; точный набор общих
  перегрузок можно расширить только после этапа 18;
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
8. Если пользователь однозначно откладывает этап, не проводить по нему
   отдельное исследование. Достаточно зафиксировать post-v0 границу, причину
   отсрочки и уже существующую совместимую точку расширения, после чего сразу
   перейти к следующему этапу.

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
| 8 | Scope mapping-а и несколько вариантов одной пары | До заморозки runtime architecture | Согласован |
| 9 | Коллекции | После v0 | Отложен |
| 10 | Patch/merge и conditional no-op | После v0 | Отложен |
| 11 | Immutable `MapExisting` | До general-purpose release | Согласован |
| 12 | Per-call data и пользовательский context | После v0, вместе с tuple/multi-source | Отложен |
| 13 | Runtime polymorphism и inheritance | После v0 | Отложен |
| 14 | Cycles и shared references | После v0 | Отложен |
| 15 | `IQueryable` projection | После v0 | Отложен |
| 16 | Переиспользование и композиция конфигурации | До general-purpose release | Согласован |
| 17 | Generic, runtime-type и multi-source mapping | До фиксации support boundary | Не начат |
| 18 | Hooks, result-dependent logic и граница manual mapping | До фиксации support boundary | Не начат |
| 19 | Нейминг публичного API | До заморозки public contract | Не начат |
| 20 | Diagnostics и observable failures | После определения возможностей API | Не начат |
| 21 | Финальный сценарный аудит и новый implementation roadmap | После этапов 1–20 | Не начат |

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
nested calls. В будущем скрытый scope должен нести общий reference cache и
другой внутренний chain state.

**Согласовано:**

- `MappingContext` является `readonly struct`: immutable call frame передаётся
  по значению, не имеет значимой reference identity и содержит текущую
  `MappingOperation` и scoped `IMapper`;
- отдельный `IContextualMapper` не вводится: root mapper и scoped mapper имеют
  один публичный контракт `IMapper`, но являются разными экземплярами с разным
  lifetime;
- каждый root `IMapper.Map(...)` создаёт скрытый reference-type
  `MappingScope`; он хранит scoped mapper и в будущем принимает общий reference
  cache и другой внутренний chain state;
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
всю registration. Одновременно root types без статически известной
верхнеуровневой формы и типы, представляющие последовательность, исполняемый
код либо ещё не полученное значение, требуют отдельной продуктовой семантики,
которой не должно быть в v0 даже под видом raw escape hatch.

**Согласовано:**

- eligibility и capabilities разделены. Pair допустима, если оба root-типа
  являются legal C# 9 generic arguments, могут быть названы из generated
  mapper-а, имеют статически известную верхнеуровневую форму и не входят в
  явно отложенную root-категорию;
- tuple roots (`System.ValueTuple`, tuple syntax, `System.Tuple` и реализации
  `ITuple`) полностью исключены из v0 в обеих mapping-позициях, включая direct
  и manual mapping;
- sequence, collection и buffer roots также исключены. Категория включает
  arrays, любой `IEnumerable` кроме `string`, `IEnumerator`,
  `IAsyncEnumerable<T>`, `IAsyncEnumerator<T>`, `Memory<T>`,
  `ReadOnlyMemory<T>` и `ReadOnlySequence<T>`, включая пользовательские типы,
  реализующие соответствующие контракты;
- delegate roots включают конкретные delegate-типы, `System.Delegate` и
  `System.MulticastDelegate`. Expression-tree roots включают всю иерархию
  `System.Linq.Expressions.Expression`, в том числе
  `Expression<TDelegate>`. Обе категории полностью исключены из v0;
- deferred/async roots (`Task` и его иерархия, `ValueTask`, `ValueTask<T>`,
  `Lazy<T>`) и push-sequence roots, реализующие `IObservable<T>`, полностью
  исключены из v0. Для них нужны отдельные правила выполнения, lifetime,
  исключений и последующего mapping-а;
- категория определяется после снятия верхнеуровневой `Nullable<T>`-обёртки,
  поэтому, например, `ValueTask<int>?` также запрещён. Для разрешённого
  underlying value type nullable-форма сохраняет собственную canonical pair;
- type parameter непосредственно в source или destination root запрещён
  независимо от `class`, `struct`, `new()`, base/interface и других
  constraints. Type parameter внутри известного nominal root допустим:
  `Page<T> -> PageDto<T>` сохраняется, а `T -> Destination` — нет;
- запрет относится только к root mapping-позиции. Значение любой отложенной
  категории как member, constructor parameter либо generic argument внешнего
  разрешённого root остаётся обычным единым C#-значением; element mapping,
  ожидание deferred result, expression rebinding и другая специальная
  семантика не применяются;
- технически исключены `void`, pointers/function pointers, ref-like, error,
  anonymous/unnameable и недоступные generated lexical context типы;
- допустимы scalars, enums, nullable forms, custom class/struct/record,
  abstract/interface и constructed generics с известной верхнеуровневой
  nominal-формой; их arguments могут содержать type parameters. `dynamic`
  канонически совпадает с `object`, а root nullable reference annotation не
  создаёт отдельную runtime pair;
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
  `TemplateMode` удалён. Семантика текущего
  `NullDestinationHandling.CreateNew` уточнена как обработка explicit `null`
  в качестве отсутствующего previous; `TreatAsMissing` остаётся только
  рабочим именем до отдельного naming-этапа и не считается принятым;
- неприменимая inherited setting является допустимым no-op, поскольку внешний
  уровень обслуживает много пар. Неприменимая explicit map-level setting —
  configuration error: у manual pair разрешён только `MappingMode`, а direct
  pair не принимает `ConstructorSelection`;
- частичная capability не включает fallback к manual, другой creation-ветке
  или runtime discovery. Ошибка должна диагностироваться на фактической
  недоступной operation/rule/setting.

**Результат этапа:** eligibility rules, capability/settings matrix и generated
pair surface перенесены в `MAPPING_API_DESIGN.md`. Фундаментальные этапы 1–7
образуют согласованную основу; root type parameters и специальные
tuple/sequence/collection/buffer, delegate, expression-tree, deferred/async и
push-sequence categories вынесены за v0.

После этапа 7 нужно сделать отдельную checkpoint-проверку согласованности
фундамента и только затем обновить порядок миграции production-кода.

## 5. Продуктовые этапы

### Этап 8. Scope mapping-а и несколько вариантов одной пары

**Проблема.** Compilation-wide uniqueness пары запрещает разные
`User -> UserDto` mappings для public/admin, summary/details, bounded contexts
или версий API. Эту потребность невозможно выразить даже вручную.

**Согласовано:**

- public `IMapper` является единым application-wide фасадом. Он использует
  `IServiceProvider` текущего DI-scope и видит registrations из приложения и
  всех подключённых composition root-ом assemblies;
- concrete `TypeMapper` является единицей конфигурации, генерации и
  DI-активации, но не lookup-scope и не частью ключа обычного mapping-а;
- lookup key v0 — canonical type pair. Registry хранит все её registrations;
- ноль кандидатов означает missing mapping, один кандидат выполняется, два и
  более дают runtime ambiguity;
- повторные pair registrations разрешены и сами по себе не вызывают generator
  diagnostic или startup failure. Выбор первого/последнего по порядку DI либо
  assembly registrations запрещён;
- explicit и manual nested `Map(...)` используют тот же application-wide
  registry и текущий `IServiceProvider`, что и root call. Mapping scope хранит
  call-chain state, но не ограничивает набор mappings outer `TypeMapper` или
  assembly;
- точные exception types/messages для missing и ambiguous lookup относятся к
  этапу 20.

**Отложенный keyed extension path:** после v0 descriptor можно расширить
service/mapping key типа `object?`, не меняя core-shape `IMapper.Map(...)` и
generated `ITypeMapper`. Рабочий terminal sketch:

```csharp
mapper
    .From(source)
    .To<Destination>()
    .WithServiceKey("public");
```

`WithServiceKey` пока не является принятым именем. Отдельно нужно решить,
назначается ли key mapper-у или pair, наследуется ли он nested mapping-ом,
есть ли fallback к default-варианту, как выглядит `MapExisting` fluent form и
как диагностируются повторы одной pair с одним key. Если используется
собственный registry Morphant, честнее может оказаться `WithMappingKey`.

**Результат этапа:** application-wide registry, canonical-pair lookup и
deterministic `0 / 1 / 2+` law перенесены в `MAPPING_API_DESIGN.md`. Повторные
pair registrations разрешены, keyed selection оставлен совместимым post-v0
расширением.

### Этап 9. Коллекции

**Статус:** полностью отложен до выпуска v0. До этого root
sequence/collection/buffer pair не регистрируется даже как direct/manual, а
member collection доступна только как единое значение без element mapping.

**Проблема.** Без collection mapping пользователь вынужден материализовать и
перебирать DTO вручную даже при наличии element pair. Особенно важны collection
members и getter-only mutable collections в entity mappings.

**Нужно согласовать:**

- root collection и collection-member mapping;
- source sequences и baseline destination forms: array, list, set,
  collection interfaces и immutable collections;
- отдельная граница enumerators, async sequences, `Memory<T>`,
  `ReadOnlyMemory<T>` и `ReadOnlySequence<T>`;
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

**Статус:** полностью отложен до периода после v0. Null-assignment policy,
presence-aware patch и whole-plan conditional no-op являются надстройкой над
уже согласованными creation/member/null/evaluation laws и не нужны для
надёжного каркаса v0. Проведённое исследование, сравнение с другими мапперами,
рабочая рекомендация и все найденные ограничения сохранены в
[`NULL_ASSIGNMENT_HANDLING_RESEARCH.md`](NULL_ASSIGNMENT_HANDLING_RESEARCH.md).

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

**Рабочее post-v0 направление:** рассмотреть first-class
`NullAssignmentHandling { Assign, Ignore, Throw }` с default `Assign`, общей
семантикой generated member assignment и возможностью разных effective values
для create и existing paths. Не пытаться угадывать field presence по nullable
type: explicit absence требует отдельного source contract. Это направление не
принято как public API и должно быть повторно проверено после v0.

**Результат этапа:** точная patch matrix для absent/non-null/null values и
решение по whole-plan no-op.

### Этап 11. Immutable `MapExisting`

**Проблема.** Для record или init-only destination существующий result может
молча вернуться без изменений. Ручная полная reconstruction делает
`MapExisting` заметно менее полезным и легко теряет сохраняемые поля.

**Согласовано:**

- в v0 `MapExisting` не клонирует и не реконструирует destination неявно только
  потому, что новое значение нельзя присвоить после создания. Если result —
  previous, его `init`-only, get-only и readonly state сохраняется, а обычные
  setter-rules продолжают применяться;
- декларативный replacement уже выражается existing previous-aware
  `Create`. Он может сравнить source с previous, вернуть previous при
  совместимом immutable state либо выбрать explicit constructor/convention
  result. В последнем случае `Members` применяет к новому result доступные
  creation-time `init`/`required` rules;
- для non-record immutable destination пользователь явно переносит необходимые
  сохраняемые значения через constructor/member rules. Morphant не угадывает,
  какие destination-only значения нужно копировать;
- настоящий record-copy с сохранением всех неуказанных значений уже выражается
  через `MapManually` и обычный C# `with`. Отдельный `ByCopy`, generated
  `with`-plan или автоматическое клонирование record в v0 не добавляются: они
  не дают новой capability, но вводят неявную смену identity и отдельные
  вопросы copy-constructor/derived-runtime-type semantics;
- source-only `Create` не выполняется при существующем previous и потому не
  является replacement-path для immutable `MapExisting`;
- если включён declarative `MapExisting`, но его existing-ветка статически не
  может ни выбрать replacement, ни выполнить хотя бы один post-construction
  assignment, это configuration diagnostic вместо молчаливого возврата
  previous. Явный previous-aware `Create`, `MapManually` либо отключённый
  `MapExisting` делают намерение наблюдаемым и устраняют эту diagnostic;
- смешанный mutable/immutable destination не считается целиком ошибочным
  только из-за сохранённого immutable member. Неприменимый explicit
  `init`-rule диагностируется отдельно, а полнота convention mapping остаётся
  под effective validation settings.

**Отложенная надстройка:** после v0 добавить отдельную opt-in настройку
условной reconstruction. Когда existing result иначе был бы переиспользован,
она должна до первой generated mutation вычислить значения creation-only
members и пересоздать result только если хотя бы одно из них отличается от
previous. Если все такие значения равны, identity previous сохраняется, а
обычные mutable rules выполняются как сейчас.

Эта policy не объединяется с `NullAssignmentHandling`: одна управляет выбором
identity/reconstruction, другая — выполнением отдельного assignment. Рабочее
имя, уровень конфигурации, equality contract, способ reconstruction
(полный MapNew-plan, копирование previous либо явно предоставленный plan),
поведение factory/derived instances и порядок side effects согласуются после
v0. До этого никакой скрытой `EqualityComparer<T>.Default`-семантики или
fallback reconstruction в контракте нет.

**Результат этапа:** v0 использует только явный replacement через
previous-aware `Create` либо manual `with`/reconstruction, диагностирует
статически неизбежный полный no-op и сохраняет совместимый путь к отдельной
post-v0 настройке `recreate when changed`.

### Этап 12. Per-call data и пользовательский context

**Статус:** отложен до post-v0 поддержки root tuples и multi-source mapping на
этапе 17. Отдельный arguments/context contract не входит в v0 и сейчас не
добавляется в публичные интерфейсы.

**Проблема.** Mapper instance с DI покрывает сервисы, но не request-specific
tenant, user, culture, timezone, flags и formatting parameters. Текущий public
`IMapper` не принимает такие данные, а declarative DSL context не видит.

**Согласовано:**

- injected dependencies остаются зависимостями mapper-а из DI, внутренний
  chain state остаётся в `MappingScope`, а пользовательские данные конкретного
  вызова являются обычной частью source;
- после включения tuple roots mapping вида
  `(Order Order, MappingState State) -> Invoice` естественно покрывает и
  multi-source mapping, и strongly typed пользовательский state;
- существующие overload-ы `IMapper` и `ITypeMapper` не меняются. Отдельные
  `TArguments`, options object, string/object dictionary, ambient `AsyncLocal`
  и пользовательский payload в `MappingContext`/`MappingScope` не вводятся;
- state не получает специальной семантики. Его тип и позиция входят в
  canonical tuple-source type; имена tuple-elements отдельную mapping identity
  не создают;
- nested mapping получает нужный state только явно, например через
  `Map((source.Address, source.State))`. Автоматической propagation по всей
  mapping chain нет;
- `NullSourceHandling` относится к tuple root целиком. Nullable tuple-elements
  остаются обычными частями source и обрабатываются явными rules или их
  собственными nested mappings;
- влияние tuple-source на projection согласуется отдельно на этапе 15;
- отдельный per-call mechanism следует повторно рассматривать только при
  подтверждённой потребности в неявной propagation state по всей chain.

**Результат этапа:** публичный invocation contract v0 не расширяется.
Совместимый strongly typed путь для per-call data зарезервирован через будущую
tuple/multi-source support; его точная generated surface согласуется на этапе
17.

### Этап 13. Runtime polymorphism и inheritance

**Статус:** отложен до post-v0. Полное исследование сохранено в
[`RUNTIME_POLYMORPHISM_RESEARCH.md`](RUNTIME_POLYMORPHISM_RESEARCH.md).

**Проблема.** Base pair сама по себе не выбирает `Dog -> DogDto` для runtime
`Dog`, а ручной switch заставляет пользователя писать dispatcher. Это также
затрагивает collection elements и existing destination runtime type.

**Согласованная v0-граница:**

- обычный lookup всегда выполняет ровно requested canonical pair; runtime-тип
  source не выбирает derived registration автоматически;
- `IncludeBase()` наследует только mapping-конфигурацию и не включает runtime
  dispatch;
- base и derived registrations независимы. Само наличие `Dog -> DogDto` не
  меняет поведение `Animal -> AnimalDto`;
- специальный polymorphic алгоритм уже выражается через `MapManually` с явным
  type-switch и application-wide exact nested mappings;
- основной массовый сценарий polymorphic collection elements остаётся
  отложенным вместе с общей collection support;
- `IMapper`, `ITypeMapper`, `MappingContext`, `MappingScope` и базовый registry
  не расширяются ради будущей feature.

**Рабочее post-v0 направление:** отдельная explicit-связь на base pair с
условным именем `IncludeDerived<TSource, TDestination>()`. Она перечисляет
закрытый набор допустимых derived pairs, но не наследует rules и не создаёт
registration. `IncludeAllDerived` и application-wide поиск assignable pairs не
рекомендуются.

Сначала registry разрешает конкретный base descriptor по правилу `0 / 1 / 2+`,
затем его generated dispatcher выбирает единственный most-specific explicit
source link. Несравнимые interface-ветки дают ambiguity независимо от порядка
регистрации; неизвестный subtype использует base mapping. Выбранная derived
pair снова разрешается обычным application-wide exact lookup, а её
missing/ambiguity не скрывается fallback-ом.

Для `MapExisting` derived branch применяется только при `null` previous либо
runtime-совместимом derived destination. Несовместимый previous передаётся
base mapping-у: Morphant не выбрасывает его и не вызывает derived `MapNew`
неявно. При `null` previous вызывается именно derived `MapExisting` с `null`,
после чего действует её own `NullDestinationHandling`.

Runtime dispatch и projection считаются разными capabilities. Derived links
могут быть общей декларацией, но expression-tree lowering и query-provider
ограничения согласуются отдельно на этапе 15; client-side fallback запрещён.

**После v0 нужно согласовать:**

- окончательное имя и сторону API регистрации;
- direct либо транзитивные dispatch links;
- unknown-subtype policy для abstract base destination;
- точные diagnostics и observable lookup errors;
- keyed variant propagation;
- collection element lifecycle и projection capability.

**Результат этапа:** v0 сохраняет exact-pair semantics и manual escape hatch;
automatic runtime dispatch отложен как совместимая explicit надстройка над
descriptor registry.

### Этап 14. Cycles и shared references

**Проблема.** Без reference handling cyclic graph приводит к бесконечной
recursion, а повторно встреченный source instance создаёт разные destination
instances.

**Отложено до после v0.** Публичный API и текущий declarative contract менять
не требуется: chain-wide состояние уже имеет отдельную точку расширения в
`MappingScope`.

Сохранённое рабочее направление:

- tracking является opt-in policy с default `None`;
- cache key состоит из reference identity source и identity уже разрешённого
  mapping descriptor-а;
- result регистрируется после `Create`, но до `Members`;
- поэтому setter/field cycles разрешимы, а constructor/initializer cycles до
  появления result принципиально неразрешимы;
- repeated source возвращает тот же result без повторного выполнения rules;
- конфликтующие non-null previous в `MapExisting` являются observable error;
- `MapManually`, custom handler, `MaxDepth` и projection не включаются в
  built-in policy автоматически.

Полное уже выполненное исследование сохранено в
[`REFERENCE_HANDLING_RESEARCH.md`](REFERENCE_HANDLING_RESEARCH.md). Точное имя
setting, diagnostics и runtime-типы cache согласуются только при возвращении к
feature после v0.

**Результат этапа:** реализация отложена; текущий `MappingScope` сохраняет
совместимую точку для opt-in reference cache.

### Этап 15. `IQueryable` projection

**Однозначно отложено до после v0 без отдельного исследования.** В v0 нет
public `Project(...)`, projectable capability и expression-tree roots. Текущая
runtime mapping semantics не должна молча обещать совместимость с query
provider-ами или client-side fallback.

При возвращении к feature отдельно проектируются public contract, допустимое
expression-compatible подмножество и внутренняя representation. Ни один из
этих вопросов не фиксирует ограничения production implementation v0.

**Результат этапа:** Projection пропущен; следующий активный этап — 16.

### Этап 16. Переиспользование и композиция конфигурации

**Согласованная v0-граница:** отдельный configuration-fragment API не
добавляется. Declarative configuration переиспользуется только через явно
выраженную C#-иерархию mapper-ов:

- `base.Configure(builder)` подключает configuration chain базового mapper-а и
  наследует его root-level settings;
- повторное объявление canonical pair в derived mapper-е само по себе не
  наследует её map-level plan;
- `IncludeBase()` на текущей pair явно подключает plan и map-level settings
  ближайшего matching mapping-а из подключённой base chain;
- без `base.Configure(builder)` либо без matching base pair вызов
  `IncludeBase()` является ошибочной конфигурацией;
- pair без `IncludeBase()` начинает с чистого map-level plan, но продолжает
  видеть root settings, унаследованные через `base.Configure(builder)`.

Effective settings разрешаются в порядке:

1. текущая pair;
2. pair, подключённая через `IncludeBase()`;
3. root текущего mapper-а;
4. roots подключённых base mapper-ов от ближайшего к дальнему;
5. assembly;
6. library default.

Plan объединяется отдельно от settings:

- локальный `Create` целиком заменяет унаследованный `Create`;
- `Members` объединяются по destination member, а локальное правило, включая
  `Ignore()`, перекрывает унаследованное;
- conventions применяются после объединения только к ещё не занятым members;
- локальный `MapManually` заменяет весь унаследованный declarative plan;
- manual plan нельзя частично объединять с локальными `Create` или `Members`.

Generator не следует за произвольными helper calls, изменяющими builder, и не
выполняет пользовательский configuration code. Обычные instance/static методы
остаются способом переиспользовать вычисления внутри `Create`, `Members` и
`MapManually`, где они являются обычным C#.

General-purpose fragments для unrelated pairs, generic fragments и
cross-assembly `IncludeBase()` откладываются. Готовые mappings из внешней
assembly независимо попадают в application-wide registry и не импортируют
configuration plan друг друга. Будущие keyed variants также не выводят
composition из registry.

**Результат этапа:** v0 использует только явную hierarchy-based composition;
следующий активный этап — 17.

### Этап 17. Generic, runtime-type и multi-source mapping

Tuple/multi-source и per-call-data части этого этапа отложены до после v0
вместе со специальной tuple support. В v0 сохраняются constructed generic roots
со статически известной nominal-формой и type parameters внутри их arguments.
Type parameter непосредственно в root-позиции запрещён этапом 7 и может быть
пересмотрен здесь после v0 только как отдельная capability.

**Проблема.** Новый дизайн должен сохранить согласованные для v0 constructed
generic scenarios, но не переносить автоматически прежнюю поддержку bare root
type parameter и не смешивать её с более сложными open-generic/runtime
dispatch. Multi-source mapping сейчас требует wrapper, потому что tuple pair
запрещена общей policy.

**Нужно согласовать:**

- constructed generic types и nullable generic arguments;
- generic mapper type parameters внутри известных nominal roots;
- нужна ли после v0 поддержка type parameter непосредственно как root и какие
  constraints могли бы сделать её предсказуемой;
- reusable open-generic registration;
- resolution closed pair из generic definition;
- runtime source/destination `Type` только среди generated known pairs;
- reflection-free registry и поведение неизвестной pair;
- root tuple как direct/manual multi-source input;
- generated declarative surface для tuple-source и разрешение конфликтов
  одинаковых convention-members из разных tuple-elements;
- использование обычного tuple-element как strongly typed пользовательского
  state без отдельного arguments API;
- явную передачу state в nested tuple mappings без ambient propagation;
- canonical identity по типам и порядку tuple-elements без учёта их имён;
- нужны ли какие-либо специальные overloads до трёх source сверх обычной
  tuple pair;
- ambiguity с будущими keyed variants и polymorphic dispatch;
- generated surface для inaccessible generic arguments.

**Предварительное направление:** сначала гарантированно сохранить constructed
generics и вложенные mapper type parameters при известной верхнеуровневой
nominal-форме. Bare root type parameter, open-generic и runtime-type dispatcher
рассматривать после v0 как отдельные opt-in capabilities. Tuple является
обычным `TSource` и должна покрыть multi-source mapping и явно передаваемый
пользовательский state без новых overload-ов `IMapper`; отдельно остаётся
согласовать её declarative/convention surface.

**Результат этапа:** support matrix для generic и runtime-resolved pairs и
точная tuple/multi-source boundary, включая явный пользовательский state.

### Этап 18. Hooks, result-dependent logic и граница manual mapping

**Проблема.** `Members` видит source и previous, но не result. В частности,
factory или direct `Create` может вернуть cached, derived либо иным образом
runtime-настроенный destination, а значения последующих members должны
зависеть от фактического состояния именно этого instance. Не определено,
нужен ли для такого структурного сценария узкий result-aware API, нужны ли
first-class before/after hooks и другие imperative extension points либо это
сознательная область manual mapping.

**Нужно согласовать:**

- member expression, зависящий от runtime-state только что созданного factory
  или direct result;
- возможную общую перегрузку `Members(source, previous, result)`, где previous
  и result представлены presence-aware nullable-обёртками; точная shape,
  нейминг, применимость к `MapNew`/`MapExisting` и поведение при `null` result
  пока не выбраны;
- порядок чтений result относительно shallow snapshot и generated member
  assignments, включая намеренную зависимость от setter side effects;
- `BeforeMap`/`AfterMap` и side effects;
- post-processing с authoritative replacement result;
- валидация после mapping;
- вызовы services и внешнего I/O;
- async mapping;
- post-v0 boundary для `Task`/`ValueTask`/`Lazy`/`IObservable` root pairs:
  нужен ли им отдельный mapping contract либо они должны остаться вне core;
- private-state mutation, dynamic/`ExpandoObject` и runtime-only shapes;
- reverse mapping;
- бизнес-валидация и enrichment;
- критерий, когда ручной код является нормальной границей продукта, а когда
  свидетельствует о пробеле core API.

**Предварительное направление:** оставить I/O, async enrichment, business
validation, private-state bypass и fully dynamic mapping вне core. До
добавления generic hooks отдельно проверить узкий result-aware `Members`:
factory-result с runtime-state является структурным mapping-сценарием и не
должен автоматически проваливаться в `MapManually`. Конкретная перегрузка при
этом остаётся лишь кандидатом до разбора observable semantics и примеров.

**Результат этапа:** явный список first-class scenarios, manual scenarios и
осознанных non-goals.

## 6. Завершающие этапы

### Этап 19. Нейминг публичного API

**Проблема.** Имена появлялись по мере согласования отдельных семантических
узлов и не проходили общий аудит на call sites. В частности,
`TreatAsMissing` описывает уже согласованное поведение
`NullDestinationHandling`, но пока не принято как окончательное имя.

**Нужно согласовать:**

- окончательное имя ветки `NullDestinationHandling`, которая считает explicit
  `null` отсутствующим previous, без ложного обещания создать новый instance;
- имена generated creation/member-plan типов и их согласованность с
  `Create` / `Members`;
- терминологию для presence wrappers, включая `Previous<T>` и возможный
  result-wrapper этапа 18;
- согласованность пар `MapNew` / `MapExisting`, declarative / direct / manual и
  публичных settings;
- короткие примеры IntelliSense/call-site для каждого спорного имени и
  отсутствие конфликтов между `Default`, рабочими названиями и значениями по
  умолчанию.

**Предварительное направление:** проводить naming-аудит после определения
семантики продуктовых этапов, но до diagnostics и migration roadmap. Ни одно
рабочее имя не становится принятым только потому, что оно используется в
текущем design-документе.

**Результат этапа:** финальная таблица public names, обновлённая цельная
спецификация и явный список намеренно оставленных внутренних рабочих терминов.

### Этап 20. Diagnostics и observable failures

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

### Этап 21. Финальный сценарный аудит и новый implementation roadmap

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
| Root tuple/array/sequence/collection/buffer pair | Unsupported в v0; 9 и 17 после v0 |
| Root delegate pair | Unsupported в v0; отдельный этап после v0 при реальной потребности |
| Root expression-tree pair | Unsupported в v0; связь с projection рассматривается на этапе 15 после v0 |
| Root `Task`/`ValueTask`/`Lazy`/`IObservable` pair | Unsupported в v0; async/deferred boundary рассматривается на этапе 18 после v0 |
| Type parameter непосредственно как root | Unsupported в v0; отдельная generic capability этапа 17 после v0 |
| Public/admin или shallow/deep вариант одной pair | Unkeyed повторы допустимы, но неоднозначны при использовании; keyed selection после v0 |
| Collection root/member/getter-only/existing | 9 |
| Nullable patch и absent patch field | После v0 (этап 10) |
| Immutable record update | 11 |
| Tenant/culture/request flag | После v0: tuple-source на этапах 12 и 17 |
| Derived runtime source и polymorphic element | После v0 (этап 13) |
| Cyclic graph и shared child | 14 |
| EF/query-provider projection | 15 |
| Base, fragment и external configuration reuse | 16 |
| Constructed/open generic и runtime destination type | 17 |
| Multi-source mapping | 17 |
| Полностью специальный synchronous algorithm | 2, 4, 7, 18 |
| Result-dependent rule, hooks и async boundary | 18 |
| Публичный нейминг, включая рабочее `TreatAsMissing` | 19 |
| Ошибочная конфигурация и проигнорированный result | 20 |

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

**Этап 17 — generic, runtime-type и multi-source mapping.** Этап 16 ограничил
v0-композицию явно подключённой C#-иерархией mapper-ов: `base.Configure`
наследует root settings, `IncludeBase()` отдельно импортирует ближайший
map-level plan, а arbitrary builder helpers и general-purpose fragments не
анализируются. Следующий активный вопрос — окончательная support boundary для
constructed generic roots и необходимость любых возможностей сверх уже
отложенных tuple/multi-source, bare root type parameter, open-generic и
runtime-type mapping.
