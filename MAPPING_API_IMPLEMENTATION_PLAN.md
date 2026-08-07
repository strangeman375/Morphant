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

Прежний `Template()`-дизайн не является compatibility target. Его obsolete
production-код, tests, implementation plan и excluded reference snapshot
удалены из активного дерева. Историческим источником остаётся Git; удачные
решения из прежнего дизайна переносятся только осознанно и с новой
спецификацией, без параллельного обслуживания старого surface.

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
До позднего этапа diagnostics ошибочная конфигурация сохраняет C#-legal
generated contract и получает typed exception-stub, но не должна молча менять
выбранную семантику. Структурно невозможный contract не генерируется и не
подавляет независимые legal pairs того же mapper-а.

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

Runtime-сценарии generated mapper-а являются интеграционными по своей природе.
Они определяются обычным C#-кодом в реальных C# 9, C# 11 и latest consumer
assemblies, подключающих generator через package-like analyzer reference.
MSBuild компилирует source вместе с generated mapper-ом, после чего test host
напрямую вызывает уже скомпилированный scenario: mapper создаётся обычным
конструктором, приводится к точному `ITypeMapper<,>` и получает прямой вызов
`Create` / `Update`. Runtime `CSharpCompilation`, `GeneratorDriver`, emit/load и
reflection invocation в integration slice не используются.

Полный exact-source production-composition и reflection inventory публичного
API остаются focused unit-спецификациями. Unit-test helpers ограничены
exact-source, compiler и focused model-проверками; general user-scenario
runtime execution туда не возвращается. Test-owned actualization harness может
выполнить конкретный step только для доказательства свежей semantics при
сохранённом `GeneratorDriver`; это часть incremental concern, а не integration
coverage.

Публичные XML comments и пользовательская документация обновляются вместе с
тем этапом, который вводит или меняет соответствующий контракт. Актуальная
документация использует только текущий API; прежние имена допустимы лишь в
явно обозначенном историческом контексте.

## Граница текущего roadmap

Этот план доводит до готовности согласованный core v0:

- universal `IMapper.Map` facade и точные
  `ITypeMapper.Create` / `ITypeMapper.Update` operations;
- declarative pipeline `Construct` + `Members`;
- полностью ручной `Convert`;
- application-wide exact-pair dispatch поверх вручную зарегистрированных
  mapping services;
- root и scoped `IMapper`, `MappingContext` и `MappingScope`;
- adaptive nested `Map` и explicit nested `Create` / `Update`;
- settings и явная композиция через mapper inheritance;
- generated surface, actualization, incrementality и интеграционный сценарий.

Диагностики и observable failures вынесены в отдельные поздние этапы.
Observable failures детализированы и приняты этапом 24. Diagnostics ведутся в
отдельном [`DIAGNOSTICS_PLAN.md`](DIAGNOSTICS_PLAN.md); его таксономия является
текущим следующим срезом.

Collections, projection и остальные post-v0 возможности в текущую реализацию
не входят. Они перечислены в конце документа только для сохранения границы и
не являются следующими этапами этого roadmap.

## Следующий этап

**Фаза 6, этап 23 — diagnostics, этап 2: каталог, категория 1.**

Статус: ожидает ревью.

Этап 17 принят. Этап 18 по решению от 6 августа 2026 года перенесён за границу
core v0. Этапы 19–22 и 24 приняты. Для этапа 23 составлен отдельный
[`DIAGNOSTICS_PLAN.md`](DIAGNOSTICS_PLAN.md). Его этап 1 с 12 категориями и
общими границами принят. В этапе 2 полностью специфицирована категория 1:
`MORPH0001`–`MORPH0004`, compatibility revision, global generation gate,
precedence, suppression и самостоятельная тестовая матрица. Категория 1
ожидает ревью; категории 2–12 заблокированы её принятием.

## Фаза 1. Публичный фундамент и generated surface

### Этап 1. Публичный контракт и граница миграции

Статус: принят.

Цель — перевести repository на согласованный словарь нового API и создать
компилируемый фундамент, не пытаясь в том же срезе реализовать весь DSL.

Production scope:

- изменить `IMapper` и `ITypeMapper<TSource, TDestination>` на целевой
  nullable-input / non-nullable-return contract;
- сохранить universal entry point как две перегрузки `IMapper.Map`, а
  операции exact-pair contract назвать `ITypeMapper.Create` и
  `ITypeMapper.Update` без legacy-алиасов `Map`;
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
  `ByConvention`, `ByFactory` и marker hierarchy для будущих adaptive
  `Map` / explicit `Create` / explicit `Update`; не вводить
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
- хранить local root/map settings и подготовить места для typed
  `IncludeBase`;
- разрешать несколько registrations одной canonical pair в разных mapper-ах;
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
этапах 7–22. Runtime-сценарии этапа перенесены в dedicated integration
project на этапе 22; exact-source проверки остаются в unit-test project.

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
`__Create` helper внутри `Update` без зависимости от public `Create`. Helper
генерируется и для `MappingMode.Update`, когда public `Create` отключён.
Declarative source нормализуется единым policy для generated surface и
executable mapper-а;
`Nullable<TSource>` разворачивается в underlying `TSource`, а для definitely
non-nullable values проверки не генерируются. Invalid effective settings
сохраняются как детерминированные unsupported operations до diagnostics.
Public `Map` methods выполняют только settings/null prelude и dispatch:
достижимая no-previous ветка исполняется в collision-safe `__Create`, а
existing-destination ветка — в collision-safe `__Update`. Helpers получают
нормализованный source и исходный `MappingContext`; `__Update` также получает
non-null параметр `destination`, поскольку это фактический destination для
обновления, а не отдельный снимок предыдущего состояния.
Самостоятельные `TypeMapperMappingModeTests` и `TypeMapperNullHandlingTests`
проверяют полный generated source, runtime laws, precedence, call order,
nullable forms и invalid states. Их runtime-сценарии перенесены в dedicated
integration project на этапе 22.

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

Previous-aware tree строится отдельно для `__Create` и `__Update`:
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
class/struct/record/nullable/generic destinations. Runtime-сценарии перенесены
в dedicated integration project на этапе 22; самостоятельные exact-source
проверки остаются в unit-test project.

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
переиспользуется всеми reachable `__Create` / `__Update` branches. Это
относится как к lambda body, так и к materialized method-group/delegate:
типизированный delegate local и invocation находятся внутри общего helper-а,
а operation-specific код передаёт только фактически captured source/previous.
Direct block и direct method-group/delegate используют тот же mapper-level
закон; одинаковый callable больше не объявляется заново внутри leaf-ветвей.

Полная production-композиция также закрепляет границу replacement lowering:
`MapNewFactory` / `MapNewConstructor` разрешены в `__Update` только внутри
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
отмеченный executable slice, перенесённый в dedicated integration project на
этапе 22; exact-source проверки остаются в unit-test project.

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
(`8/8`). Runtime-сценарии перенесены в dedicated integration project на этапе
22; exact-source проверки остаются в unit-test project.

### Этап 11. Previous/result-aware members и lifecycle границы

Статус: принят.

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
- статически пустая existing-ветка является допустимым no-op и возвращает
  исходный destination без требования previous-aware `Construct`;
- допустимый no-op не включает скрытый replacement: source-only `Construct`
  на existing-ветке по-прежнему не выполняется.

Тестовый scope:

- previous отличается от replacement result;
- result state, созданный constructor/factory/direct code;
- mixed result-dependent и result-independent rules в одной lambda;
- init/required/set/field across all reachable creation branches;
- unreachable expressions, side-effect counts и null short-circuit;
- immutable Update: convention/source-only no-op, explicit `Ignore`,
  неприменимый на reuse-ветке `init`, explicit reuse и replacement.

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

Статически пустой immutable `Update` является корректным no-op: existing-
ветка возвращает тот же destination, не выполняя source-only `Construct` и
не вычисляя неприменимые creation-time member expressions. Previous-aware
`Construct` нужен только тогда, когда configuration действительно выбирает
reuse либо replacement; доступность `Update` сама по себе не обещает mutation.

Самостоятельная категория `TypeMapperMemberTests` дополнена lifecycle,
immutable и result-aware сценариями, включая полный exact-source snapshot,
no-op identity, отсутствие source-only и creation-time side effects,
previous/replacement identity, constructor/factory/direct state, mixed
dependencies, `init`/`required`/setter/field и terminal null (`14/14`).
Regression-срезы этапов 9 и 8, conventions, null handling и mapping mode
сохраняют прежнее поведение. Runtime-сценарии перенесены в dedicated
integration project на этапе 22; exact-source проверки остаются в unit-test
project.

### Этап 12. Declarative control flow и member-plan composition

Статус: принят.

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

Реализовано: structured `Construct` и обе формы `Members` используют общий
конечный declarative control-flow planner. Он поддерживает initialized/const
locals, nested blocks, complete `if` и statement `switch`, conditional и
switch expressions на уровнях whole plan, strategy, rule и marker, несколько
`return`, явный `throw`, pattern variables и `DestinationMembers` `with`
overlays. Не выбранные paths не вычисляются, а overridden rules и ставшие
ненужными dependencies удаляются до lowering.

Lowering остаётся operation- и lifecycle-aware: source/previous/result,
declarative locals и pattern variables получают collision-safe substitutions;
result-dependent member control выполняется после появления фактического
constructor/direct/factory/reuse/replacement result; terminal null завершает
ветку до member control. Direct `Construct` и `ByFactory` bodies остаются
непрозрачным C#, а общий direct helper не дублируется между declarative member
leaves. Cross-plan sharing одинаковых expressions намеренно не добавлено и
реализовано отдельно на этапе 13.

Compile-time Configure constants и доступные mapper/static captures
сохраняются. Runtime Configure locals, mutation-oriented statements и прочая
grammar за согласованной boundary переводятся в deterministic invalid model
state без попытки исполнения как imperative DSL. Неполный switch expression
сохраняет обычный runtime fallback C#.

Самостоятельная категория `TypeMapperDeclarativeControlFlowTests` содержит
runtime и полный exact-source срез всех пяти generated files: locals/if,
statement и expression switch, plan/rule/marker branches, patterns/guards,
throws, `with` overlays, result-aware lifecycle, structured/direct/factory
construction, captures и неподдерживаемую grammar (`18/18`). Regression-срезы
этапов 11, 9 и 8 сохраняют прежнее construction/member поведение. Runtime-
сценарии перенесены в dedicated integration project на этапе 22; exact-source
проверки остаются в unit-test project.

### Этап 13. Общий dependency graph и observable evaluation laws

Статус: принят.

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

Реализовано: structured construction и effective member plan после
specialization, branch selection и `with` overlays получают общий
path-sensitive dependency graph. Identity узла строится из bound Roslyn
operation: учитываются symbols и overloads, receiver, ordered arguments,
constants, types и nested operations; прозрачные parentheses и implicit target
conversions не разрывают identity underlying value. Один выбранный path
материализует общий узел в collision-safe typed local, а остальные paths его не
вычисляют.

Declarative locals сохраняют как symbol dependency, так и тип фактического
storage после conversion. Поэтому общий underlying value разделяется до разных
target conversions без недопустимого обратного преобразования. При выносе
nested dependencies explicit constructor arguments по-прежнему вычисляются в
порядке записи. Result-dependent nodes появляются только после создания
фактического result; replacement/reuse и aliasing не создают отдельной
evaluation semantics.

Имя synthetic local описывает сохранённое значение, а не внутреннюю причину
его materialization. Простые member paths получают имя из полного пути
(`source.Customer.Id` → `sourceCustomerId`), произвольные вычисления используют
нейтральный fallback `value`. Collision suffixes распределяются отдельно в
области каждого generated method, поэтому имена из `__Create` не влияют на
имена в `__Update`.

Direct `Construct`, factory body и `Convert` не поставляют узлы в общий graph.
Обычные guarantees о порядке независимых member expressions, generated
assignments и setter side effects не расширены.

Самостоятельная категория `TypeMapperDependencyGraphTests` содержит runtime и
полный exact-source срез всех пяти generated files: constructor/member и
duplicate-member sharing, nested operations, parentheses, target conversions,
declarative locals, result-dependent values, selected branches, overridden
rules, разные symbols/receivers/overloads, explicit constructor order,
direct/factory opacity, observable target conversions, pattern-variable name
collisions, nullable/parenthesis wrappers и aliasing (`13/13`).
Exact-source regression-срезы этапов 12, 11 и 8 обновлены только на новые
dependency/order-preserving locals; их runtime semantics и остальные generated
files не изменились.

## Фаза 3. Runtime dispatch, manual mapping и nested mappings

### Этап 14. Application dispatch, root mapper и mapping scope

Статус: принят.

Цель — реализовать application-wide exact-pair dispatch без runtime reflection
для поиска mappings.

5 августа 2026 года внешний composition-root / DI registration API явно
отложен до после v0. В core v0 нет `AddMorphant(...)`, generated manifests,
assembly attributes для регистрации или автоматического assembly scanning.
Пользователь вручную регистрирует closed
`ITypeMapper<TSource, TDestination>` services; стандартная
`IEnumerable<ITypeMapper<TSource, TDestination>>` текущего
`IServiceProvider` является множеством кандидатов exact pair. Это low-level
workaround, а не окончательный convenience API.

Production scope:

- root `Mapper` с текущим `IServiceProvider` и ручной provider-registration
  каждой closed `ITypeMapper<TSource, TDestination>` pair;
- application-wide lookup запрашивает только точную
  `IEnumerable<ITypeMapper<TSource, TDestination>>`, без runtime reflection,
  mapper/assembly filtering или hidden fallback;
- lookup law `0 / 1 / 2+` без first/last-registration-wins;
- несколько registrations одной canonical pair не являются compile-time
  duplicate сами по себе;
- `MappingMode` остаётся capability выбранного mapper-а, а не частью key;
- каждый root `Map` создаёт новый `MappingScope` и завершает его в `finally`;
- scoped `IMapper` использует тот же provider/scope, но новый
  immutable `MappingContext` frame;
- source-only overload создаёт `Create` frame, two-argument — `Update` frame,
  включая explicit `null` destination;
- source-only facade dispatch вызывает `ITypeMapper.Create`, а facade с
  destination — `ITypeMapper.Update`;
- последовательная recursion/reentrancy и exception isolation;
- независимые root scopes допускают parallel calls; parallel nested use одного
  scope не получает guarantee;
- на момент этапа 14 внутренние failures оставались provisional до отдельного
  observable-failures этапа; lookup result при этом не мог меняться. Этап 24
  закрепил окончательные публичные типы без изменения lookup law.

Тестовый scope:

- zero, one и multiple manual registrations/candidates exact pair;
- provider activation и scoped/transient mapper dependencies;
- root/nested frame operations, shared scope и provider identity;
- recursion, reentrancy, caught nested exception и scope completion;
- canonical nullable/generic pair lookup;
- отсутствие зависимости от mapper type, assembly и registration order.

Результат этапа: `IMapper` становится рабочей application-wide facade и
создаёт корректный runtime lifecycle для последующих nested calls. Отдельная
самостоятельная категория `MapperRuntimeTests` закрепляет manual lookup,
operations, nested frames, completion, recursion/reentrancy, exception
isolation, parallel root scopes, transient activation и type identity
(`10/10`).

### Этап 15. Полностью ручной `Convert`

Статус: принят.

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

`Convert` переносится в один collision-safe private helper и вызывается обеими
generated operations с исходным source, точным `Option<TDestination>` и
неизменённым `MappingContext`. Helper сохраняет expression/static/block lambda,
обычные C# statements, mapper members и локальные объявления, но не допускает
runtime captures из `Configure`. Declarative marker-вызовы внутри manual body,
повторный `Convert`, смешанный manual/declarative plan и неприменимые explicit
map-level settings остаются deterministic invalid state до diagnostics.

Emitter обходит source/destination null handling и весь declarative lowering
до любой normalization. `MappingMode` проверяется до manual helper; nullable
reference/value destination превращается в `Option.None` либо
`Option.Some(non-null value)` непосредственно из фактического argument-а.
Returned `null`, previous instance и replacement возвращаются без guard или
post-processing.

Самостоятельная категория `TypeMapperConvertTests` содержит runtime и полный
exact-source срез: call-state matrix, inherited no-effect settings,
`MappingMode`, expression/static/block bodies, constructors/factories,
mutation, loops, exceptions, local functions, record `with`, nested scoped
dispatch, nullable value pairs, captures/conflicts/markers, отсутствие
conventions и collision-safe helper (`8/8`).

### Этап 16. Declarative nested mapping

Статус: принят.

Цель — дать common-case nested mapping короткое adaptive API, сохранив явный
выбор Create/Update для специальных случаев.

Production scope:

- adaptive forms `Map()`, `Map<TDestination>()`, `Map(source)` и
  `Map<TDestination>(source)` во всех target-typed member/constructor
  позициях, declarative control flow и locals;
- parameterless forms выводят readable source-member из имени target-а;
- adaptive no-previous branch вызывает nested Create, existing outer Update —
  nested Update с текущим member-ом фактического `result` либо соответствующим
  readable member-ом outer `previous` для constructor parameter;
- explicit forms `Create(source)`, `Create<TDestination>(source)`,
  `Update(source, destination)` и
  `Update<TDestination>(source, destination)` всегда сохраняют выбранную
  nested operation;
- static nested source выводится из source-expression, destination — из target
  либо explicit generic argument; generic result обязан warning-free
  преобразовываться в target type;
- adaptive generic Update проверяет runtime-совместимость текущего destination;
  incompatible non-null value приводит к
  `NestedDestinationTypeMismatchException`, а не к скрытому Create;
- true get-only destination properties, properties с недоступным обычным
  setter-ом и доступные `readonly` fields входят в `DestinationMembers` как
  get-only markers и допускают standalone
  `Update(source, members.Member)`; direct `init`-only остаётся creation-only и
  proxy не получает;
- read-only target читается один раз; при `null` nested call и source-expression
  пропускаются, при non-null выполняется Update с discard returned replacement;
- `previous` и `result` являются read-only inputs: assignment, increment,
  decrement и `ref`/`out` mutation запрещены declarative plan-ом;
- arguments вычисляются один раз слева направо; dispatch использует тот же
  provider/scope и новый call frame;
- convention `Auto()` по-прежнему никогда не превращается в implicit nested
  mapping.

Тестовый scope:

- все восемь forms в constructor и member targets;
- adaptive outer Create/Update matrix, normalized null destination,
  replacement result и constructor previous-member association;
- source/target inference, typed и untyped locals, interfaces, conversions и
  incompatible runtime destination;
- explicit null, nullable sources и destination values;
- read-only property/field non-null/null paths, single evaluation и discarded
  replacement;
- ambiguous adaptive local reuse и mutation `previous`/`result`;
- argument evaluation order, graph sharing, exception propagation и
  application-wide lookup вне outer mapper/assembly.

Результат этапа: основной nested rule следует фактической outer lifecycle
ветке, а принудительные Create/Update и in-place read-only update выражаются
явно без ручной мутации outer destination.

Реализовано: marker-вызовы семантически связываются с конечной target-позицией
и понижаются в scoped `context.Mapper.Map<TSource, TDestination>`. Adaptive
locals остаются aliases и получают source/destination context от конечного
member-а либо constructor parameter-а; reuse одного вызова для разных current
destinations в Update детерминированно unsupported. Parameterless source
выводится из readable source surface, constructor parameter предварительно
связывается с readable destination-member.

Writable member в existing Update использует member фактического `result`,
включая replacement. Constructor parameter использует member outer previous.
Explicit Update не меняет operation для `null`; adaptive no-previous ветка
выполняет Create. Generic adaptive Update генерирует проверяемое runtime
приведение.

Readable non-writable properties и доступные `readonly` fields добавлены в
generated member surface без setter-а. Standalone Update через такой marker
генерирует null guard до source, однократное чтение member-а и discard nested
result. Эти markers не участвуют в conventions, `Auto()` и unmapped
validation. Declarative input mutation отсекается до lowering.

Самостоятельная категория `TypeMapperNestedMapTests` содержит runtime и полный
exact-source срез перечисленного контракта; `MemberSurfaceTests` фиксирует
generated read-only marker shape.

## Фаза 4. Settings и configuration composition

### Этап 17. Полный `ConstructorSelection`

Статус: принят.

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

Реализовано: все семь значений разрешаются на mapping-, mapper-, assembly- и
library-level. Shape-стратегии выбирают constructor до проверки применимости и
не делают fallback; `Greediest` сравнивает число реально emitted arguments
warning-free применимых plans, а `Largest` — declared parameter count.
Optional/`params`, ties, required initialization, `SetsRequiredMembers`,
`ByConvention` rules и explicit `Construct` следуют описанному контракту.
Inherited setting остаётся no-op для direct/`Convert`, explicit map-level use
сохраняет invalid state до диагностик. Самостоятельная категория
`TypeMapperConstructorSelectionTests` фиксирует runtime, exact-source,
configuration и capability matrix.

### Этап 18. Automatic boxing policy

Статус: не начат; отложен до post-v0 по решению от 6 августа 2026 года.

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

### Этап 19. Mapper inheritance, typed `IncludeBase` и settings composition

Статус: принят.

Цель — реализовать единственную v0-модель переиспользования configuration.

Production scope:

- распознавать явный `base.Configure(builder)` и подключать chain base
  mapper-ов;
- без этого вызова base configuration и roots не участвуют;
- local pair без typed `IncludeBase` начинает с чистого pair plan, но наследует
  подключённые root settings;
- `base.Configure(builder)` не переносит base registrations в generated
  surface derived mapper-а, а только подключает root settings и делает base
  pair configurations кандидатами для `IncludeBase`;
- `IncludeBase<TBaseSource, TBaseDestination>()` выбирает явно названную exact
  pair сначала на текущем mapper-level независимо от порядка объявлений, затем
  в подключённой base chain от ближайшего уровня к дальнему;
- current source/destination должны быть assignable к указанным base
  source/destination; при нескольких registrations указанной pair выбирается
  ближайший base mapper level;
- импортируются все map-level settings, включая `MappingMode` и
  `ConstructorSelection`;
- exact precedence: current pair -> included base pair -> current root ->
  connected base roots nearest-first -> assembly -> library default;
- last-call-wins на каждом C# level, включая `Default`;
- `Construct` и `Convert` base pair не импортируются;
- imported/local `Members` объединяются по destination member, local rule,
  `Auto` и `Ignore` перекрывают imported rule, а conventions вычисляются
  заново для current pair и заполняют остаток;
- dependencies строятся заново только для effective rules;
- local `Convert` заменяет imported member plan и остаётся полной manual
  реализацией current pair;
- `UnmappedMemberValidation` получает полную effective setting model, но её
  diagnostic enforcement остаётся позднему diagnostics-этапу;
- general fragments, arbitrary builder helpers и cross-assembly
  `IncludeBase` не добавляются.

Тестовый scope:

- отсутствие inherited-only registrations, base chain presence/absence,
  same-level order independence, current-level precedence, exact nearest base
  pair, self-reference и source/destination assignability;
- root settings без IncludeBase, все pair settings только с typed IncludeBase;
- отсутствие импорта `Construct`/`Convert`, Members merge/override/Auto/Ignore
  и local Convert replacement;
- mixed overload forms Members в base/derived;
- multiple inheritance levels, Default clearing и call order;
- generic/nested mapper declarations, nested mappings внутри imported rules и
  accessibility только effective member expressions;
- no-effect/invalid explicit settings как model states.

Результат этапа: composition имеет один детерминированный путь и не зависит от
application dispatch либо неявного поиска fragments.

Реализовано: source-visible mapper hierarchy подключается только прямым
`base.Configure(builder)`, включая expression-bodied overrides, generic base
mapper-ы и nested mapper declarations. Base mapper не требует
`MorphantMapperAttribute`; для закрытого generic наследника дополнительно
эмитируется открытый fluent surface, необходимый исходному base DSL.
Inherited-only pairs не переносятся в generated mapper; повторная local pair
начинает с чистого plan. Typed `IncludeBase` хранит exact base pair после
generic substitution, сначала ищет её на текущем level независимо от порядка
объявлений, затем в connected base levels и проверяет assignability обоих
типов. Same-level self-reference сохраняется как unsupported state. Проверка
остаётся generator-side: C# не позволяет ограничить содержащие `TSource` и
`TDestination` в `where` метода без изменения двухаргументной формы API.

Settings разрешаются в полном порядке current pair -> included base pairs ->
current root -> connected base roots -> assembly -> library; last-call-wins и
`Default` работают независимо для всех slices, включая
`UnmappedMemberValidation`, `MappingMode` и `ConstructorSelection`.
`Construct`/`Convert` не импортируются; `Members` объединяются по destination
member с локальным приоритетом, повторным применением conventions и
перестроением dependency graph. Перекрытая inaccessible rule удаляется до
emission; effective inaccessible rule, несовместимые типы, отсутствующая base
pair/chain и повторные composition calls сохраняются как детерминированные
unsupported states до diagnostics.

Самостоятельная категория `TypeMapperInheritanceTests` фиксирует runtime,
полную cross-pair configuration model, C# 9 generic/accessibility boundaries,
transitive composition и exact production snapshot всех generated artifacts.
XML comments, settings pages и отдельное руководство по configuration
inheritance актуализированы.

## Фаза 5. Надёжность, миграция и интеграция core v0

### Этап 20. Actualization нового generated surface и mapper-а

Статус: принят.

Цель — доказать корректное появление, обновление и исчезновение всех artifacts
нового дизайна.

Production scope:

- при необходимости исправить semantic dependencies pipelines, но не менять
  публичную семантику предыдущих этапов;
- актуализировать construction/member plans, fluent extensions, generated
  mapper contracts;
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
  registration-compatible contract changes;
- referenced assembly changes.

Результат этапа: generated output всегда соответствует текущей compilation, а
не историческому состоянию incremental pipeline.

Реализовано: test-owned actualization harness сохраняет один
`GeneratorDriver` на протяжении последовательности edits, заменяет syntax
trees и references и на каждом шаге проверяет полный набор hint names, точный
CRLF-content, compiler warnings/errors и при необходимости исполняет generated
mapper. Самостоятельные construction/member/type-mapper categories фиксируют
add/change/remove/restore lifecycle, последнюю причину генерации, несколько
pairs/mappers для одного destination, structured/direct и member-capability
transitions, constructors, members, documentation, attributes, settings,
generic constraints и стабильный output при нерелевантном source-file edit.
Отдельные последовательности заменяют сборку с тем же identity и доказывают
актуализацию construction plan, member plan и mapper implementation с
последующим восстановлением исходной версии. Production pipelines уже имели
достаточные semantic dependencies; изменений публичной или внутренней
production-семантики не потребовалось.

### Этап 21. Incrementality и cache isolation

Статус: принят.

Цель — обеспечить точечную инвалидизацию без глобальной перестройки всех
mappings и plans.

Production scope:

- ввести/уточнить value models и comparers для каждого incremental boundary;
- не хранить syntax/symbol/compilation objects после semantic projection, где
  они не нужны;
- rebuild только затронутой pair, destination plan или mapper;
- изменения method body, не участвующего в transferable code, не инвалидируют
  unrelated artifacts;
- global coordination выполняется только там, где действительно требуется
  hint collision resolution;
- output content equality предотвращает лишнюю emission.

Тестовый scope:

- caching, dependency isolation, per-pair/per-destination invalidation и
  global coordination;
- unrelated syntax, using, docs, attributes, references и settings changes;
- multiple mappers/assemblies;
- unchanged output identity после эквивалентной edit sequence.

Результат этапа: новый дизайн сохраняет incremental свойства source generator-а
при реальном проектном масштабе.

Реализовано: surface pipeline разделён на independently tracked semantic model
и emission boundaries для construction plan, member plan, pair extensions и
generated mapper. Canonical pairs остаются привязаны к стабильным
per-mapper/per-pair кандидатам; destination plans выбирают стабильного
владельца без позиционных сдвигов соседних outputs. Глобальная координация
содержит только canonical/destination ownership и реальные case-insensitive
hint-name collisions, а обычные readable hint names не попадают в global
allocation state.

Для destination models введены contract dependencies по исходным declarations
и metadata references, включая containing/base types, semantic context,
parse/compilation options и assembly identity. После semantic projection
construction/member/extension models и mapper source сравниваются по чистому
value-content; неизменившийся content не доходит до повторной emission.
Test-owned harness сохраняет один tracked `GeneratorDriver` между edits и
проверяет `New` / `Modified` / `Unchanged` / `Cached` / `Removed` для точных
hint names. Самостоятельные категории покрывают caching, unrelated syntax,
method bodies, using, docs, attributes, per-destination и per-mapper
invalidation, add/remove и last-reason lifecycle, settings, reference
replacement, multiple mappers/assemblies, collision coordination и возврат к
эквивалентному состоянию.

### Этап 22. Финальный migration audit, документация и integration slice

Статус: принят.

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
- проверить явную регистрацию через стандартный DI container, multiple
  assemblies, generated mapper
  activation, root/manual/declarative nested calls,
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
- one/multiple assembly registrations, generic closed mappings и scoped
  dependencies;
- happy-path examples из документации компилируются и выполняются;
- публичная API baseline не содержит legacy surface.

Результат этапа: основная реализация нового дизайна завершена и готова к
пользовательскому ревью как единый core v0; остаются две специально отложенные
категории качества ниже.

Реализовано: migration audit подтвердил отсутствие legacy public surface,
obsolete adapters и возвращённых `Template()` tests; exact public API baseline
фиксирует все exported/protected core-v0 members и enum values. Exact
production composition проверяет полный набор и имена пяти generated artifacts
без дополнительных compatibility files. Оба reflection/Roslyn-based exact
контракта находятся в unit-test project и не смешиваются с runtime integration.

122 исходные executable scenario groups развёрнуты в 130 обычных compile-time
scenario files: 121 компилируется отдельным C# 9 consumer assembly, 9 — C# 11
consumer assembly. Latest consumer сохраняет documentation/multiple-assembly
slice. Все consumer projects используют analyzer-style project references;
integration host напрямую вызывает их public `Scenario.Verify()`, внутри
которых generated mapper создаётся и вызывается через точный `ITypeMapper<,>`.
`ProductionGeneratorIntegrationTest`, runtime Roslyn packages, dynamic emit,
`Assembly.Load` и reflection invocation удалены из integration project.

Consumer assemblies не ссылаются друг на друга: только агрегирующий test host
видит C# 9, C# 11 и latest projects одновременно. Каждый scenario владеет
своими DTO, mapper-ами и domain fixtures; небольшие типы между scenarios не
переиспользуются. Cross-assembly nested-lookup data находится в явно названной
папке существующего `Morphant.Generator.UnitTests.TestAssets`: эта assembly уже
создаёт необходимую границу, поэтому отдельный project для одного scenario не
используется. Runtime registration выполняется настоящим
`Microsoft.Extensions.DependencyInjection`, подключённым как точечный NuGet-
пакет без framework reference на `Microsoft.AspNetCore.App`; custom
`IServiceProvider` stub удалён, а multiple-assembly slice создаёт реальные DI
scopes и получает generated mapper-ы с их scoped constructor dependency из
контейнера.

Честная project compilation выявила и закрыла production-дефект внутреннего
nested-map conversion probe: при `TreatWarningsAsErrors=true` nullable warning,
повышенный настройкой project-а, ошибочно считался настоящим compiler error и
заменял допустимый plan на unsupported path. Probe теперь различает исходную
severity и по-прежнему отвергает реальные compiler errors.

Consumer slice проверяет documentation quick start, one/multiple assembly
manual registrations, generated mapper activation, root, manual и declarative
nested dispatch, closed generic и nullable destinations, mapper dependency из
текущего service scope и все перенесённые executable categories.

README превращён в рабочую entry point документации. Добавлены quick start,
declarative lifecycle/`Option`, runtime dispatch/DI, generated artifacts и
core-v0 boundary; обновлены manual/nested/inheritance guides и все settings
pages. Dependency graph, authoritative return, reuse/replacement, identity и
non-goals описаны явно. Scenario matrix сопоставляет раздел 13 design document
и fundamental core-v0 строки финального аудита с реализованными путями.
Focused compile-time integration suite проходит `132/132`; три вынесенные
unit-спецификации production composition, inheritance composition и public API
baseline проходят `3/3`. Остальная документационная и migration-часть этапа
сохраняется без изменений.

Финальная repository cleanup удалила архивный roadmap и excluded snapshot
прежнего `Template()`-дизайна, завершённый refinement plan и stale внутренний
словарь `MapNew` / `MapExisting`. Активный generator теперь последовательно
использует `Create` / `Update`; исторические сравнения остаются только там, где
они объясняют нормативные решения текущего API.

## Фаза 6. Поздние отдельные планы

### Этап 23. Diagnostics

Статус: не начат; подготовительный этап 2, категория 1, ожидает ревью.

Работа ведётся по отдельному
[`DIAGNOSTICS_PLAN.md`](DIAGNOSTICS_PLAN.md). Сначала согласуется полная
таксономия v0, затем по одной категории составляется полный каталог с IDs,
сообщениями, locations, severity, suppression и recovery. Реализация и тесты
начинаются только после согласования каталога; завершает этап двусторонний
аудит плана и production-кода. Таксономия принята; категория окружения
полностью специфицирована и ожидает ревью.

### Этап 24. Observable failures

Статус: принят.

Цель — зафиксировать единый публичный контракт ошибок Morphant до введения
compile-time diagnostics и исключить пустые либо частично сгенерированные
C#-legal mapping contracts.

Согласованная граница:

- все продуктовые mapping failures, создаваемые самим Morphant, и все
  исключения из generated code имеют публичный typed exception в namespace
  `Morphant.Exceptions` и общий base `MorphantException`;
- обычная argument validation рукописного public API использует стандартные
  .NET exceptions;
- пользовательские exceptions из `Construct`, `Members`, `Convert`, source
  expressions, mapper dependencies и service provider не оборачиваются;
- если C# может объявить `ITypeMapper<TSource, TDestination>`, invalid либо
  unsupported mapping сохраняет interface и обе operations; недоступный path
  получает executable exception-stub;
- unsupported root не получает ложных construction/member/pair-extension
  surfaces;
- structurally impossible mapper shape, unnameable pair contract и generic
  interfaces, способные унифицироваться, остаются без executable stub до
  diagnostics; независимые legal pairs того же mapper-а генерируются.

Production scope:

- ввести `MappingConfigurationException`,
  `MappingOperationNotSupportedException`, `NullSourceException`,
  `NullDestinationException`, `MappingNotFoundException`,
  `AmbiguousMappingException`, `InvalidMappingRegistrationException`,
  `MappingScopeCompletedException`,
  `NestedDestinationTypeMismatchException`,
  `OptionValueMissingException` и `UnmatchedMappingSwitchException`;
- включить существующий `RuntimeInvocationNotSupportedException` в ту же
  hierarchy;
- заменить Morphant-authored standard exceptions в runtime и generated code,
  сохранив исходные user-authored throw expressions;
- выдавать полные mapping stubs для invalid settings/plans и C#-legal
  unsupported roots;
- исключать только конфликтующие generic pairs, а не весь mapper;
- сохранить lookup law `0 / 1 / 2+`, отдельно зафиксировать единственную
  registration, разрешившуюся в `null`, и завершённый mapping scope;
- обновить public XML, normative design, README и conceptual/settings docs.

Тестовый scope:

- reflection baseline всего публичного exception API и exact messages;
- exact generated source для invalid effective setting и unsupported root;
- production-composition проверка отсутствия ложных DSL artifacts у
  unsupported root;
- generic-unification scenario с независимой pair в том же mapper-е;
- real `Microsoft.Extensions.DependencyInjection` lookup tests для missing,
  ambiguous, null registration и completed scope;
- compiled C# 9 consumer для executable unsupported stub и independent generic
  contract;
- существующие compiled scenarios для operation gates, null policies,
  invalid plans, nested destination mismatch, unmatched switch и неизменённых
  user exceptions.

Реализовано: добавлена публичная hierarchy из общего `MorphantException` и
двенадцати конкретных типов. Generated code и продуктовые failure paths
runtime dispatch, scope и `Option<T>` не создают standard exceptions.
Обычная проверка аргументов рукописного API следует .NET conventions:
`Mapper` использует `ArgumentNullException` для отсутствующего service
provider. Generated mapper использует отдельные типы для invalid
configuration, отключённой operation, null policies, adaptive destination
mismatch и non-exhaustive declarative switch; user throw expressions остаются
без обёртки.

Pair pipeline разделяет supported, unsupported-but-nameable и structurally
unnameable contracts. C#-legal collection/tuple/array/delegate/deferred и
прочие запрещённые roots получают полный `ITypeMapper<,>` с двумя throwing
methods, но не получают construction/member/extensions. Generic unification
удаляет только конфликтующие pairs; независимые pairs продолжают генерироваться.
Invalid settings и plan conflicts сохраняют полный interface и сообщают
конкретную причину вместо общего `not supported yet`.

Добавлены самостоятельные exact-source/unit и compiled C# 9 integration
сценарии. Обновлены существующие runtime scenarios и snapshots для typed
failure contract. Focused verification проходит: 46 unit tests по exception,
runtime, exact-emission, actualization и затронутым mapper categories; 119
compiled integration tests по всем изменённым failure paths; отдельный C# 9 и
C# 11 consumer build проходит без warnings и errors. Полная suite по правилам
репозитория не запускалась.

## За границей текущего плана

Следующие возможности сохраняются как post-v0 roadmap commitments либо
направления аудита, но требуют отдельного проектирования и собственных
implementation plans:

- composition-root / DI convenience API (`AddMorphant(...)`), generated
  manifests и автоматическое подключение mappings из выбранных assemblies;
- оптимизация per-call allocations в runtime dispatch и mapping scope без
  изменения lookup/lifecycle semantics;
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
- opt-in policy, запрещающая automatic boxing в constructor/member
  candidates (отложенный этап 18);
- automatic reverse mapping, async/I/O mapping и остальные предложенные
  non-goals — только после отдельного продуктового решения.

Ни один из этих пунктов не становится неявным продолжением этапа 23.
Следующий post-v0 roadmap выбирается и согласуется отдельно после завершения
core v0.
