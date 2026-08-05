# План реализации нового mapping API Morphant

Дата составления: 2 августа 2026 года.

Статус: текущий implementation roadmap и журнал прогресса для
`Construct` / `Members` / `Convert`.

Нормативным источником семантики является
[`MAPPING_API_DESIGN.md`](MAPPING_API_DESIGN.md). Итоговый продуктовый аудит и
перечень post-v0 направлений находятся в
[`MAPPING_API_FINAL_AUDIT.md`](MAPPING_API_FINAL_AUDIT.md). Если roadmap и
нормативный дизайн расходятся, приоритет имеет нормативный дизайн, а roadmap
нужно уточнить до продолжения реализации.

Roadmap является рабочим документом. Если по ходу реализации выяснится, что
план, состав, границы или порядок этапов нужно изменить, его можно и нужно
редактировать и актуализировать. Изменения, затрагивающие публичный контракт,
observable behavior или границу поддержки, предварительно согласуются с
пользователем.

Согласованный дизайн также не является неизменяемым. Если в процессе
реализации появляется возможность сделать API или поведение Morphant лучше,
последовательнее либо удобнее для пользователя, такую идею можно и нужно
вынести на обсуждение до реализации. После согласования сначала
актуализируется нормативный дизайн, а затем при необходимости roadmap, код и
тесты.

Прежний `Template()`-дизайн не является compatibility target. Его production-
код не обязан компилироваться, а historical tests — проходить. Полный
pre-cleanup срез потенциально полезных реализаций, тестовых сценариев и старых
версий изменённых файлов сохранён вне solution в
[`reference/legacy-template-design`](reference/legacy-template-design/README.md).
[`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) восстановлен как
исторический roadmap. Эти материалы не обслуживаются; удачные решения из них
переносятся только осознанно и с новой спецификацией. Нельзя тратить работу на
сохранение старого поведения как такового, но и удалять потенциально полезные
наработки без отдельного reference-среза нельзя.

## Правила работы по плану

Работа идёт строго по одному этапу. Для каждого этапа действует один и тот же
цикл:

1. Перед началом согласовать с пользователем вопросы, способные изменить
   публичный контракт, observable behavior или границу поддержки.
2. Реализовать весь заявленный срез и его тесты. Не переходить к соседнему
   срезу ради удобства текущей реализации.
3. Выполнить только сфокусированную проверку изменённой категории. Полный
   прогон периодически выполняет пользователь.
4. Обновить статус этапа на `ожидает ревью`, зафиксировать следующий этап как
   заблокированный, закоммитить и опубликовать coherent change в `main`, после
   чего остановиться.
5. Пользователь проверяет реализацию и тесты. При замечаниях текущий этап
   остаётся открытым до внесения и повторной проверки всех необходимых
   изменений.
6. Только после явного подтверждения пользователя отметить этап как `принят`
   и перейти к следующему.

Допустимые статусы этапа:

- `не начат`;
- `ожидает ревью`;
- `принят`.

Начало реализации этапа не меняет его статус и не требует отдельного коммита:
переход из `не начат` в `ожидает ревью` фиксируется только вместе с
завершённой реализацией и тестами этапа.

Каждый этап должен оставлять solution текущего дизайна собираемым, а уже
принятые сценарии текущего дизайна — рабочими. Совместимость с отменённым
дизайном не проверяется. Временный скрытый fallback на другой mapping
algorithm недопустим.
До позднего этапа diagnostics ошибочная конфигурация может получать
детерминированный unsupported-path, но не должна молча менять выбранную
семантику.

Тесты проектируются как спецификация пользовательского поведения:

- generator рассматривается как black box;
- каждая категория полностью покрывает собственную задачу и остаётся
  достаточной при удалении других категорий;
- production helpers, модели, emitters и constants нельзя использовать для
  вычисления expected results;
- generated surface преимущественно проверяется полным expected source;
- executable semantics дополнительно проверяется реальным вызовом generated
  mapper-а;
- intentional overlap между категориями допустим;
- C# 9, nullable-контракт, CRLF, deterministic output и правила hint names
  сохраняются на каждом этапе.

Тесты, которые собирают generated assembly, выполняют mapper runtime либо
проверяют композицию полного production-generator-а, являются
интеграционными по своей природе. Текущие вызовы
`ConventionTypeMapperGeneratorTest.RunAndExecute` и
`StructuredConstructTypeMapperGeneratorTest.RunAndExecute` временно добавляют
runtime-проверки к exact-source категориям в unit-test project; все такие
вызовы и production composition нужно перенести в отдельный
`Morphant.Generator.IntegrationTests` не позднее этапа 22. До переноса это
явно считается техническим долгом, а не целевой организацией тестов.

Публичные XML comments и пользовательская документация обновляются вместе с
тем этапом, который вводит или меняет соответствующий контракт. Актуальная
документация использует только текущий API; прежние имена допустимы лишь в
явно обозначенном историческом контексте.

## Граница текущего roadmap

Этот план доводит до готовности согласованный core v0:

- точные `Create` / `Update` operations через две перегрузки `Map`;
- declarative pipeline `Construct` + `Members`;
- полностью ручной `Convert`;
- application-wide exact-pair registry;
- root и scoped `IMapper`, `MappingContext` и `MappingScope`;
- explicit nested `Create` / `Update`;
- settings и явная композиция через mapper inheritance;
- generated surface, actualization, incrementality и интеграционный сценарий.

Диагностики и observable failures намеренно оставлены отдельными поздними
этапами без внутренней декомпозиции. После завершения основной реализации для
каждого из них будет составлен отдельный согласованный план.

Collections, projection и остальные post-v0 возможности в текущую реализацию
не входят. Они перечислены в конце документа только для сохранения границы и
не являются следующими этапами этого roadmap.

## Следующий этап

**Фаза 2, этап 11 — previous/result-aware members и lifecycle границы.**

Статус: ожидает ревью.

Этап 10 принят. Этап 12 и все последующие этапы заблокированы до принятия
этапа 11.

## Фаза 1. Публичный фундамент и generated surface

### Этап 1. Публичный контракт и граница миграции

Статус: принят.

Цель — перевести repository на согласованный словарь нового API и создать
компилируемый фундамент, не пытаясь в том же срезе реализовать весь DSL.

Production scope:

- изменить `IMapper` и `ITypeMapper<TSource, TDestination>` на целевой
  nullable-input / non-nullable-return contract;
- ввести `Option<T>` с именованным `None`, явной фабрикой `Some(T)`,
  различием `None`, `Some(default)` и `Some(null)`, а также точным
  `Value` / `TryGetValue` contract; публичный constructor и implicit
  conversion не вводятся;
- ввести `MappingOperation.Create = 1` / `Update = 2` и immutable value-type
  `MappingContext` с `Operation` и `IMapper`; оба типа находятся в папке
  `Context` и namespace `Morphant.Context`, а значение `0` остаётся
  неинициализированным и не обозначает операцию;
- переименовать операции `MappingMode` в `Create`, `Update` и
  `CreateAndUpdate`;
- переименовать `NullDestinationHandling.CreateNew` в `Create`,
  `MemberMatching` в `MemberSelection`, а `ConstructorMember<T>` в
  `ConstructorParameter<T>`;
- подготовить общий builder- и marker-фундамент для будущих pair-specific
  `Construct`, `Members` и `Convert`, включая `Auto`, `Ignore`,
  `ByConvention`, `ByFactory` и четыре формы explicit `Map(...)`; не вводить
  временные универсальные overload-ы с неточной root-nullability;
- удалить из публичного контракта `Template()`, `TemplateMode`,
  `NullabilityMismatchValidation` и `IContextualMapper`;
- на время компилируемого перехода допускался внутренний adapter прежнего
  generator-а; после перехода production `TypeMapper` на новую модель в
  этапе 6 adapter и обслуживавшие его tests удалены.

Тестовый scope:

- consumer-side nullable metadata новых mapping interfaces;
- semantics `Option<T>` для reference, nullable value, non-nullable value и
  nested-nullable generic arguments;
- сборка существующего convention-only generated mapper-а после cutover.

Отдельная reflection-категория для буквального повторения видимой формы
runtime API не сохраняется: поведение проверяется обычными тестами, nullable
metadata — одной consumer-side проверкой, а стабильный public surface перед
публикацией будет контролироваться `PublicApiAnalyzers`.

Результат этапа: solution использует только финальные публичные имена нового
дизайна; сложные generated plans и их executable semantics ещё не обещаются.

### Этап 2. Pair eligibility, canonical identity и capability model

Статус: принят.

Цель — отделить допустимость mapping-пары от возможностей destination и
создать единый источник решений для всех последующих pipelines.

Production scope:

- реализовать полную симметричную eligibility policy для source и destination;
- исключить root type parameters и согласованные tuple, collection/buffer,
  delegate, expression-tree, deferred/async и push-sequence categories;
- сохранить допустимость built-in/BCL scalars, enums, classes, structs,
  records, interfaces, abstract types, nullable forms и constructed generic
  roots с известной nominal shape;
- зафиксировать canonical identity с учётом `Nullable<T>` и без различия
  aliases, `dynamic`/`object`, tuple names и nullable reference annotations;
- независимо вычислять runtime, manual, structured-construction,
  direct-construction и members capabilities;
- выбирать structured construction при наличии любого поддерживаемого
  доступного constructor, включая parameterless; direct surface использовать
  только при отсутствии constructor surface либо для opaque destination;
- применить полную opaque/direct policy к built-in scalars, enums, `Guid`,
  `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, `Half`,
  `Int128`, `UInt128`, `Uri`, `Version`, `BigInteger`, `Complex`, `Rune`,
  `Index` и `Range`; opaque destination не получает `Members` и automatic
  convention construction;
- для structured destination учитывать в member capability `set`, `init` и
  mutable fields; для direct destination — только обычные setters и mutable
  fields, включая `required`, но не `init`-only properties;
- определять eligibility и destination capabilities из общего generated
  assembly-context. Pair-типы, constructors и destination members должны быть
  доступны без private/protected-привилегий конкретного mapper-а; public и
  доступный `internal` surface сохраняются;
- представить capability result отдельной immutable incremental model, не
  зависящей от emitter-а.

Тестовый scope:

- самостоятельные полные матрицы eligibility для source и destination;
- root nullable, nested generic arguments, inaccessible и unnameable types;
- capability combinations: structured only, direct only, members with обеими
  creation forms и destination без body-members;
- parameterless class/struct как structured, creation-time members structured
  surface и только post-construction members direct surface;
- scalar/enum/custom-struct distinction;
- canonical pair identity и case-insensitive hint-name collision inputs;
- generic mapper shapes, разрешённые constructed roots и запрещённые bare type
  parameters.

Результат этапа: любой следующий pipeline получает готовое решение о pair и
capabilities, не повторяя symbol-policy самостоятельно.

### Этап 3. Generated construction surface

Статус: принят.

Цель — сгенерировать полный compile-time surface для structured и direct
`Construct`, а также точный pair-specific `Convert`, пока без lowering в
исполняемый mapping.

Production scope:

- генерировать `DestinationConstruction` и при необходимости
  `DestinationConstructorParameters` для structured capability;
- зеркалить поддерживаемые destination constructors через
  `ConstructorParameter<T>` и использовать C# compiler как probe для overload
  resolution;
- поддержать explicit constructor, `ByConvention()` с optional parameter
  plan, `new(ByFactory(...))` и implicit conversion из normalized
  `Option<TDestination>`;
- для direct capability генерировать `Construct`, возвращающий настоящий
  `TDestination`, без искусственного plan type;
- сгенерировать source-only и previous-aware overloads с точным normalized
  `Option<TDestination>`;
- сгенерировать единственный pair-specific `Convert` с normalized
  `Option<TDestination>` и `MappingContext`; не пытаться выразить его обычным
  методом runtime `MapperBuilder<TSource, TDestination>`, поскольку для
  nullable value destination это дало бы неверный `Option<TDestination?>`;
- использовать именованные runtime delegates `Morphant.Delegates.Construct`
  и `Morphant.Delegates.Convert` вместо голых `Func`, чтобы IntelliSense
  сохранял `source`, `previous` и `context`; отдельные generic arguments для
  normalized previous и точного result сохраняют nullable contract;
- сохранить positional, named, mixed, optional и whole-array `params` forms,
  а также explicit `ConstructorParameter<T>` cast;
- переиспользовать один generic plan original destination definition для
  closed constructed destinations;
- дедуплицировать alpha-equivalent generic pair extensions. Их constraints
  выводить только из source/destination generic definitions, не копируя
  дополнительные mapper-specific constraints; generic construction plan при
  этом точно воспроизводит constraints destination definition;
- использовать общий assembly-stable constructor surface: private/protected
  constructor не появляется только из-за lexical-привилегий конкретного
  mapper-а;
- перенести nullability/attributes/oblivious contract, `ObsoleteAttribute`,
  XML documentation, declaration order, namespace и hint-name laws из
  нормативного дизайна.

Тестовый scope:

- Shape, DestinationSupport, Constructors, Nullability, Attributes,
  Documentation, Naming, Generics и compile-time Usage как независимые
  категории;
- отсутствие structured plan у direct/opaque destination и его наличие у
  parameterless class/struct;
- обе `Construct` overload arities, единственная `Convert` overload и
  normalized previous для nullable destinations;
- nested/containing generic parameters и constraints;
- alpha-equivalent pairs с одинаковыми definition-level, но различными
  mapper-specific constraints;
- deterministic ordering и collision-safe names.

Результат этапа: IntelliSense показывает корректные `Construct` и `Convert`
surface, а C# компилятор валидирует пользовательскую форму construction plan;
TypeMapper пока не исполняет эти lambdas.

### Этап 4. Generated member surface

Статус: принят.

Цель — сгенерировать самостоятельный body-member plan нового дизайна.

Production scope:

- генерировать record `DestinationMembers` только при members capability;
- включать поддерживаемые properties и fields по accessibility, hiding и
  base-first declaration-order rules;
- включать `init`-only properties в structured surface, но исключать их из
  direct surface; обычные setters и mutable fields сохранять в обеих формах,
  включая помеченные `required`;
- использовать `Member<T>` с точным input-nullability destination member-а;
- генерировать две альтернативные `Members` overloads:
  `(source, previous)` и `(source, previous, result)`;
- типизировать обе overloads именованными `Morphant.Delegates.Members` с
  общей family name и различной generic arity, чтобы IntelliSense сохранял
  смысловые имена lambda-параметров;
- нормализовать root nullability у previous и result, сохраняя nested nullable
  annotations;
- сохранить object initializer и record `with` как compile-time composition
  surface;
- перенести documentation, attributes, generic sharing, namespace и hint-name
  contracts;
- выводить plan и fluent methods раздельными `Member` / `MemberExtension`
  artifacts; extension artifact дополняет общую
  `MorphantGeneratedMappingExtensions` partial class и не изменяет
  construction/manual artifacts предыдущего этапа.

Тестовый scope:

- Shape, DestinationSupport, Members, Inheritance, Accessibility,
  Nullability, Attributes, Documentation, Naming, Generics и Usage;
- property/field matrix, `set`, `init`, `required`, readonly/get-only и
  unsupported members;
- обе overloads для structured и direct member-capable destinations;
- отсутствие `init`-only property в direct surface при сохранении обычного
  `required set` и `required` mutable field;
- record `with` compile-time usage и отсутствие императивной mutation API;
- одновременный compile-time usage `Members` для `Destination` и
  `Destination?`, когда destination является struct, без ambiguity;
- destinations с constructors без members и с members без constructors.

Результат этапа: generated member plan является полноценным отдельным DSL
surface, но ещё не исполняется generated mapper-ом.

### Этап 5. Pair configuration discovery и semantic model

Статус: принят.

Цель — заменить прежнюю Template-centric registration model на единую модель
pair configuration.

Production scope:

- обнаруживать `Map<TSource, TDestination>()` и линейные fluent chains в
  обычном и expression-bodied `Configure`;
- распознавать обе формы `Construct`, обе формы `Members` и единственный
  `Convert` независимо от порядка fluent-вызовов;
- сохранять syntax и bound semantic information lambdas без преждевременного
  lowering;
- отделить declarative plan от manual plan в модели;
- хранить local root/map settings и подготовить места для `IncludeBase()`;
- разрешать несколько descriptors одной canonical pair в разных mapper-ах;
- выявлять конфликтующие local calls и generic unification как model states,
  не вводя diagnostics либо fallback на этом этапе;
- не следовать за aliases, delegates и произвольными helper calls,
  изменяющими builder; reusable runtime logic остаётся mapper member-ом;
- удалить `TemplateMode` и destination-wide coordination из новой модели.

Implementation shape этапа:

- прямые invocation chains сначала обнаруживаются без попытки выполнить
  configuration code;
- capability model строится только из bound `Map<TSource, TDestination>()`,
  после чего соответствующие generated plan и extension sources добавляются
  во внутреннюю compilation для точного semantic binding fluent calls;
- downstream model хранит исходный expression syntax, его `SemanticModel`,
  bound delegate signature и `IOperation`, не выполняя lowering;
- declarative и manual calls сохраняются раздельными массивами, а duplicate и
  mixed состояния — явными flags без diagnostics либо last-call fallback;
- `IncludeBase` зарезервирован отдельной composition-частью модели, но остаётся
  пустым до этапа mapper inheritance;
- generated construction/member surfaces и executable `TypeMapper` получают
  пары из новой модели; прежний Template-centric adapter удалён при принятии
  этапа 6.

Тестовый scope:

- отдельная полноценная RegistrationDiscovery-категория;
- chains, порядок вызовов, expression-bodied `Configure`, несколько pairs и
  несколько mapper-ов;
- ложные одноимённые методы из другого API;
- повторные `Construct` / `Members` / `Convert`, mixed manual/declarative и
  potentially unifying generic pairs как сохранённые invalid states;
- отсутствие неявного discovery через helper/local-function execution.
- exact binding structured/direct signatures, normalized nullable roots и
  method groups;
- полный expected semantic model как читаемый generated snapshot с проверкой
  compiler warnings.

Результат этапа: generator имеет стабильную semantic input model для нового
API; observable реакция на invalid states остаётся поздней diagnostics-фазой.

## Фаза 2. Declarative mapping

### Этап 6. Convention-only `Create` и `Update`

Статус: принят.

Цель — перенести уже проверенный convention mapping на новую result/previous
модель до добавления explicit plans.

Production scope:

- реализовать default structured construction с
  `ConstructorSelection.Unambiguous`, включая parameterless constructor и
  обычную default construction custom value type;
- `Create` получает result через convention constructor и применяет body
  member conventions;
- обычный `Update` выбирает non-null previous как result и применяет допустимые
  post-construction assignments;
- возвращаемый result всегда авторитетен, включая value и nullable-value
  destinations;
- сохранить матрицы constructor/member accessibility, hiding, required,
  `[SetsRequiredMembers]` и warning-free implicit conversions;
- сохранить вычисление общих convention values не более одного раза;
- поддержать class, struct, record, nullable value, abstract/interface Update
  и constructed generic destinations согласно capabilities;
- не создавать hidden fallback для direct destinations без configured
  `Construct`.

Тестовый scope:

- полные самостоятельные категории Create, Update, Constructors, Members,
  DestinationKinds и TypeCompatibility;
- exact generated source и runtime execution;
- previous identity для reference destination и copy-return semantics для
  value destination;
- required/init differences между creation и existing result;
- unsupported no-previous direct branch без configured `Construct`, без
  подмены `default` либо runtime conversion.

Результат этапа: `builder.Map<Source, Destination>()` работает по новому
declarative lifecycle без explicit `Construct` и `Members`.
При принятии этапа полностью удалены obsolete `TemplateSurface`, legacy
discovery/planners и historical tests; они больше не входят ни в production,
ни в обязательную проверку. Их точные pre-cleanup версии позднее восстановлены
в исключённом из сборки reference-срезе для осознанного переноса решений в
этапах 7–22. Runtime-сценарии этапа временно остаются рядом с exact-source
тестами и помечены для переноса в integration project.

### Этап 7. MappingMode и declarative null normalization

Статус: принят.

Цель — зафиксировать общий prelude, на который опираются все declarative
lambdas.

Production scope:

- реализовать `MappingMode.Create`, `Update` и `CreateAndUpdate` с общим
  generated `ITypeMapper` contract;
- разрешать `MappingMode` по цепочке pair -> mapper root -> assembly -> library
  default;
- выполнять `NullSourceHandling` раньше любой destination-проверки;
- применять `NullDestinationHandling.Create` / `Throw` только в `Update`;
- после prelude формировать normalized `Option<TDestination>` и передавать в
  declarative model non-null underlying source;
- считать `Map(source)` и нормализованный `Map(source, null)` неразличимыми
  внутри declarative DSL;
- не требовать включённого public `Create`, чтобы
  `NullDestinationHandling.Create` выполнил no-previous branch внутри
  разрешённого `Update`;
- не генерировать ненужные runtime null checks для definitely non-nullable
  value types;
- обновить XML docs и settings documentation.

Тестовый scope:

- независимые полные категории MappingMode и NullHandling;
- все source/destination nullability forms и precedence combinations;
- source-first ordering, explicit-null Update и normalized lambda contracts;
- invalid effective settings как deterministic unsupported states до
  diagnostics;
- отсутствие влияния call order root/map setting на effective result.

Результат этапа: любой следующий declarative plan начинает работу с точно
определёнными non-null source и previous presence.

Реализовано: effective settings разрешаются по полной precedence chain,
`MappingMode` остаётся единым operation gate, null-source обрабатывается до
destination, а `NullDestinationHandling.Create` использует отдельный
`CreateImpl` helper внутри `Update` без зависимости от public `Create`. Helper
генерируется и для `MappingMode.Update`, когда public `Create` отключён.
Declarative source нормализуется единым policy для generated surface и
executable mapper-а;
`Nullable<TSource>` разворачивается в underlying `TSource`, а для definitely
non-nullable values проверки не генерируются. Invalid effective settings
сохраняются как детерминированные unsupported operations до diagnostics.
Public `Map` methods выполняют только settings/null prelude и dispatch:
достижимая no-previous ветка исполняется в collision-safe `CreateImpl`, а
existing-destination ветка — в collision-safe `UpdateImpl`. Helpers получают
нормализованный source и исходный `MappingContext`; `UpdateImpl` также получает
non-null параметр `destination`, поскольку это фактический destination для
обновления, а не отдельный снимок предыдущего состояния.
Самостоятельные `TypeMapperMappingModeTests` и `TypeMapperNullHandlingTests`
проверяют полный generated source, runtime laws, precedence, call order,
nullable forms и invalid states. Их runtime-вызовы входят в уже отмеченный
временный integration debt и должны быть перенесены не позднее этапа 22.

### Этап 8. Исполнение structured `Construct`

Статус: принят.

Цель — превратить generated construction plan в реальный выбор result.

Production scope:

- lowering explicit destination constructor и его parameters;
- lowering `ByConvention()` и optional
  `DestinationConstructorParameters` overrides;
- source-only `Construct` выполняется только при отсутствии previous;
- previous-aware `Construct` является полным result selector для обеих
  operations и может выбрать previous, constructor или convention branch;
- поддержать явные значения, `Auto()` и допустимый `Ignore()` для optional /
  `params` parameters;
- сохранить compiler-resolved overload, positional/named argument order и
  explicit wrapper casts;
- вычислять только выбранную branch, а explicit arguments — ровно один раз
  слева направо в порядке записи;
- специализировать previous-aware plan отдельно для известного отсутствия
  previous в `Create` и наличия destination в обычном `Update`, удаляя только
  доказанно недостижимые ветки с сохранением short-circuit и side effects;
- считать занятым для неявной body-member convention только фактически
  сформированный одноимённый constructor argument; explicit `Members` сильнее,
  а required initializer сохраняется без `[SetsRequiredMembers]`;
- не выполнять fallback к другому constructor при невозможности выбранного.

Тестовый scope:

- source-only и previous-aware semantics для Create, Update и explicit-null
  Update;
- explicit, convention, previous и conditional construction branches;
- overloads, named/mixed args, optional, `params`, casts и ambiguity inputs;
- branch reachability, side-effect counts и evaluation order;
- provided/omitted constructor parameters, corresponding body-members и
  required shared values;
- class/struct/record/nullable/generic destinations.

Результат этапа: structured `Construct` полностью определяет выбор base result;
body-members пока применяются существующей convention/member logic.

Реализовано: source-only `Construct` выполняет выбранный structured constructor
только в no-previous ветке, а previous-aware форма является полным selector-ом
для `Create`, existing `Update` и explicit-null `Update`. Поддержаны explicit
constructor, `ByConvention()` с optional parameter overrides, `Auto()` и
допустимый `Ignore()`; compiler probe сохраняет выбранный overload, wrapper
casts, positional/named/mixed argument order, optional и целиком переданный
`params` array без fallback к другому constructor-у. Каждая runtime-ветка
строит отдельный constructor/previous/unsupported leaf, поэтому невыбранные
arguments не вычисляются, а выбранные выполняются ровно один раз слева направо.

Previous-aware tree строится отдельно для `CreateImpl` и `UpdateImpl`:
известные `previous.HasValue` и защищённые `previous.Value` специализируются до
emission, поэтому обычный Create не содержит синтетический `Option.None` и
недостижимую previous-ветку, а Update работает напрямую с `destination`.
Составные short-circuit conditions сохраняют порядок и side effects;
действительно достижимый выбор previous в Create остаётся unsupported path.
Если специализация приводит обе стороны ещё вычисляемого условия к одному
plan, condition сохраняется как discard-expression, а общий plan генерируется
один раз. Это убирает дублирование branch body без потери observable effects;
части выражения, отсечённые short-circuit до condition lowering, не исполняются.

Фактически сформированный constructor argument подавляет только соответствующую
неявную member-convention. Опущенный optional/`params` parameter и `Ignore()`
member не занимают; required member без `[SetsRequiredMembers]` остаётся в
initializer и разделяет automatic value с constructor argument. Явный
`Members` rule должен оставаться сильнее этого подавления на этапе 10.

По согласованному уточнению в этап 8 перенесён минимальный declarative block
control flow: блоки, состоящие из `if` / `else` и завершающих `return`, можно
использовать для выбора whole construction branch. Это сохраняет естественный
C# 9 target typing для отдельных `return previous` и `return new(...)`;
locals, `throw`, statement `switch` и полная block composition по-прежнему
остаются этапу 12.

Самостоятельная категория `TypeMapperStructuredConstructTests` фиксирует
полный generated surface/type mapper source, compiler ambiguity, executable
lifecycle, branch reachability, side effects и evaluation order для
class/struct/record/nullable/generic destinations. Её runtime-вызовы входят в
уже отмеченный временный integration debt и должны быть перенесены не позднее
этапа 22; самостоятельные exact-source проверки остаются в unit-test project.

### Этап 9. Direct `Construct` и `ByFactory`

Статус: принят.

Цель — закрыть получение уже готового destination instance без смешивания с
manual mapping.

Production scope:

- исполнять source-only и previous-aware direct `Construct` для destination
  без structured constructor surface и для opaque destinations;
- поддержать expression lambda, natural method group и переносимую
  синхронную block lambda;
- lowering `new(ByFactory(...))` внутри structured construction;
- поддержать inline lambda, block, method group и `Func<TDestination>` из
  mapper member-а для factory;
- сохранять source-only previous reuse и previous-aware replacement laws;
- получать user result ровно один раз;
- считать runtime `null` из direct/factory ветки авторитетным терминальным
  result и не выполнять member stage;
- не считать direct result эквивалентом `Convert`: применимые member rules и
  conventions продолжают выполняться;
- не использовать automatic construction для direct destination.

Тестовый scope:

- scalars, enums, interfaces и abstract/factory-only scenarios;
- expression/block/method-group/delegate forms;
- reuse/replacement, null result, side effects и exceptions;
- instance/static mapper members, constants, supported captures и collision-
  safe helper names;
- последующее применение member conventions к non-null direct/factory result.

Результат этапа: все creation capabilities способны получить настоящий result
с единой дальнейшей member semantics.

Реализовано: direct `Construct` исполняется для opaque destination и типов без
structured constructor surface. Expression-body переносится как выражение,
natural method group либо mapper delegate материализуется в типизированный
local, а синхронный block-body целиком переносится в collision-safe private
helper. Source-only форма вызывается только в no-previous ветке; существующий
destination переиспользуется без вычисления пользовательского creation-кода.
Previous-aware форма получает точный `Option.None` / `Option.Some(destination)`
и может вернуть previous, replacement либо терминальный `null`.

Structured `new(ByFactory(...))` поддерживает inline expression/block lambda,
method group и `Func<TDestination>` из mapper member-а. Factory-body переносится
как обычный синхронный C#-код с locals, mutation, loops, exceptions и local
functions; source/previous передаются только при фактическом capture. Mapper
members и compile-time constants сохраняются, обычные Configure-locals не
переносятся. Имена generated helper/local function, delegate и result locals
разрешаются collision-safe.

Factory callable переносится в один collision-safe private helper mapper-а и
переиспользуется всеми reachable `CreateImpl` / `UpdateImpl` branches. Это
относится как к lambda body, так и к materialized method-group/delegate:
типизированный delegate local и invocation находятся внутри общего helper-а,
а operation-specific код передаёт только фактически captured source/previous.
Direct block и direct method-group/delegate используют тот же mapper-level
закон; одинаковый callable больше не объявляется заново внутри leaf-ветвей.

Полная production-композиция также закрепляет границу replacement lowering:
`MapNewFactory` / `MapNewConstructor` разрешены в `UpdateImpl` только внутри
явно выбранного control-flow replacement leaf. Обычный convention-only
`Update` не проходит через creation plan и переиспользует переданный
destination.

Любой direct/factory result вычисляется и сохраняется ровно один раз. Runtime
`null` немедленно завершает mapping до member stage; для non-null result
применяется общий post-construction convention plan. Это одинаково работает
для reference, scalar, enum, interface, abstract/factory-only, nullable value и
constructed generic destinations; automatic construction у direct destination
не появляется.

Аудит остальных generated callbacks выявил ту же потерю semantic parameter
names у всех pair-specific `Construct`, `Members` и `Convert` overloads. Они
переведены с `Func` на единый runtime-набор `Morphant.Delegates.Construct`,
`Members` и `Convert`; `Members` overloads различаются generic arity. У
`ByFactory(Func<TDestination>)` параметров callback-а нет, поэтому эта
zero-argument marker-фабрика намеренно остаётся `Func<TDestination>`.

Самостоятельная категория `TypeMapperCreationResultTests` содержит полные
exact-source спецификации direct/factory lowering и executable-проверки всех
форм, lifecycle, terminal null, side effects, exceptions, captures, collisions
и destination kinds. Два отдельных snapshots фиксируют общий mapper-level
helper для direct delegate и `ByFactory` delegate. Проверки проходят `11/11`.
Самостоятельные `MappingDelegateTests` проверяют пять runtime-сигнатур, их
generic separation и semantic parameter names (`2/2`). Runtime-вызовы входят в уже
отмеченный временный integration debt и должны быть перенесены не позднее
этапа 22; exact-source проверки остаются в unit-test project.

### Этап 10. Базовый explicit `Members` plan

Статус: принят.

Цель — сделать `Members` единственным declarative surface настройки
body-members; обычный C# внутри direct/factory/manual code остаётся свободен
инициализировать готовый instance.

Production scope:

- lowering explicit member expressions к фактически выбранному result;
- поддержать `Auto()` / `Auto<T>()`, `Ignore()` / `Ignore<T>()` и неуказанные
  members;
- explicit rule занимает member раньше convention и остаётся авторитетным при
  одноимённом constructor argument; `Ignore()` сохраняет значение выбранного
  result;
- применять обычные conventions только к незанятым members;
- реализовать `MemberSelection.Auto` и `Explicit` с pair/root/assembly/default
  resolution;
- сохранить property/field, accessibility, hiding, declaration order,
  warning-free implicit conversion и nullable laws;
- не считать matching name основанием для implicit nested mapping;
- для explicit и automatic values сохранять однократное вычисление на
  применимом path.

Тестовый scope:

- independent ExplicitRules, Markers, MemberSelection, ConventionMembers и
  Compatibility categories;
- Create/Update, previous/constructor/direct/factory results;
- set/init/required/field combinations;
- absent rule under Auto/Explicit, explicit override и Ignore preservation;
- typed marker target-typing scenarios.

Результат этапа: двухпараметрический `Members` выполняет полный обычный member
plan поверх любого выбранного result.

Реализовано: двухпараметрический expression-lambda `Members` разбирается как
самостоятельный declarative member plan. Явное expression, `Auto()` /
`Auto<T>()` и `Ignore()` / `Ignore<T>()` занимают member в пользовательском
порядке до применения conventions. Явное expression остаётся сильнее
одноимённого constructor argument; `Ignore()` не создаёт assignment, а
`Auto()` обязан разрешиться теми же exact-name и warning-free implicit C#-
правилами, что обычная convention. При `MemberSelection.Auto` conventions
добавляются только для оставшихся незанятых members, при `Explicit` — не
добавляются.

Effective `MemberSelection` разрешается по цепочке
`current map -> current mapper root -> MorphantMemberSelection -> Auto`;
`Default` продолжает цепочку, last-call-wins на C#-уровне сохраняется, а
invalid effective value остаётся детерминированным unsupported path. Public
XML comments, `docs/settings/member-selection.md` и README актуализированы
вместе с реализацией.

Effective plan передаётся в существующий construction lowering до выбора
ветви. Structured constructor получает допустимые `init`, `required`, setter
и field rules в initializer; previous получает только post-construction
setters/fields; direct и factory results также получают только доступный
post-construction plan. Create-post и Update-post mappings хранятся раздельно,
поэтому direct/factory Create использует `Option.None`, а existing Update —
исходный destination; Create не заимствует expression, специализированное под
наличие previous. Неприменимое `init` expression в existing-ветке не
вычисляется. Явные и automatic expressions испускаются по одному разу на
применимом path. Точная previous/result-aware связь replacement-ветвей,
трёхпараметрический overload и статически невозможные lifecycle-комбинации
остаются этапу 11; declarative blocks, locals и control-flow composition —
этапу 12.

Самостоятельная категория `TypeMapperMemberTests` разделена на
`ExplicitRules`, `Markers`, `MemberSelection`, `ConventionMembers` и
`Compatibility`. Она содержит полный exact-source snapshot всех пяти
generated files и executable-проверки Create/Update, constructor/direct/
factory results, `set`/`init`/`required`/field, marker preservation,
precedence, implicit/nullability conversions и однократное вычисление
(`8/8`). Runtime-вызовы входят в уже отмеченный временный integration debt и
должны быть перенесены не позднее этапа 22; exact-source проверки остаются в
unit-test project.

### Этап 11. Previous/result-aware members и lifecycle границы

Статус: ожидает ревью.

Цель — реализовать точную связь previous, result и creation/post-creation
assignments.

Production scope:

- previous внутри `Members` всегда обозначает исходный normalized destination,
  даже если `Construct` выбрал replacement;
- трёхпараметрическая overload предоставляет фактически выбранный non-null
  result без presence-wrapper;
- определять result-dependency отдельно для каждого rule и его условий;
- result-independent `init` и creation-time `required` rules могут участвовать
  в initializer structured constructor/convention branch;
- result-dependent setter/field rules выполняются только после появления
  result;
- previous/factory/direct branches применяют только реально допустимые
  post-construction rules;
- expression неприменимого `init` rule не вычисляется;
- terminal null завершает mapping до всех member rules;
- статически невозможный lifecycle сохраняется как invalid model state без
  другого fallback до diagnostics;
- неизбежный immutable Update no-op также не маскируется молчаливой сменой
  algorithm.

Тестовый scope:

- previous отличается от replacement result;
- result state, созданный constructor/factory/direct code;
- mixed result-dependent и result-independent rules в одной lambda;
- init/required/set/field across all reachable creation branches;
- unreachable expressions, side-effect counts и null short-circuit;
- immutable Update: explicit replacement, explicit reuse и invalid no-op.

Результат этапа: обе формы `Members` являются одним declarative plan, а phase
каждого rule следует только его реальным dependencies.

Реализовано: двух- и трёхпараметрические expression-lambda `Members`
нормализуются в один operation-specific plan. Для `Create`, replacement и
existing branches строятся отдельные expressions: `previous` всегда
подставляется как исходный normalized destination, а `result` — как фактически
выбранный non-null instance. Nullable value destination получает unwrapped
non-null `result`; terminal `null` из direct/factory construction завершает
ветку до member assignments.

Result-dependency определяется отдельно для каждого effective rule; compile-
time `nameof(result)` зависимостью не считается. Result-independent `init` и
creation-time `required` остаются в initializer structured constructor,
result-dependent setters и mutable fields выполняются после создания через
collision-safe result local. Previous/reuse ветка выполняет только доступные
post-construction assignments и не вычисляет неприменимые `init` expressions.
Factory result принимает только post-construction rules; явный creation-only
rule переводит конфигурацию в детерминированный unsupported state до появления
публичной diagnostic. Result-dependent creation-only rule также не получает
скрытого fallback; `[SetsRequiredMembers]` остаётся единственным C#-основанием
допустить constructor до последующих required assignments.

Неизбежный immutable `Update`, который может только вернуть previous без
assignment, больше не маскируется успешным no-op. Он получает тот же
детерминированный unsupported path во всех settings/nullability surfaces.
Previous-aware `Construct` остаётся явным выражением намерения и поэтому
разрешает как reuse, так и replacement.

Самостоятельная категория `TypeMapperMemberTests` дополнена lifecycle,
immutable и result-aware сценариями, включая полный exact-source snapshot,
previous/replacement identity, constructor/factory/direct state, mixed
dependencies, `init`/`required`/setter/field, side effects и terminal null
(`14/14`). Regression-срезы этапов 9 и 8, conventions, null handling и mapping
mode сохраняют прежнее поведение. Runtime-вызовы входят в уже отмеченный
временный integration debt; exact-source проверки остаются в unit-test
project.

### Этап 12. Declarative control flow и member-plan composition

Статус: не начат.

Цель — перенести согласованную конечную DSL grammar на раздельные construction
и member plans.

Production scope:

- expression lambdas, initialized/const locals, nested blocks;
- `if` / `else if` / `else`, несколько `return` и `throw`;
- statement `switch` с завершёнными paths;
- conditional и switch expressions для whole plan, strategy, rule и marker;
- record `with` overlays только для `DestinationMembers`;
- удалять overridden member rule и ставшие ненужными dependencies;
- подставлять Configure-local compile-time constants и переносить доступные
  mapper/static members;
- отклонять mutation-oriented и прочую grammar за зафиксированной boundary без
  попытки интерпретировать её как обычный runtime C#;
- direct/factory blocks оставлять непрозрачным обычным синхронным C#.

Тестовый scope:

- отдельные complete categories для locals/if, switch, expressions, throws и
  member `with`;
- path reachability и no-evaluation для невыбранных branches;
- override semantics member overlays;
- supported captures, pattern variables и collision-safe lowering;
- полный набор неподдерживаемых statement forms как deterministic invalid
  states до diagnostics.

Результат этапа: пользователь может декларативно выбирать plan и rules, не
получая скрытой imperative execution semantics.

### Этап 13. Общий dependency graph и observable evaluation laws

Статус: не начат.

Цель — выполнить главный carry-forward contract прежнего unified template:
одинаковое bound expression между structured `Construct` и `Members`
вычисляется один раз.

Production scope:

- построить общий path-sensitive graph для structured construction и member
  plan;
- связывать semantically identical subexpressions с учётом symbols,
  receivers, arguments, constants, selected nested operation и target;
- не разделять value из-за лишних parentheses или wrapper context;
- не объединять одинаковый текст, привязанный к разным symbols/overloads;
- вычислять required node ровно один раз на выбранном path и ни разу на
  неприменимом path;
- сохранить declarative local dependencies и explicit constructor argument
  order;
- не извлекать cross-plan nodes из direct `Construct`, factory body или
  `Convert`;
- не обещать порядок независимых member expressions, assignments либо
  видимость setter/nested side effects.

Тестовый scope:

- common expression constructor/member, duplicate member uses и target
  conversions;
- path-sensitive branches и overridden rules;
- same text / different symbol, overload, receiver, nested operation и
  destination;
- side-effect counters и exact runtime values;
- aliasing scenarios, для которых порядок намеренно не является contract;
- стабильность generated names и отсутствие лишних locals.

Результат этапа: split API не меняет число observable вычислений относительно
согласованного dependency contract.

## Фаза 3. Runtime dispatch, manual mapping и nested mappings

### Этап 14. Application registry, root mapper и mapping scope

Статус: не начат.

Цель — реализовать application-wide exact-pair dispatch без runtime reflection
для поиска mappings.

Перед кодом отдельно согласовать только внешний composition-root / DI
registration API, поскольку нормативный дизайн фиксирует registry laws, но не
публичную форму `AddMorphant(...)` и generated manifest wiring.

Production scope:

- generated descriptors и manifest для mappings приложения и подключённых
  assemblies;
- immutable application registry по canonical type pair;
- root `IMapper` с текущим `IServiceProvider` и активацией concrete generated
  TypeMapper с его dependencies;
- lookup law `0 / 1 / 2+` без first/last-registration-wins;
- несколько descriptors одной canonical pair не являются compile-time
  duplicate сами по себе;
- `MappingMode` остаётся capability выбранного descriptor, а не частью key;
- каждый root `Map` создаёт новый `MappingScope` и завершает его в `finally`;
- scoped `IMapper` использует тот же registry/provider/scope, но новый
  immutable `MappingContext` frame;
- source-only overload создаёт `Create` frame, two-argument — `Update` frame,
  включая explicit `null` destination;
- последовательная recursion/reentrancy и exception isolation;
- независимые root scopes допускают parallel calls; parallel nested use одного
  scope не получает guarantee;
- provisional internal failures допустимы до отдельного observable-failures
  этапа, но lookup result не может меняться.

Тестовый scope:

- registry construction из одного и нескольких assemblies;
- zero, one и multiple candidates;
- descriptor activation и scoped/transient mapper dependencies;
- root/nested frame operations, shared scope и provider identity;
- recursion, reentrancy, caught nested exception и scope completion;
- canonical nullable/generic pair lookup;
- отсутствие зависимости от mapper type, assembly и registration order.

Результат этапа: `IMapper` становится рабочей application-wide facade и
создаёт корректный runtime lifecycle для последующих nested calls.

### Этап 15. Полностью ручной `Convert`

Статус: не начат.

Цель — реализовать `Convert` как отдельную альтернативу declarative pipeline.

Production scope:

- единственная lambda `(source, previous, context) => TDestination`;
- source передаётся до `NullSourceHandling`, explicit-null destination — как
  `Option.None`, а исходная operation — через `context.Operation`;
- `MappingMode` применяется, остальные settings и весь declarative pipeline
  обходятся;
- expression и arbitrary synchronous block bodies переносятся как обычный C#;
- constructor/factory/mutation/loops/try/local functions/record `with` и
  несколько returns сохраняют обычную C# semantics;
- `context.Mapper` выполняет ручные nested mappings в текущем scope;
- returned value, включая `null`, всегда авторитетно;
- inherited no-effect settings не запускают скрытые stages;
- mixed manual/declarative configuration остаётся invalid state до
  diagnostics.

Тестовый scope:

- Create, Update-null и Update-value state matrix;
- null settings действительно не выполняются;
- MappingMode operation gate;
- expression/block/method calls, mutation, loops, exceptions и local
  functions;
- nested manual mapping и immutable outer frame;
- `null`, previous reuse и replacement results;
- отсутствие conventions/Construct/Members/markers.

Результат этапа: любой сложный синхронный mapping можно выразить явно, не
расширяя declarative grammar.

### Этап 16. Explicit declarative nested `Map`

Статус: не начат.

Цель — реализовать nested dispatch как явный member/constructor rule.

Production scope:

- четыре формы `Map(source)`, `Map<TDestination>(source)`,
  `Map(source, destination)` и
  `Map<TDestination>(source, destination)`;
- one-argument form всегда вызывает nested `Create`, two-argument — nested
  `Update`, независимо от outer operation;
- static nested source выводится из первого argument, destination — из target
  либо explicit generic argument;
- generic result обязан warning-free преобразовываться в target type;
- explicit `null` second argument сохраняет nested Update и не заменяется
  Create;
- child previous передаётся только явно; outer previous/result не
  подставляются generator-ом;
- nested result авторитетен и присваивается outer target;
- arguments вычисляются один раз слева направо;
- dispatch использует тот же registry/provider/scope и новый call frame;
- convention `Auto()` по-прежнему никогда не превращается в implicit nested
  mapping.

Тестовый scope:

- все четыре forms в constructor и member targets;
- outer Create/Update x nested Create/Update matrix;
- target inference, explicit destination, interfaces и conversions;
- explicit null, nullable sources и destination values;
- previous-vs-result child expressions;
- argument evaluation order, nested replacement и exception propagation;
- application-wide lookup вне outer mapper/assembly.

Результат этапа: declarative mapping поддерживает graph composition без
скрытого выбора nested pair или operation.

## Фаза 4. Settings и configuration composition

### Этап 17. Полный `ConstructorSelection`

Статус: не начат.

Цель — реализовать все согласованные стратегии выбора convention constructor,
не затрагивая explicit structured `Construct`.

Production scope:

- `Default`, `Explicit`, `Parameterless`, `Single`, `Unambiguous`, `Greediest`
  и `Largest`;
- pair -> included base pair -> mapper roots -> assembly -> library default
  precedence там, где base levels уже доступны;
- deterministic selection и отсутствие fallback после выбора;
- различие largest declared и greediest applicable constructor;
- optional/`params`, required initializer и warning-free conversion effects;
- setting не влияет на direct destinations и `Convert`;
- map-level use без structured capability сохраняется как invalid state до
  diagnostics;
- XML docs и отдельная settings page.

Тестовый scope:

- каждая strategy как самостоятельная полная contract matrix;
- ties, inaccessible/unsupported constructors, optional/`params` и missing
  arguments;
- interaction with explicit Construct, Members required/init и destination
  kinds;
- all configuration levels available at this point.

Результат этапа: convention construction целиком управляется публичной
настройкой с `Unambiguous` default.

### Этап 18. Automatic boxing policy

Статус: не начат.

Цель — добавить согласованный opt-in strict mode для automatic mappings,
требующих boxing.

Перед написанием тестов отдельно согласовать окончательное публичное имя enum
и builder method, а также точную границу потенциального boxing у type
parameters. Семантическая основа уже зафиксирована:

- library default разрешает automatic boxing;
- strict mode исключает boxing только из automatic constructor/member
  candidates;
- explicit expressions и `Convert` остаются обычным C#;
- explicit `Auto()` подчиняется той же automatic policy и не обходит её;
- setting не вводит unboxing, casts или runtime dynamic conversion.

Тестовый scope после согласования:

- value/reference, nullable boxing, enums, interfaces и generic parameters;
- constructor и member candidates;
- conventions, explicit `Auto`, explicit expression и Convert;
- settings precedence и XML/user documentation.

Результат этапа: пользователь может запретить неявные allocations/boxing в
automatic mapping, не меняя общую conversion model.

### Этап 19. Mapper inheritance, `IncludeBase()` и settings composition

Статус: не начат.

Цель — реализовать единственную v0-модель переиспользования configuration.

Production scope:

- распознавать явный `base.Configure(builder)` и подключать chain base
  mapper-ов;
- без этого вызова base configuration и roots не участвуют;
- повторная local pair без `IncludeBase()` начинает с чистого pair plan, но
  наследует подключённые root settings;
- `IncludeBase()` импортирует ближайший matching base pair и её map-level
  settings;
- exact precedence: current pair -> included base pair -> current root ->
  connected base roots nearest-first -> assembly -> library default;
- last-call-wins на каждом C# level, включая `Default`;
- local `Construct` целиком заменяет inherited `Construct`;
- local/inherited `Members` объединяются по destination member, local rule и
  `Ignore` перекрывают inherited, conventions заполняют остаток;
- dependencies строятся заново только для effective rules;
- local `Convert` заменяет весь inherited declarative plan;
- manual plan нельзя частично смешивать с local declarative configuration;
- `UnmappedMemberValidation` получает полную effective setting model, но её
  diagnostic enforcement остаётся позднему diagnostics-этапу;
- general fragments, arbitrary builder helpers и cross-assembly
  `IncludeBase()` не добавляются.

Тестовый scope:

- base chain presence/absence и nearest matching pair;
- root settings без IncludeBase, pair settings только с IncludeBase;
- Construct replacement, Members merge/override/Ignore и Convert replacement;
- mixed overload forms Members в base/derived;
- multiple inheritance levels, Default clearing и call order;
- generic/nested mapper declarations и inaccessible base plans;
- no-effect/invalid explicit settings как model states.

Результат этапа: composition имеет один детерминированный путь и не зависит от
application registry либо неявного поиска fragments.

## Фаза 5. Надёжность, миграция и интеграция core v0

### Этап 20. Actualization нового generated surface и mapper-а

Статус: не начат.

Цель — доказать корректное появление, обновление и исчезновение всех artifacts
нового дизайна.

Production scope:

- при необходимости исправить semantic dependencies pipelines, но не менять
  публичную семантику предыдущих этапов;
- актуализировать construction/member plans, fluent extensions, generated
  mapper contracts и runtime descriptors;
- корректно реагировать на изменения source/destination/mapper, settings,
  constructors, members, attributes, docs, constraints и references;
- удалять artifact, когда исчезает последняя причина его генерации;
- сохранять stable hint names и content identity при нерелевантных изменениях.

Тестовый scope:

- отдельные полноценные Actualization categories для каждого artifact kind и
  executable mapper model;
- add/change/remove lifecycle;
- same destination через несколько pairs/mappers;
- capability transitions structured <-> direct, members appear/disappear и
  registry candidate count changes;
- referenced assembly changes.

Результат этапа: generated output всегда соответствует текущей compilation, а
не историческому состоянию incremental pipeline.

### Этап 21. Incrementality и cache isolation

Статус: не начат.

Цель — обеспечить точечную инвалидизацию без глобальной перестройки всех
mappings и plans.

Production scope:

- ввести/уточнить value models и comparers для каждого incremental boundary;
- не хранить syntax/symbol/compilation objects после semantic projection, где
  они не нужны;
- rebuild только затронутой pair, destination plan, mapper или registry
  manifest;
- изменения method body, не участвующего в transferable code, не инвалидируют
  unrelated artifacts;
- global coordination выполняется только там, где действительно требуется
  registry либо hint collision resolution;
- output content equality предотвращает лишнюю emission.

Тестовый scope:

- caching, dependency isolation, per-pair/per-destination invalidation и
  global coordination;
- unrelated syntax, using, docs, attributes, references и settings changes;
- multiple mappers/assemblies and descriptor manifests;
- unchanged output identity после эквивалентной edit sequence.

Результат этапа: новый дизайн сохраняет incremental свойства source generator-а
при реальном проектном масштабе.

### Этап 22. Финальный migration audit, документация и integration slice

Статус: не начат.

Цель — завершить основную реализацию core v0 до отдельной работы над
diagnostics и observable failures.

Production scope:

- проверить, что удалённые adapters, dead code и tests прежнего `Template()`
  pipeline не были возвращены последующими этапами;
- проверить отсутствие публичных `Template`, `TemplateMode`,
  `IContextualMapper`, `MemberMatching`, `ConstructorMember`, старых enum
  values и `NullabilityMismatchValidation`;
- проверить полный generated artifact naming и отсутствие лишних files;
- собрать real consumer integration project из package-like references;
- проверить DI registration, application registry, multiple assemblies,
  generated mapper activation, root/manual/declarative nested calls,
  nullable/generic destinations и mapper dependencies;
- обновить README, quick start, conceptual docs для `Construct`, `Members`,
  `Convert`, `Option`, runtime dispatch и все settings pages;
- явно документировать declarative dependency graph, authoritative returned
  result, identity/replacement и v0 non-goals;
- сопоставить реализованные scenarios с разделом 13
  `MAPPING_API_DESIGN.md` и core-v0 строками финального аудита.

Тестовый scope:

- focused integration suite поверх собранного runtime + analyzer package;
- C# 9 consumer и newer-language consumer;
- one/multiple assembly registries, generic closed descriptors и scoped
  dependencies;
- happy-path examples из документации компилируются и выполняются;
- публичная API baseline не содержит legacy surface.

Результат этапа: основная реализация нового дизайна завершена и готова к
пользовательскому ревью как единый core v0; остаются две специально отложенные
категории качества ниже.

## Фаза 6. Поздние отдельные планы

### Этап 23. Diagnostics

Статус: не начат.

Не детализируется в этом roadmap. После принятия этапа 22 для compile-time
diagnostics будет составлен отдельный план на основе нормативного раздела об
ошибочных и конфликтующих конфигурациях. До согласования того плана работа над
diagnostic IDs, сообщениями, locations, severity и recovery не начинается.

### Этап 24. Observable failures

Статус: не начат.

Не детализируется в этом roadmap. После готовности основной реализации для
runtime exception types, messages и остальных observable failure contracts
будет составлен отдельный план. До этого provisional failures не считаются
публично зафиксированным контрактом и не должны влиять на выбор mapping
algorithm.

## За границей текущего плана

Следующие возможности сохраняются как post-v0 roadmap commitments либо
направления аудита, но требуют отдельного проектирования и собственных
implementation plans:

- collections, dictionaries, buffers, getter-only collections, clear/fill,
  replacement, key reconciliation и element-path flattening;
- `IncludeMembers` и convention flattening;
- patch/merge presence policy для absent/value/explicit-null/default;
- automatic immutable Update reconstruction;
- tuple roots, multi-source mapping и strongly typed per-call state;
- keyed mapping variants;
- runtime polymorphism через explicit derived links;
- reference tracking, shared identity и cycles;
- `IQueryable` projection с reuse declarative pair plan и без client-side
  fallback;
- open-generic и runtime-type lookup;
- cross-assembly configuration composition и reusable fragments;
- hooks/middleware и result post-processing;
- identity-preserving declarative update get-only complex child;
- first-class enum mapping;
- opt-in scalable name matching;
- automatic reverse mapping, async/I/O mapping и остальные предложенные
  non-goals — только после отдельного продуктового решения.

Ни один из этих пунктов не становится неявным продолжением этапа 24.
Следующий post-v0 roadmap выбирается и согласуется отдельно после завершения
core v0.
