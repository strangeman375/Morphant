# План compile-time diagnostics Morphant v0

Дата составления: 7 августа 2026 года.

Последнее обновление: 8 августа 2026 года.

Статус: этап 2, категория 5, ожидает ревью.

Этот документ является отдельным рабочим планом этапа 23 из
[`MAPPING_API_IMPLEMENTATION_PLAN.md`](MAPPING_API_IMPLEMENTATION_PLAN.md).
Нормативную mapping-семантику задаёт
[`MAPPING_API_DESIGN.md`](MAPPING_API_DESIGN.md), а уже реализованную границу
runtime failures и recovery-stubs — раздел 14.2 того же документа и
[`docs/observable-failures.md`](docs/observable-failures.md).

План можно уточнять по мере детализации diagnostics. Изменение публичной
семантики, severity, diagnostic ownership, recovery либо границы v0 сначала
согласуется с пользователем, затем фиксируется здесь и в нормативном дизайне.

## 1. Цель и граница

Цель этапа 23 — сделать каждое ошибочное или неполное состояние core v0,
которое Morphant способен определить во время компиляции, явным,
детерминированным и локализованным diagnostic-ом.

Под «полным перечнем» понимается полнота относительно уже утверждённого core
v0. Diagnostics будущих collections, projection, runtime polymorphism,
reference handling, name normalization, automatic boxing,
`NullAssignmentHandling`, conditional reconstruction, cross-assembly
`IncludeBase` и других post-v0 возможностей в этот план не входят.

План не превращает runtime service lookup в compile-time анализ. Содержимое
application-wide `IServiceProvider` generator-у неизвестно, поэтому missing,
ambiguous и invalid registrations сохраняют утверждённые runtime exceptions.

## 2. Этапы работы

| Этап | Результат | Статус |
|---:|---|---|
| 1 | Полная таксономия категорий и общие границы diagnostics | Принят |
| 2 | Полный каталог и точный контракт каждой diagnostic по одной категории за раз | Категории 1–4 приняты; категория 5 ожидает ревью; категории 6–12 не начаты |
| 3 | Реализация, recovery, самостоятельные unit- и integration-тесты вертикальными срезами | Заблокирован этапом 2 |
| 4 | Двусторонний финальный аудит каталога, реализации, тестов и документации | Заблокирован этапом 3 |

Если при составлении каталога обнаружится пересечение, пропуск либо неверная
граница категории, работа возвращается к этапу 1. Изменение таксономии
согласуется до продолжения детализации.

## 3. Общие законы diagnostics

- Ошибочная либо неподдерживаемая конфигурация core v0 получает severity
  `Error`. Неполнота mapping-а, управляемая `UnmappedMemberValidation`, получает
  severity `Warning`.
- Diagnostic учитывает effective settings и достижимость операций. Проблема
  недостижимой `Create`- либо `Update`-ветки не должна блокировать другую
  корректную операцию.
- Одна первичная причина подавляет каскад зависимых diagnostics. При
  пересечении категорий diagnostic принадлежит самой ранней причине, после
  которой зависимый plan уже нельзя анализировать достоверно.
- Primary location указывает на наиболее конкретную пользовательскую
  конфигурацию, создавшую проблему. Для compilation-wide prerequisite без
  естественного source span точная location policy определяется на этапе 2.
- Повторяемые observations одной причины дедуплицируются в пределах
  согласованного diagnostic identity. Точная область дедупликации задаётся в
  контракте конкретной diagnostic.
- C#-legal mapping contract сохраняется по законам observable failures.
  Недоступные операции получают существующие typed recovery-stubs;
  structurally impossible contract не подделывается дополнительным generated
  surface.
- Morphant не дублирует точную и достаточную ошибку C# compiler-а. Собственная
  diagnostic добавляется только когда она сообщает Morphant-specific причину,
  затронутую operation либо способ исправления.
- Diagnostics детерминированы относительно исходного кода и compiler-visible
  settings; порядок discovery и incremental invalidation не меняют их набор и
  порядок.

## 4. Таксономия diagnostics v0

Таксономия содержит 12 категорий. На этом этапе она фиксирует ownership и
границы, но намеренно не задаёт отдельные diagnostic IDs, сообщения или
полный перечень состояний внутри категории.

| № | Категория | Граница ответственности |
|---:|---|---|
| 1 | Окружение компиляции и обязательный contract Morphant | Глобальные prerequisites, без которых generator не может корректно интерпретировать ни один mapper: минимальная эффективная версия C# и наличие однозначного совместимого набора обязательных Morphant symbols. |
| 2 | Объявление mapper-а и формируемость generated contract | Форма пользовательского mapper type и возможность корректно объявить его partial implementation, interfaces и operation contracts. Конфликты между зарегистрированными pair относятся к категории 3. |
| 3 | Регистрация mapping pair и допустимость типов | Вызовы `Map<TSource, TDestination>`, canonical identity, eligibility pair types, повторные pair текущего mapper-а и межпарные конфликты generated contract. Одинаковые pair в разных mapper types здесь не запрещаются. |
| 4 | Обнаружение конфигурации и builder flow | Возможность однозначно восстановить поддерживаемый прямой линейный flow `Configure`. Вынос Morphant builder-а в alias, helper, delegate либо неподдерживаемый control flow не игнорируется молча. |
| 5 | Локальная композиция mapping plan | Состав и взаимная совместимость локальных `Construct`, `Members`, `Convert` и остальных pair-level configuration fragments до анализа их содержимого. |
| 6 | Значения и применимость settings | Валидность effective settings, precedence и применимость policy к mapping model, destination capability и доступным операциям. |
| 7 | Наследование конфигурации и `IncludeBase` | `base.Configure(builder)`, configuration chain, typed `IncludeBase`, поиск base pair, совместимость, циклы, повторное включение и переносимость effective inherited rules. |
| 8 | Переносимость callbacks и declarative grammar | Возможность статически перенести пользовательские lambdas, expressions, locals, captures и control flow в generated implementation без исполнения configuration code. |
| 9 | Корректность construction plan | Выбор и применимость construction strategy, constructor binding и достижимые creation branches после успешной локальной композиции. |
| 10 | Корректность member plan | Выбор destination members, explicit и convention rules, mutability, initialization phase, dependencies и достижимая применимость assignments. |
| 11 | Корректность nested mapping | Статическая корректность adaptive `Map` и explicit nested `Create` / `Update`, включая operation, destination lifecycle и совместимость nested result. Runtime service lookup остаётся за пределами категории. |
| 12 | Полнота mapping-а через `UnmappedMemberValidation` | Warning-анализ неиспользованных source members и незаполненных destination members после построения effective reachable plan. |

## 5. Зафиксированные boundary-решения

### 5.1. Runtime lookup

Отсутствие mapping service, несколько registrations одной canonical pair,
registration, разрешившаяся в `null`, и завершённый mapping scope являются
runtime-состояниями. Они наблюдаются через `MappingNotFoundException`,
`AmbiguousMappingException`, `InvalidMappingRegistrationException` и
`MappingScopeCompletedException`, а не через compile-time diagnostics.

### 5.2. Неподдерживаемый builder flow

Настоящее использование Morphant builder-а вне поддерживаемого прямого
линейного `Configure` flow является ошибкой конфигурации, а не молча
игнорируемым кодом. Одноимённые вызовы стороннего API Morphant не принадлежат и
по-прежнему игнорируются.

### 5.3. Повторная canonical pair

Повторная регистрация одной canonical pair внутри одного mapper-а является
ошибкой конфигурации. Одинаковая pair в разных mapper types и assemblies
разрешена; неоднозначность возникает только при фактическом runtime lookup.

### 5.4. Compilation environment

Минимальная поддерживаемая эффективная версия языка — C# 9. Обязательная
Morphant dependency считается пригодной только когда generator может
однозначно разрешить совместимый публичный contract, необходимый его текущей
версии. Это охватывает отсутствие runtime-библиотеки, конфликтующие
определения metadata names и несовместимое смешение версий runtime и
generator-а.

Generator не способен выдать собственную diagnostic, если analyzer вообще не
загрузился в несовместимом compiler host: такой отказ происходит до запуска
Morphant и остаётся диагностикой хоста. Non-C# compilation не является
отдельной пользовательской границей пакета, поскольку generator публикуется в
`analyzers/dotnet/cs`.

### 5.5. Ownership generated mapping contract

Каждая зарегистрированная через `Map<TSource, TDestination>` canonical pair
передаёт generator-у полное владение соответствующим
`ITypeMapper<TSource, TDestination>` contract-ом mapper-а. Mapper не объявляет
этот interface заранее и не подменяет generated explicit implementations
ручными методами. Полностью ручное поведение pair выражается через `Convert`,
при этом interface и его `Create` / `Update` по-прежнему генерирует Morphant.

Exact contract, унаследованный mapper-ом через base class, конфликтом не
считается. Derived mapper может повторно зарегистрировать ту же canonical pair
и получить собственную generated implementation; по законам inheritance такая
регистрация начинает с чистого map-level plan и подключает base plan только
через явный `IncludeBase`.

`abstract`, generic, nested, private/protected и non-sealed mapper types сами
по себе разрешены, если их partial contract допустим в C#. Отсутствующий либо
недоступный source-bodied `Configure` относится к категории 4. Morphant не
дублирует точные ошибки C# для атрибута на неподходящем kind type-а, уже
противоречивых пользовательских partial declarations и других malformed
declarations, которые compiler полностью диагностирует без generated code.

## 6. Контракт и каталог diagnostics

На этапе 2 для каждой отдельной diagnostic фиксируются:

- ID, title, category и severity;
- точное условие появления и условия отсутствия;
- message format и параметры;
- primary и при необходимости additional locations;
- затрагиваемые mapper, pair и операции;
- precedence относительно первичных причин, правила каскадного подавления и
  дедупликации;
- generated recovery и доступность оставшихся операций;
- взаимодействие с settings, inheritance и
  `UnmappedMemberValidation`;
- минимальный набор positive, negative, location, deduplication, recovery и
  integration scenarios.

Каталог составляется и согласуется по одной категории в порядке раздела 4.
Diagnostic считается специфицированной только когда все поля контракта имеют
однозначное значение; рабочее имя без location и recovery policy не завершает
этап 2.

### 6.1. Политика ID

Diagnostic IDs имеют форму `MORPHdddd`, назначаются последовательно с
`MORPH0001`, не кодируют номер категории и после публикации никогда не
переиспользуются. Изменение опубликованного ID является breaking change:
существующие suppressions и `dotnet_diagnostic.<ID>.severity` перестанут
действовать.

Пятисимвольный project-specific prefix `MORPH` соответствует рекомендациям
Roslyn: ID является C# identifier, имеет форму `<PREFIX><number>`, содержит
меньше 15 символов, а prefix длиннее двух символов снижает риск коллизии.
Нормативная рекомендация:
[Choosing diagnostic IDs](https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/choosing-diagnostic-ids).

Перед первым назначением 7 августа 2026 года выполнен exact-search
`MORPH0001`–`MORPH0004` и analyzer-context использования prefix `MORPH` в
публичном GitHub code, web index и индексируемых NuGet/analyzer pages. Внешних
.NET diagnostics с этими ID не найдено. Посторонние строки `Morph0001` вне
Roslyn diagnostics коллизией не считаются. Единого глобального реестра и
механизма резервирования analyzer IDs нет, поэтому проверка фиксирует
отсутствие известной публичной коллизии на дату выбора, а не вечную
эксклюзивность prefix-а.

Перед назначением каждого следующего ID выполняется повторный публичный
collision check. При найденной внешней коллизии ещё не опубликованный ID можно
изменить; уже опубликованный ID сохраняется.

Перед назначением второй группы 8 августа 2026 года тем же способом проверены
`MORPH0005`–`MORPH0010`. Внешних публичных .NET/Roslyn diagnostics с этими ID
не найдено. Диапазоны между категориями не резервируются: IDs назначаются в
общей последовательности, а ownership категории выражается descriptor
category и каталогом, а не номером.

Перед назначением третьей группы 8 августа 2026 года тем же способом проверены
`MORPH0011`–`MORPH0014`. Внешних публичных .NET/Roslyn diagnostics с этими ID
не найдено.

Перед назначением четвёртой группы 8 августа 2026 года тем же способом
проверены `MORPH0015`–`MORPH0018`. Внешних публичных .NET/Roslyn diagnostics с
этими ID не найдено.

Перед назначением пятой группы 8 августа 2026 года тем же способом проверены
`MORPH0019`–`MORPH0020`. Внешних публичных .NET/Roslyn diagnostics с этими ID
не найдено.

### 6.2. Категория 1: общий contract

Категория «Окружение компиляции и обязательный contract Morphant» содержит
ровно четыре diagnostics:

| ID | Title | Message format |
|---|---|---|
| `MORPH0001` | `Unsupported C# language version` | `Morphant requires C# 9.0 or later, but this compilation uses C# {0}.` |
| `MORPH0002` | `Morphant runtime contract not found` | `Morphant generator requires a reference to a compatible Morphant runtime library.` |
| `MORPH0003` | `Ambiguous Morphant runtime contract` | `Multiple Morphant runtime contracts were found. Reference exactly one compatible Morphant runtime library.` |
| `MORPH0004` | `Incompatible Morphant runtime contract` | `The referenced Morphant runtime contract is incompatible with this generator: {0}.` |

Для всех четырёх diagnostics действует общий descriptor contract:

- category — `Morphant.Compatibility`;
- default severity — `Error`;
- diagnostic включена по умолчанию и не имеет `NotConfigurable`;
- description и help link отсутствуют, custom tags пусты;
- primary location — `Location.None`, additional locations отсутствуют;
- один ID публикуется не более одного раза на compilation независимо от числа
  syntax trees, mapper declarations и обнаруженных повреждённых symbols;
- проверка выполняется при каждом запуске загруженного generator-а, даже если
  compilation не объявляет mapper-ов;
- при любой опубликованной либо подавленной prerequisite diagnostic generator
  не создаёт ни одного source file и не запускает mapper-level analysis;
- recovery-stubs не создаются: C# language либо runtime API недостаточно
  надёжны даже для корректного объявления общего generated contract;
- изменение severity или suppression через `.editorconfig`/MSBuild меняет
  только представление diagnostic compiler-ом, но не снимает generation gate.

`MORPH0001` независима от runtime contract и может публиковаться одновременно
с одной из `MORPH0002`–`MORPH0004`. Runtime diagnostics взаимоисключающие:
ambiguity имеет приоритет над incompatibility, частично найденный contract
считается incompatible, а missing публикуется только при полном отсутствии
contract candidate.

### 6.3. `MORPH0001`: unsupported language version

Diagnostic публикуется, когда effective `CSharpParseOptions.LanguageVersion`
ниже `LanguageVersion.CSharp9`. В message parameter `{0}` передаётся
стандартное display name effective version, например `8.0`, а не raw enum name
или указанное пользователем alias-значение `default` / `latest`.

Diagnostic отсутствует для C# 9 и любой более новой effective version,
включая `latest`, `latestMajor` и `preview`, когда они разрешаются в версию не
ниже C# 9. Наличие mapper-ов, runtime contract и их корректность на условие не
влияют.

Diagnostic блокирует всю генерацию. Если runtime contract одновременно
missing, ambiguous либо incompatible, соответствующая независимая runtime
diagnostic также публикуется; mapper-level diagnostics подавляются.

### 6.4. Runtime contract candidate и revision

Runtime contract candidate — compilation assembly либо referenced assembly,
который содержит assembly metadata
`Morphant.GeneratorContractVersion` или объявляет хотя бы один bootstrap
symbol Morphant. Bootstrap manifest revision 1 включает:

- `Morphant.MorphantMapperAttribute`;
- `Morphant.TypeMapper` и его единственный применимый instance method
  `void Configure(Morphant.MapperBuilder)`;
- `Morphant.MapperBuilder`, `Morphant.MapperBuilderBase<T>` и
  `Morphant.MapperBuilder<TSource, TDestination>`;
- instance registration method
  `MapperBuilder<TSource, TDestination> Map<TSource, TDestination>(MappingMode)`;
- runtime type families, непосредственно связываемые либо называемые
  generated code: `ITypeMapper<,>`, `Option<>`, `Context.MappingContext`,
  `Context.MappingOperation`, `Delegates.Construct`, `Delegates.Members`,
  `Delegates.Convert`, constructor/member wrappers, declarative markers и
  Morphant exception types, используемые generated branches.

Manifest проверяет metadata name, kind, generic arity, accessibility и
обязательную member signature. Он не требует byte-for-byte совпадения всего
публичного API: поддерживаемая revision является обещанием полного
generator/runtime contract, а structural manifest защищает bootstrap и
обнаруживает повреждённую либо подменённую dependency.

Runtime assembly публикует ровно одно значение assembly metadata
`Morphant.GeneratorContractVersion=1`. Это package-coordination metadata, а не
новый C# mapping API. Совместимые runtime и generator releases используют одну
revision; несовместимое изменение contract повышает revision. NuGet package
version для этой проверки не используется.

### 6.5. `MORPH0002`: runtime contract not found

Diagnostic публикуется, когда compilation не содержит ни одного runtime
contract candidate: отсутствуют и metadata revision, и все bootstrap symbols.
Типичный случай — analyzer загружен отдельно, а compile assets библиотеки
Morphant не подключены.

Частично присутствующий bootstrap, assembly с revision metadata либо любое
конфликтующее определение не считается missing: оно переходит в
`MORPH0003` или `MORPH0004`. Diagnostic не публикуется для единственного
однозначного compatible candidate.

### 6.6. `MORPH0003`: ambiguous runtime contract

Diagnostic публикуется, когда найдено несколько runtime contract candidates
либо один из обязательных metadata names имеет несколько конкурирующих
определений, из-за которых нельзя выбрать единственный согласованный symbol
set. Это включает одновременные runtime references и shadow-определения
contract types в consumer compilation.

Совместимость отдельных candidates не анализируется дальше: ambiguity является
первичной причиной и подавляет `MORPH0002`/`MORPH0004`. Message намеренно не
перечисляет assembly paths, чтобы результат не зависел от машины; конкретные
candidate identities сохраняются только как детерминированные test/debug data,
не как часть публичного message contract.

### 6.7. `MORPH0004`: incompatible runtime contract

Diagnostic публикуется для единственного runtime contract candidate, если его
нельзя безопасно использовать с текущим generator-ом. Проверка идёт в
детерминированном порядке и останавливается на первой причине:

1. metadata `Morphant.GeneratorContractVersion` отсутствует, повторена либо не
   является одним canonical decimal integer;
2. revision не входит в exact set revisions, поддерживаемых generator-ом;
3. bootstrap symbol отсутствует;
4. bootstrap symbol имеет неверный kind, generic arity, accessibility либо
   обязательную member signature.

Внутри шагов 3–4 manifest проверяется в порядке групп из раздела 6.4, а
symbols одной группы — по полному metadata name в ordinal order. Поэтому при
нескольких shape failures reason не зависит от порядка metadata references.

В `{0}` передаётся одна из стабильных reason forms без завершающей точки:

- `contract revision metadata 'Morphant.GeneratorContractVersion' is missing`;
- `contract revision metadata 'Morphant.GeneratorContractVersion' is duplicated`;
- `contract revision metadata 'Morphant.GeneratorContractVersion' is invalid`;
- `contract revision '{actual}' is not supported; expected '{expected}'`;
- `required symbol '{metadataName}' is missing`;
- `required symbol '{metadataName}' has an incompatible shape`.

Для revision 1 generator поддерживает exact set `{ 1 }`, поэтому
`{expected}` равно `1`. Если будущий generator на переходный период
поддерживает несколько revisions, reason перечисляет их по возрастанию через
`, `; это перечисление становится частью exact message tests.

### 6.8. Дедупликация, порядок и suppression

Publication order категории фиксирован: `MORPH0001`, затем одна выбранная
runtime diagnostic. Среди runtime failures precedence равно
`MORPH0003` > `MORPH0004` > `MORPH0002`; это приоритет причин, а не сортировка
одновременно публикуемых сообщений.

Повторные observations внутри одной compilation сводятся к одной причине до
создания `Diagnostic`. Incremental reevaluation с тем же effective language и
runtime contract не меняет ID, message или order. Замена parse options либо
runtime reference пересчитывает global gate и либо снимает его целиком, либо
публикует новый точный набор без сохранения прежнего состояния.

Поскольку diagnostics configurable, пользователь может понизить либо
подавить их стандартным `dotnet_diagnostic.MORPHdddd.severity`. Это не является
opt-out из compatibility policy: source generation остаётся отключённой, пока
prerequisite фактически не исправлена.

### 6.9. Самостоятельная тестовая матрица категории 1

Unit-категория окружения независимо фиксирует:

- exact descriptors всех четырёх diagnostics: ID, title, category, default
  severity, enabled/configurable flags, message format и отсутствие locations;
- C# 8 против C# 9, explicit/default/latest/preview aliases и exact display
  parameter `MORPH0001`;
- отсутствие runtime contract, единственный compatible contract, частичный
  bootstrap, missing/duplicated/malformed/unsupported revision и каждую
  bootstrap shape failure;
- две runtime assemblies, runtime плюс consumer shadow types и несколько
  ambiguous metadata names с единственной `MORPH0003`;
- совместную публикацию `MORPH0001` и ровно одной runtime diagnostic, exact
  order и runtime precedence;
- множество syntax trees и mapper declarations без дублирования global
  diagnostics;
- полный empty generated result и отсутствие всех mapper-level diagnostics
  при каждом gate failure;
- suppression и изменение severity без возобновления generation;
- add/change/remove/restore parse-options и reference actualization при одном
  сохранённом incremental driver-е.

Package-like integration-категория независимо проверяет:

- normal Morphant runtime + bundled generator в C# 9 consumer без diagnostics;
- analyzer-only consumer без compile assets с `MORPH0002` и без generated
  files;
- mismatched runtime/generator revisions с exact `MORPH0004`;
- duplicate runtime contracts с exact `MORPH0003`;
- C# 8 consumer с exact `MORPH0001`;
- suppress/override severity в реальном project: build presentation меняется,
  но generated Morphant contract по-прежнему отсутствует.

### 6.10. Категория 2: общий contract

Категория «Объявление mapper-а и формируемость generated contract» содержит
ровно шесть diagnostics:

| ID | Title | Message format |
|---|---|---|
| `MORPH0005` | `Mapper must derive from TypeMapper` | `Mapper '{0}' must derive from 'Morphant.TypeMapper'.` |
| `MORPH0006` | `Mapper must be partial` | `Mapper '{0}' must be declared partial so Morphant can generate its mapping contract.` |
| `MORPH0007` | `Containing type must be partial` | `Containing type '{0}' must be declared partial so Morphant can generate nested mapper contracts.` |
| `MORPH0008` | `File-local mapper declaration is not supported` | `File-local type '{0}' cannot declare or contain a generated Morphant mapper contract.` |
| `MORPH0009` | `Mapping contract is already declared` | `Mapping contract '{0}' is already declared by mapper '{1}'. Remove the interface declaration or the Map registration.` |
| `MORPH0010` | `Mapping contract conflicts with a declared interface` | `Mapping contract '{0}' can unify with an interface contract declared by mapper '{1}'.` |

Для всех шести diagnostics действует общий descriptor contract:

- category — `Morphant.Declaration`;
- default severity — `Error`;
- diagnostic включена по умолчанию и не имеет `NotConfigurable`;
- description и help link отсутствуют, custom tags пусты;
- изменение severity либо suppression меняет только compiler presentation и
  не делает структурно невозможную generation допустимой;
- проверка начинается только после успешного compilation-wide gate категории
  1; prerequisite diagnostic подавляет всю категорию 2;
- effective settings, mapper inheritance settings,
  `UnmappedMemberValidation` и достижимость `Create` / `Update` не влияют на
  условия категории: речь идёт о форме самого declaration contract до
  построения mapping plan.

Параметры type names используют один fully-qualified nullable-aware display с
`global::`, special type keywords и escaped identifiers. Mapping contract
форматируется как
`global::Morphant.ITypeMapper<{canonicalSource}, {canonicalDestination}>` по
canonical pair identity: reference nullability, `dynamic`/`object` и tuple
element names не создают разные сообщения для одной pair.

`MORPH0005` и `MORPH0006` дедуплицируются по mapper symbol. `MORPH0007` и
`MORPH0008` дедуплицируются по конкретному containing/file-local type symbol,
поэтому одна проблема общего container-а не повторяется для каждого nested
mapper-а. `MORPH0009` и `MORPH0010` дедуплицируются по mapper и canonical pair
и всегда привязываются к первой регистрации этой pair. Одинаковые mapper либо
pair names в разных symbol identities остаются независимыми.

### 6.11. `MORPH0005`: mapper base type

Diagnostic публикуется для class declaration с `MorphantMapperAttribute`,
если его base-type chain не содержит exact `Morphant.TypeMapper` symbol из
проверенного runtime contract. В `{0}` передаётся fully-qualified mapper type.
Primary location — name syntax применённого `MorphantMapperAttribute`;
additional locations отсутствуют.

Diagnostic отсутствует при прямом и косвенном наследовании `TypeMapper`.
Malformed base declaration, для которого compiler уже выдаёт точную и
достаточную type-resolution либо inheritance error, не получает дублирующую
Morphant diagnostic.

`MORPH0005` является mapper-wide первичной причиной: остальные diagnostics
категории 2 и категории 3–12 для этого type-а подавляются, generated artifacts
этого mapper-а отсутствуют полностью. Другие корректные mapper-ы compilation
продолжают анализироваться и генерироваться.

### 6.12. `MORPH0006`: partial mapper

Diagnostic публикуется, когда корректно объявленный mapper, который Morphant
должен дополнить generated declaration-ом, сам не имеет модификатора
`partial`. В `{0}` передаётся fully-qualified mapper type. Primary location —
identifier mapper declaration; additional locations отсутствуют.

Все пользовательские declarations одного уже partial type-а должны быть
C#-согласованными. Если несколько пользовательских declarations сами нарушают
законы partial types, Morphant не повторяет существующую compiler diagnostic.
Несколько согласованных partial declarations и модификаторы `abstract`,
`sealed` либо обычный non-sealed class diagnostic не создают.

Diagnostic запрещает только executable `TypeMapper` artifact mapper-а.
Construction/member/extension surfaces, которые независимо C#-legal и
однозначно выводятся из поддерживаемых registrations, сохраняются, чтобы
configuration DSL не создавал каскад `CS1061`. Категории 3–12 для mapper-а при
этом подавляются.

### 6.13. `MORPH0007`: partial containing type

Diagnostic публикуется для каждого type declaration в lexical containing
chain mapper-а, который Morphant должен повторно открыть в generated file, но
который не объявлен `partial`. В `{0}` передаётся fully-qualified containing
type. Primary location — identifier соответствующего declaration; additional
locations отсутствуют.

Несколько non-partial ancestors дают по одной diagnostic на каждый type. Один
общий container нескольких mapper-ов даёт одну diagnostic и блокирует
executable artifact каждого вложенного mapper-а. Уже malformed partial type
остаётся compiler-у. Legal partial class, record, struct и interface
containers сами по себе разрешены.

Recovery совпадает с `MORPH0006`: executable mapper artifact отсутствует, но
независимо legal construction/member/extension surfaces сохраняются;
категории 3–12 затронутых mapper-ов подавляются.

### 6.14. `MORPH0008`: file-local declaration

Diagnostic публикуется, когда mapper либо любой type в его lexical containing
chain имеет `INamedTypeSymbol.IsFileLocal`: generated syntax tree не может
законно продолжить file-local partial type. В `{0}` передаётся fully-qualified
file-local type. Primary location — token `file`; additional locations
отсутствуют.

Один file-local container нескольких mapper-ов даёт одну diagnostic. Она может
публиковаться вместе с независимыми `MORPH0006`/`MORPH0007`, если declarations
одновременно требуют `partial`; `MORPH0005` остаётся более ранней mapper-wide
причиной.

Executable artifacts всех затронутых mapper-ов отсутствуют. Независимо legal
construction/member/extension surfaces сохраняются, а категории 3–12
затронутых mapper-ов подавляются.

### 6.15. Declared interface graph и unification

Declared interface graph mapper-а состоит из interfaces, непосредственно
указанных в base lists любых его partial declarations, и их транзитивных base
interfaces. Interfaces, полученные только через base class mapper-а, в этот
graph не входят: exact inherited mapping contract разрешён разделом 5.5.

Mapping contract candidate — constructed interface, чья
`OriginalDefinition` равна runtime symbol `Morphant.ITypeMapper<,>`. Candidate
совпадает с зарегистрированной pair, когда обе canonical type identities
равны. Два неравных contracts могут унифицироваться, если одна согласованная
подстановка type parameters делает равными обе позиции `TSource` и
`TDestination`. Generic constraints не используются как доказательство
неравенства, как и для конфликтов между двумя generated pair категории 3.

Для `MORPH0009`/`MORPH0010` additional locations являются locations всех
непосредственно объявленных interface type syntaxes mapper-а, чьи interface
graphs вводят конфликтующий candidate. Они сортируются в стабильном source
order и не включают metadata-only declarations транзитивных interfaces.

### 6.16. `MORPH0009`: exact contract already declared

Diagnostic публикуется, когда первая регистрация canonical pair требует
generated `ITypeMapper<,>`, уже присутствующий как exact candidate в declared
interface graph mapper-а. В `{0}` передаётся canonical mapping contract, в
`{1}` — fully-qualified mapper type. Primary location — identifier `Map`
первой регистрации pair; additional locations задаёт раздел 6.15.

Diagnostic публикуется независимо от наличия implicit либо explicit user
implementations interface-а: contract зарегистрированной pair принадлежит
generator-у. Несколько paths к тому же exact candidate и повторные
registrations pair не дублируют diagnostic. Exact contract через base class
diagnostic не создаёт.

Конфликтующая pair не входит только в executable mapper artifact; её legal
construction/member/extension surfaces сохраняются. Независимые pairs того же
mapper-а продолжают генерацию. `MORPH0009` имеет приоритет над `MORPH0010` и
подавляет только diagnostics, которым для этой canonical pair уже требуется
формируемый executable mapping plan. Независимо устанавливаемые registration
и builder-flow errors по-прежнему публикуются.

### 6.17. `MORPH0010`: unifiable declared contract

Diagnostic публикуется, когда exact candidate отсутствует, но generated
contract первой регистрации canonical pair способен унифицироваться хотя бы с
одним отличающимся candidate из declared interface graph mapper-а. В `{0}`
передаётся canonical mapping contract, в `{1}` — fully-qualified mapper type.
Primary location — identifier `Map` первой регистрации pair; additional
locations задаёт раздел 6.15.

Diagnostic отсутствует для contracts, которые не могут стать равными при
единой подстановке, и для interfaces, полученных только через base class.
Несколько unifiable candidates дают одну diagnostic canonical pair со всеми
соответствующими direct-interface locations.

Recovery pair-local: конфликтующий executable contract отсутствует, legal
construction/member/extension surfaces сохраняются, а независимые pairs того
же mapper-а продолжают генерацию. Diagnostics, которым для этой pair уже
требуется формируемый executable mapping plan, подавляются; независимо
устанавливаемые registration и builder-flow errors сохраняются.

### 6.18. Precedence, порядок и suppression

`MORPH0005` подавляет остальные mapper diagnostics. Без него независимые
`MORPH0006`, `MORPH0007` и `MORPH0008` могут публиковаться вместе; наличие хотя
бы одной из них подавляет pair-local `MORPH0009`/`MORPH0010` и категории 3–12
затронутого mapper-а. Для одной canonical pair exact `MORPH0009` имеет
приоритет над unifiable `MORPH0010`. Pair-local contract conflict не скрывает
independent duplicate/eligibility либо builder-flow reason, но останавливает
анализ содержимого mapping plan, который generator всё равно не сможет
испустить.

Publication order — по ID, затем ordinal stable identity mapper-а,
containing type-а либо canonical pair. Порядок syntax discovery и
incremental invalidation не влияет на diagnostic set, messages, primary или
additional locations.

Suppression либо понижение severity не возобновляет запрещённый executable
artifact. Structural state пересчитывается при actualization declaration-а,
base type либо interface graph: после исправления diagnostic исчезает и
generation восстанавливается без сохранения прежнего gate.

### 6.19. Самостоятельная тестовая матрица категории 2

Unit-категория declaration независимо фиксирует:

- exact descriptors `MORPH0005`–`MORPH0010`: ID, title, category, default
  severity, enabled/configurable flags, message formats и parameters;
- direct/indirect `TypeMapper` inheritance, unrelated base class, exact
  attribute location и отсутствие mapper-level cascades у `MORPH0005`;
- single non-partial mapper, несколько согласованных partial declarations,
  already-malformed partial declarations и exact mapper identifier location;
- каждый legal containing kind, несколько non-partial ancestors, общий
  container нескольких mapper-ов, deduplication и exact containing identifier
  locations `MORPH0007`;
- file-local mapper и file-local container, exact `file` location,
  deduplication общего container-а и совместную публикацию с partial errors;
- direct exact `ITypeMapper<,>`, exact candidate через derived interface,
  несколько direct paths, user implementations и разрешённый exact contract
  только через base class для `MORPH0009`;
- direct и transitive generic interface unification, nested constructed
  roots, ignored constraints, non-unifiable contracts, base-class exclusion и
  precedence `MORPH0009` над `MORPH0010`;
- exact primary/additional locations, canonical message normalization,
  deduplication первой `Map` registration и стабильный порядок diagnostics;
- полный generated result каждого recovery: отсутствие executable artifact
  при mapper-wide failure, сохранение independently legal DSL surfaces,
  исключение только конфликтующей pair и сохранение независимых pairs;
- suppression/изменение severity без возобновления запрещённого artifact и
  отсутствие diagnostics категорий 3–12, которые стали недостоверными;
- add/change/remove/restore `TypeMapper` base, `partial`, containing/file-local
  modifiers и direct interface graph при одном сохранённом incremental
  driver-е.

Package-like integration-категория независимо проверяет:

- compilable generated contracts для abstract, closed generic, nested,
  private/protected и non-sealed mapper forms;
- non-partial mapper, non-partial container и file-local chain с exact
  diagnostics без каскадных C# errors в сохранённых DSL surfaces;
- direct exact и unifiable user-declared interfaces с отсутствующим только
  конфликтующим executable contract и исполнимой независимой pair;
- derived mapper, который наследует exact contract, повторно регистрирует pair
  и получает собственную исполнимую generated implementation;
- реальное `.editorconfig`/MSBuild suppression или severity override:
  presentation меняется, но structurally impossible artifact не появляется.

### 6.20. Категория 3: общий contract

Категория «Регистрация mapping pair и допустимость типов» содержит ровно
четыре diagnostics:

| ID | Title | Message format |
|---|---|---|
| `MORPH0011` | `Mapping type is unavailable to generated code` | `The {0} type '{1}' is unavailable to Morphant-generated code.` |
| `MORPH0012` | `Unsupported mapping root type` | `The {0} type '{1}' is not supported as a mapping root because it is {2}.` |
| `MORPH0013` | `Duplicate mapping registration` | `Mapping contract '{0}' is registered more than once in mapper '{1}'.` |
| `MORPH0014` | `Mapping contracts can unify` | `Mapping contracts '{0}' and '{1}' can unify in mapper '{2}'.` |

Для всех четырёх diagnostics действует общий descriptor contract:

- category — `Morphant.Registration`;
- default severity — `Error`;
- diagnostic включена по умолчанию и не имеет `NotConfigurable`;
- description и help link отсутствуют, custom tags пусты;
- проверка начинается только после успешного compilation-wide gate категории
  1 и mapper-wide structural gate категории 2;
- effective settings, `MappingMode`, mapper inheritance settings,
  `UnmappedMemberValidation` и достижимость `Create` / `Update` не влияют на
  условия категории: eligibility и уникальность contract-а устанавливаются
  до построения operation-aware mapping plan;
- suppression либо изменение severity меняет только compiler presentation и
  не меняет eligibility, ownership registration или generated recovery.

В `{0}` diagnostics `MORPH0011`/`MORPH0012` передаётся lowercase role
`source` либо `destination`, а в `{1}` — fully-qualified nullable-aware имя
фактического type argument с `global::`, special type keywords и escaped
identifiers. В `{2}` `MORPH0012` передаётся одна из фиксированных reason
phrases раздела 6.23.

Mapping contracts в `MORPH0013`/`MORPH0014` форматируются как
`global::Morphant.ITypeMapper<{canonicalSource}, {canonicalDestination}>`.
Mapper type форматируется тем же fully-qualified display, что и в категории
2. Message parameters не используют исходные aliases и не зависят от
nullable-context конкретного syntax tree.

### 6.21. Canonical identity и registration order

Canonical identity mapping type строится рекурсивно по semantic symbol и
игнорирует:

- source aliases;
- reference nullable annotations на корне и внутри generic arguments;
- различие `dynamic` и `object`;
- имена tuple elements;
- различие native-integer syntax `nint` / `nuint` и соответствующих
  `System.IntPtr` / `System.UIntPtr`.

`Nullable<T>` остаётся отдельным constructed value type и не совпадает с `T`.
Array rank и shape, nominal generic definition, содержащие types, порядок и
canonical identities generic arguments входят в identity. Разные type
parameters различаются по symbol identity, а не только по имени.

Registration order — стабильный lexical order вызовов `Map` внутри
mapper-level. Для одной canonical pair первая registration является
authoritative: она владеет mapping plan и служит earlier location для всех
повторов. Одинаковая pair в другом mapper type либо повторно объявленная в
derived mapper-е является независимой и не конфликтует с base mapper-ом.

`MORPH0011` и `MORPH0012` дедуплицируются по mapper, canonical pair и role и
привязываются к первой registration pair. Если обе root-позиции независимо
ошибочны, публикуются две diagnostics — source перед destination. Повторные
registrations не повторяют eligibility diagnostics, но получают собственные
`MORPH0013` по разделу 6.25.

### 6.22. `MORPH0011`: unavailable mapping type

Diagnostic публикуется, когда семантически разрешившийся type argument
`Map<TSource, TDestination>` допустим для пользовательского generic-вызова,
но полный type graph невозможно однозначно назвать из общего generated
assembly-context. Проверка рекурсивно охватывает root, его containing types,
array element и generic arguments. В частности, сюда входят `private`,
`private protected`, `protected` и file-local types на любой глубине.

Primary location — полный syntax соответствующего source либо destination
type argument первой registration canonical pair; additional locations
отсутствуют. Если один inaccessible symbol встречается в нескольких разных
pairs, каждая затронутая pair получает собственную diagnostic.

Public и доступные из текущей assembly `internal` / `protected internal` types
diagnostic не создают. Ошибочные, pointer/function-pointer, `void`, ref-like,
static, anonymous, unbound generic и другие forms, которые уже не могут
связать generic-вызов либо получают точную достаточную C# diagnostic, Morphant
повторно не диагностирует.

`MORPH0011` является pair-local structural gate. Pair полностью исключается:
для неё не генерируются executable `ITypeMapper<,>` contract,
construction/member/extension surfaces или recovery-stub. Независимые legal
pairs mapper-а продолжают генерацию. Если хотя бы одна root-позиция получает
`MORPH0011`, semantic root-анализ всей pair уже недостоверен: `MORPH0012` для
обеих позиций не публикуется.

### 6.23. Классификация unsupported roots

После успешной проверки nameability с root снимается только верхнеуровневая
`Nullable<T>`-обёртка. Первый совпавший пункт следующего фиксированного порядка
задаёт единственную reason phrase `MORPH0012` для root-позиции:

| Root category | Условие | `{2}` |
|---|---|---|
| Type parameter | Root является type parameter независимо от constraints | `a root type parameter` |
| Tuple | Tuple syntax, `System.ValueTuple`, `System.Tuple` либо type, реализующий `System.Runtime.CompilerServices.ITuple` | `a tuple` |
| Sequence, collection or buffer | Array; `IEnumerable` кроме `string`; `IEnumerator`; async enumerable/enumerator; `Memory<T>`, `ReadOnlyMemory<T>` или `ReadOnlySequence<T>` | `a sequence, collection, or buffer` |
| Delegate | Конкретный delegate, `System.Delegate` либо `System.MulticastDelegate` | `a delegate` |
| Expression tree | `System.Linq.Expressions.Expression` либо derived type, включая `Expression<TDelegate>` | `an expression tree` |
| Deferred or async value | `Task` hierarchy, `ValueTask`, `ValueTask<T>` либо `Lazy<T>` | `a deferred or async value` |
| Push sequence | Type, реализующий `IObservable<T>` | `a push sequence` |

Категории симметричны для source и destination. Они проверяются только на
корне: `Envelope<Task<T>>`, `Page<List<int>>` и другие nameable nominal roots
с отложенной категорией внутри generic arguments остаются eligible. Если сам
outer root реализует отложенный contract, запрет применяется. `string` не
считается collection root.

### 6.24. `MORPH0012`: unsupported mapping root

Diagnostic публикуется для каждой root-позиции первой registration pair,
прошедшей nameability gate и попавшей в классификацию раздела 6.23. Primary
location — полный syntax соответствующего type argument; additional locations
отсутствуют. Две unsupported позиции одной pair дают две diagnostics с
одинаковым ID и разными role, type name и location.

Pair сохраняет полный executable
`ITypeMapper<TSource, TDestination>` contract. Обе операции независимо от
effective `MappingMode` бросают `MappingConfigurationException` с
детерминированной причиной: сначала source, затем destination. Generated
construction, member и pair-extension surfaces отсутствуют, включая
`Construct`, `Members` и `Convert`; unsupported registration не получает
скрытый manual либо runtime fallback.

Независимые eligible pairs mapper-а продолжают генерацию. Diagnostics и
mapping-plan анализ категорий 5–12 для unsupported pair подавляются; независимо
доказуемые registration и builder-flow errors сохраняются.

### 6.25. `MORPH0013`: duplicate mapping registration

Diagnostic публикуется на каждой registration canonical pair после первой в
одном mapper-level. Primary location — identifier `Map` текущей лишней
registration; единственная additional location — identifier `Map` первой
authoritative registration. В `{0}` передаётся canonical mapping contract, в
`{1}` — mapper type.

Три регистрации одной pair дают две diagnostics: на второй и третьей, обе со
ссылкой на первую. Aliases и остальные normalization rules раздела 6.21 не
создают отдельные pairs. Exact registrations в разных mapper types, а также в
base и derived mapper-е, diagnostic не создают.

Первая registration целиком владеет local plan. Все chained configuration
вызовы последующих registrations игнорируются и не объединяются с первой;
зависимые diagnostics категорий 4–12 для отброшенного chain не публикуются.
Recovery первой registration определяется её собственным состоянием, поэтому
`MORPH0013` может публиковаться вместе с `MORPH0009`, `MORPH0010`,
`MORPH0011`, `MORPH0012` либо `MORPH0014` этой authoritative pair.

### 6.26. Unification generated contracts

После collapse exact duplicates две разные canonical pairs одного generic
mapper-а конфликтуют, если существует одна конечная согласованная подстановка
свободных type parameters mapper-а и его containing types, которая делает
равными обе позиции их `ITypeMapper<TSource, TDestination>` contracts.
Подстановка рекурсивна и не допускает self-containing type; generic
constraints не используются как доказательство невозможности равенства.

Unification проверяет nested constructed roots и canonical normalization
раздела 6.21. Pair с `MORPH0012` участвует: без structural conflict она всё
равно получила бы executable exception-stub. Pair, уже исключённая
`MORPH0009`, `MORPH0010` или `MORPH0011`, не участвует. Exact duplicate
относится только к `MORPH0013` и не образует `MORPH0014` сама с собой.

Conflict identity — mapper и неупорядоченная пара двух canonical contracts.
Для сообщения и locations contracts упорядочиваются по registration order;
при равных source locations используется ordinal canonical identity как
стабильный tie-breaker.

### 6.27. `MORPH0014`: unifiable mapping contracts

Diagnostic публикуется один раз для каждой конфликтующей неупорядоченной пары
contracts. В `{0}` передаётся earlier contract, в `{1}` — later contract, в
`{2}` — mapper type. Primary location — identifier `Map` later registration;
единственная additional location — identifier `Map` earlier registration.

Три contracts, каждый из которых унифицируется с двумя другими, дают три
diagnostics. Один contract, конфликтующий с двумя earlier contracts, получает
две diagnostics на одном primary location, но с разными message parameters и
additional locations. Неунифицируемые pairs и одинаковые pairs разных mapper
types diagnostic не создают.

Все pairs, участвующие хотя бы в одном конфликте, исключаются из executable
mapper artifact: interface и explicit implementations для них не генерируются.
Их независимо legal construction/member/extension surfaces сохраняются, чтобы
поддерживаемый configuration DSL компилировался; у unsupported root таких
surfaces по разделу 6.24 нет. Независимые legal pairs того же mapper-а
полностью сохраняются.

`MORPH0012` и `MORPH0014` могут публиковаться одновременно. В этом случае
structural unification recovery имеет приоритет над exception-stub: contract
unsupported pair отсутствует целиком. Diagnostics категорий 5–12, которым
нужен формируемый executable plan конфликтующей pair, подавляются;
независимо доказуемые builder-flow errors сохраняются.

### 6.28. Precedence, порядок и suppression

Compilation-wide gate категории 1 и mapper-wide structural errors
`MORPH0005`–`MORPH0008` подавляют категорию 3. Pair-local
`MORPH0009`/`MORPH0010` не скрывают независимо доказуемые
`MORPH0011`–`MORPH0013`, но исключают pair из `MORPH0014`. Внутри категории 3
structural `MORPH0011` подавляет semantic `MORPH0012` и unification-анализ
той же pair; `MORPH0013` остаётся независимой причиной; `MORPH0012` участвует
в `MORPH0014`.

Publication order — по ID, затем ordinal stable mapper identity, canonical
contract, role `source` перед `destination` и registration source order.
`MORPH0014` дополнительно сортируется по ordered pair contracts. Discovery и
incremental invalidation не меняют diagnostic set, messages, primary или
additional locations.

Suppression либо понижение severity не меняет authoritative registration,
eligibility или набор generated artifacts. Add/change/remove/restore type
accessibility, root shape, duplicate registration либо pair shape
пересчитывает соответствующий gate при actualization без сохранения прежнего
recovery.

### 6.29. Самостоятельная тестовая матрица категории 3

Unit-категория registration независимо фиксирует:

- exact descriptors `MORPH0011`–`MORPH0014`: ID, title, category, default
  severity, enabled/configurable flags, message formats, role/reason и type
  parameters;
- positive eligibility matrix: built-in и BCL scalars, enum, class, struct,
  record, nullable value/reference type, abstract class, interface и
  constructed generic с type parameter либо отложенной категорией только
  внутри известного nominal root;
- private, private-protected, protected и file-local root, containing type и
  nested generic argument; разрешённые public/internal/protected-internal
  forms; обе roles, exact type-argument locations и pair-local deduplication
  `MORPH0011`;
- malformed generic arguments с достаточной C# diagnostic без дублирующей
  Morphant diagnostic и сохранение независимой legal pair;
- каждую root-категорию раздела 6.23 напрямую и под `Nullable<T>`, source и
  destination, обе позиции одновременно, category precedence, `string`
  exclusion и пользовательские implementations отложенных contracts;
- отсутствие `MORPH0012` для отложенного type только внутри legal nominal
  root, exact role/reason parameters и suppression semantic root diagnostic
  при `MORPH0011`;
- aliases, reference nullability на любой глубине, `dynamic`/`object`, tuple
  element names и native-integer syntax как canonical duplicates, при этом
  `Nullable<T>` как отдельную pair;
- две и три exact registrations, разрешённые одинаковые pairs в разных
  mapper types и в base/derived mapper-ах, primary location каждой лишней
  `Map`, first-registration additional location и first-plan ownership
  `MORPH0013`;
- direct и nested generic unification, содержащие type parameters, ignored
  constraints, occurs check, non-unifiable shapes и exact one-diagnostic-per-
  unordered-pair cardinality `MORPH0014`;
- участие `MORPH0012` и исключение `MORPH0009`, `MORPH0010`, `MORPH0011` из
  unification, exact primary/additional locations и deterministic order;
- полный generated result каждого recovery: полное исключение unavailable
  pair, complete throwing contract без ложных DSL surfaces для unsupported
  root, first-plan ownership duplicate registration, исключение всех
  unification participants, сохранение legal DSL surfaces и независимых pairs;
- suppression/изменение severity без изменения recovery и отсутствие
  недостоверных downstream diagnostics;
- add/change/remove/restore accessibility, root category, duplicate и
  unifiable shape при одном сохранённом incremental driver-е.

Package-like integration-категория независимо проверяет:

- unavailable nested/file-local type с exact `MORPH0011`, отсутствующими
  artifacts только затронутой pair и сохранённой независимой pair;
- каждую unsupported root family хотя бы в одной из двух positions, обе
  positions вместе и реальный вызов обеих операций полного suppressed-error
  contract-а с `MappingConfigurationException`;
- отсутствие construction/member/extension surfaces unsupported root в
  реальном consumer project;
- suppressed duplicate diagnostic: исполним только plan первой registration,
  поздние chains не дополняют и не переопределяют его;
- suppressed unification diagnostics: конфликтующие executable contracts
  отсутствуют, legal configuration surfaces и независимая исполнимая pair
  сохраняются;
- реальное `.editorconfig`/MSBuild suppression или severity override для
  каждой recovery-family без изменения generated artifact set.

### 6.30. Категория 4: общий contract

Категория «Обнаружение конфигурации и builder flow» содержит ровно четыре
diagnostics:

| ID | Title | Message format |
|---|---|---|
| `MORPH0015` | `Mapper must declare Configure` | `Mapper '{0}' must declare a source-bodied override of 'Configure(Morphant.MapperBuilder)'.` |
| `MORPH0016` | `Base mapper configuration is unavailable` | `The Configure body for base mapper '{0}' is unavailable while analyzing mapper '{1}'.` |
| `MORPH0017` | `Unsupported mapper builder flow` | `Mapper builder flow in Configure of mapper '{0}' cannot be analyzed by Morphant.` |
| `MORPH0018` | `Unsupported mapping builder flow` | `Mapping builder flow for contract '{0}' in mapper '{1}' cannot be analyzed by Morphant.` |

Для всех четырёх diagnostics действует общий descriptor contract:

- category — `Morphant.Configuration`;
- default severity — `Error`;
- diagnostic включена по умолчанию и не имеет `NotConfigurable`;
- description и help link отсутствуют, custom tags пусты;
- проверка начинается только после успешного compilation-wide gate категории
  1 и mapper-wide structural gate категории 2;
- effective settings, `MappingMode`, `UnmappedMemberValidation` и
  достижимость `Create` / `Update` не влияют на сам факт восстановимости
  configuration flow;
- suppression либо изменение severity меняет только compiler presentation и
  не превращает недоступную или неоднозначную конфигурацию в исполнимый plan.

Mapper и base mapper types в message parameters используют fully-qualified
nullable-aware display категории 2. Mapping contract в `MORPH0018`
форматируется по canonical identity категории 3 как
`global::Morphant.ITypeMapper<{canonicalSource}, {canonicalDestination}>`.
Aliases, reference nullable annotations, `dynamic`/`object`, tuple element
names и native-integer syntax поэтому не меняют сообщение одной pair.

Категория распознаёт symbols, а не только имена. Одноимённые `Configure`,
`MapperBuilder`, `Map` и fluent methods стороннего API не принадлежат Morphant
и сами по себе diagnostics не создают.

### 6.31. Поддерживаемая грамматика `Configure`

Configuration level — собственный source-bodied override mapper-а либо
source-bodied override base mapper-а, связанный с ним прямым
`base.Configure(builder)`. Для каждого level Morphant анализирует exact
parameter symbol типа `global::Morphant.MapperBuilder`; совпадение имени
параметра не требуется.

Поддерживаемый flow является декларативной линейной последовательностью:

- block-bodied `Configure` содержит ноль или больше безусловных top-level
  expression statements; expression-bodied `Configure` содержит одну такую
  expression;
- Morphant expression является прямой fluent chain, корнем которой служит
  parameter текущего level-а; root methods могут настраивать mapper и один раз
  перейти через `Map<TSource, TDestination>` к pair builder-у, после чего
  chain содержит только применимые Morphant pair methods;
- отдельный прямой top-level `base.Configure(builder)` соединяет текущий
  level с source-доступным base level; receiver должен быть exact `base`, а
  единственный argument — exact builder parameter;
- круглые скобки и postfix null-forgiving `!` вокруг builder, receiver,
  chain либо argument `base.Configure` прозрачны и не разрывают flow;
- statements и expressions, которые не ссылаются на Morphant builder и не
  управляют достижимостью его chains, категория 4 игнорирует;
- preprocessor `#if` допустим: анализируется уже выбранное compiler-ом syntax
  tree, поэтому отсутствующая ветка не является runtime control flow.

Каждое настоящее использование root либо pair builder-а должно целиком
укладываться в эту грамматику. Builder нельзя сохранять в local, field,
property или tuple, возвращать, передавать helper-у либо delegate-у,
захватывать, разносить его chain по нескольким statements или проводить через
сторонний fluent method. Morphant chain не может выполняться условно,
повторяться либо откладываться через `if`, conditional/switch expression,
loop, `switch`, `try`, local function, lambda, delegate или аналогичный
control flow.

Проверка достижимости не ограничивается родительским syntax node. Например,
в `if (condition) return; builder.Map<A, B>();` registration может быть
пропущена и поэтому нарушает линейный flow. `return` после последней Morphant
chain либо независимый control flow, не влияющий на builder statements,
diagnostic не создаёт.

Arguments и bodies уже распознанных `Construct`, `Members` и `Convert`
callbacks не обходятся категорией 4. Они являются декларативным содержимым
категории 8, даже если ссылаются на внешний builder. Nested `Map` / `Create` /
`Update` markers внутри этих callbacks принадлежат категории 11.

### 6.32. `MORPH0015`: собственный source-bodied `Configure`

Diagnostic публикуется, когда mapper не объявляет собственный exact override
`void Configure(global::Morphant.MapperBuilder)`, доступный Morphant как
block- либо expression-bodied source declaration текущей input compilation.
Унаследованный concrete override не заменяет собственный: configuration
наследуется только через явный `base.Configure(builder)` из нового override.

Legal bodyless `abstract override` также получает diagnostic. Body,
сгенерированный другим source generator-ом, для Morphant недоступен: Roslyn
generators не анализируют outputs друг друга как input syntax, поэтому такая
форма не считается source-bodied override-ом.

Если exact bodyless declaration существует, primary location — identifier
`Configure`. Если собственного exact override нет, primary location —
identifier mapper declaration; additional locations отсутствуют. В `{0}`
передаётся mapper type. Diagnostic дедуплицируется по mapper symbol независимо
от количества его partial declarations.

Полностью malformed попытка объявить override — неверный return/parameter
type, type parameters, `static`, неразрешившийся type либо override unrelated
member — остаётся точной compiler diagnostic и не получает дублирующую
`MORPH0015`. Простое отсутствие override получает `MORPH0015`, даже если
non-abstract mapper одновременно получает `CS0534`.

Без собственного source body Morphant не предполагает registrations и не
генерирует для mapper-а executable contract, construction/member/extension
surfaces либо recovery-stubs. Независимые mapper-ы compilation продолжают
анализироваться.

### 6.33. `MORPH0016`: недоступный body base-конфигурации

Diagnostic публикуется на поддерживаемом прямом
`base.Configure(builder)`, если target override семантически разрешён, но его
block либо expression body отсутствует среди input syntax trees текущей
compilation. В частности, недоступны metadata-only body из referenced assembly
и declaration, ожидающая реализацию от другого source generator-а. Source body
в этой compilation доступен независимо от файла и partial declaration.

Primary location — identifier `Configure` прямого base-call; additional
locations отсутствуют. В `{0}` передаётся declaring base mapper type, в `{1}`
— анализируемый mapper type. Identity diagnostic — анализируемый mapper и
конкретное ребро configuration chain; повторный вызов того же edge не
дублирует `MORPH0016`, а нарушение повторного включения относится к категории
7.

Вызов с alias вместо exact parameter, indirect helper call либо вызов под
неподдерживаемым control flow не образует поддерживаемого base edge и
диагностируется `MORPH0017`, а не каскадным `MORPH0016`. Цикл, несколько
прямых вызовов, несогласованная configuration chain и `IncludeBase` также
остаются категорией 7, если target bodies доступны.

Недоступный body делает effective root settings и полный inherited plan
неизвестными. Все непосредственно известные после категорий 2–3 legal pairs
из доступной части configuration chain сохраняют полный
`ITypeMapper<TSource, TDestination>` contract и свои independently legal DSL
surfaces, но обе операции каждой pair бросают
`MappingConfigurationException`. Это применяется независимо от
`MappingMode`: недоступный base level не позволяет доказать даже отключённую
operation.

Registrations за недоступным edge не угадываются и artifacts не получают.
Структурно исключённые pairs категорий 2–3 не восстанавливаются этим recovery.

### 6.34. `MORPH0017`: unsupported root-builder flow

Diagnostic публикуется для каждого независимого места, где exact root
`MapperBuilder` текущего configuration level-а выходит за границы раздела
6.31. К таким причинам относятся:

- присваивание alias-у, сохранение, возврат, передача helper-у/delegate-у либо
  capture root builder-а;
- вызов Morphant root method не как часть прямой top-level chain;
- сторонний method или extension method внутри chain до перехода через
  `Map<TSource, TDestination>`;
- conditional, repeated, deferred либо иным образом нелинейное выполнение
  root setting, `Map` или прямого `base.Configure`;
- несколько ссылок на root builder внутри одной chain, включая передачу его
  через argument.

Использование результата распознанного `Map` вне pair chain не является
root-escape и относится к `MORPH0018`. Одноимённый сторонний builder либо
метод игнорируется, пока через него фактически не проходит exact Morphant
builder value.

Primary location выбирается в следующем порядке:

1. конкретный identifier root builder-а, который сохраняется, передаётся или
   захватывается;
2. name первого стороннего fluent method на root chain;
3. name первой Morphant invocation, включая `Map`, чья достижимость стала
   условной либо повторяемой;
4. identifier `Configure`, если более узкого builder-related span нет.

Additional locations отсутствуют. Flow-break identity включает mapper,
configuration level и первое syntax location, в котором один root value
покинул поддерживаемый flow. Все дальнейшие обращения через уже
диагностированный alias/helper/delegate являются каскадом и новых diagnostics
не создают. Два независимых escape-а исходного parameter-а получают две
diagnostics.

Root-escape блокирует executable plan всего mapper-а: helper либо deferred
code мог изменить root settings или зарегистрировать неизвестные pairs. Все
exact `MapperBuilder.Map<TSource, TDestination>` invocations, непосредственно
видимые вне helper/local-function/delegate bodies в доступных `Configure`
levels и structurally legal после категорий 2–3, получают полный throwing
contract и independently legal DSL surfaces. Обе операции бросают
`MappingConfigurationException` независимо от `MappingMode`.

Registrations, скрытые внутри helper, local function, lambda или delegate, не
угадываются и artifacts не получают. Независимый mapper compilation не
затрагивается.

### 6.35. `MORPH0018`: unsupported pair-builder flow

Diagnostic публикуется, когда прямой root flow однозначно достигает
authoritative `Map<TSource, TDestination>`, но возвращённый exact
`MapperBuilder<TSource, TDestination>` не остаётся внутри одной поддерживаемой
top-level fluent chain. В частности, ошибочны:

- сохранение результата `Map`, его передача, возврат либо capture;
- продолжение pair configuration через alias в другом statement;
- helper, delegate либо сторонний fluent method между `Map` и Morphant pair
  methods;
- conditional, repeated или deferred pair-chain fragment вне callback bodies.

Primary location — identifier `Map`, если его result покидает текущую fluent
chain; name первого стороннего method, если break происходит внутри chain;
либо конкретная pair-builder reference в остальных формах. Additional
locations отсутствуют.

Identity включает mapper, canonical pair и независимое место flow break.
Дальнейшие uses уже диагностированного alias/helper/delegate не создают
каскад. Несколько независимых breaks одной authoritative pair дают несколько
`MORPH0018`; один break, наблюдаемый несколькими последующими uses, даёт одну.

Recovery pair-local. Затронутая structurally legal pair сохраняет полный
`ITypeMapper<TSource, TDestination>` contract и independently legal DSL
surfaces, но `Create` и `Update` бросают `MappingConfigurationException`
независимо от effective `MappingMode`. Остальные pairs mapper-а сохраняют
собственный исполнимый plan, если их не блокирует другая причина.

Chain поздней duplicate registration, уже полностью отброшенный
`MORPH0013`, не получает `MORPH0018`: первая registration владеет pair plan,
а содержимое последующих chains не анализируется. Root-escape, способный
затронуть неизвестные root settings либо registrations, остаётся независимым
`MORPH0017` даже рядом с duplicate registration.

### 6.36. Ownership соседних категорий

Категория 4 отвечает только за обнаружимость source configuration и движение
двух builder values. После успешного восстановления linear chain она не
проверяет:

- количество и порядок `base.Configure(builder)`, configuration cycles и
  перенос настроек между connected levels — категория 7;
- `IncludeBase`, поиск и совместимость base pair — категория 7;
- состав `Construct` / `Members` / `Convert` одной pair и допустимость их
  сочетания — категория 5;
- значения arguments Morphant settings и их применимость — категория 6;
- переносимость callback bodies, locals, captures и control flow внутри них —
  категория 8;
- construction, members и nested mapping semantics — категории 9–11.

Сторонний fluent method, через который проходит builder value, остаётся
категорией 4, даже если он в итоге возвращает тот же builder type: Morphant не
исполняет configuration code и не может доказать его декларативную
эквивалентность.

### 6.37. Precedence, порядок и suppression

Compilation-wide gate категории 1 и mapper-wide structural errors
`MORPH0005`–`MORPH0008` подавляют категорию 4. `MORPH0015` подавляет
`MORPH0016`–`MORPH0018` того же mapper-а, поскольку анализируемого body нет.

Pair-local structural diagnostics `MORPH0009`–`MORPH0014` не скрывают
независимо доказуемый builder-flow break. Их structural recovery имеет
приоритет: `MORPH0016`–`MORPH0018` не возвращают исключённый executable
contract и не создают DSL surfaces для unsupported root. `MORPH0013`
дополнительно подавляет анализ только отброшенных duplicate chains по разделу
6.35.

Независимые `MORPH0016` и `MORPH0017` могут публиковаться вместе. Mapper-wide
recovery любой из них уже делает все известные operations throwing, но не
скрывает самостоятельный `MORPH0018`: после исправления одной причины вторая
не должна появляться впервые. Diagnostics категорий 5–12, которым нужен
достоверный effective mapping plan, подавляются для соответствующего mapper-а
либо pair; соседние независимо доказуемые structural причины сохраняются.

Publication order — по ID, затем ordinal stable mapper identity,
configuration-level order от derived к base, canonical pair и source
location flow break. Discovery, traversal connected base levels и incremental
invalidation не меняют diagnostic set, messages или locations.

Suppression либо понижение severity не меняет recovery и не разрешает
исполнять непроверенный configuration code. Добавление/удаление source body,
подключение/разрыв base edge и перенос builder use в поддерживаемый либо
неподдерживаемый flow полностью пересчитываются при actualization без
сохранения прежнего gate.

### 6.38. Самостоятельная тестовая матрица категории 4

Unit-категория configuration независимо фиксирует:

- exact descriptors `MORPH0015`–`MORPH0018`: ID, title, category, default
  severity, enabled/configurable flags, message formats и type/contract
  parameters;
- block-bodied, expression-bodied и empty own override; унаследованный
  concrete override, legal bodyless abstract override, missing override,
  body другого generator-а и malformed override attempts с exact
  `MORPH0015`/compiler ownership и locations;
- доступный source base body в том же и другом syntax tree, metadata-only и
  generator-produced body, generic base substitution, exact base-call
  location, edge deduplication и throwing recovery `MORPH0016`;
- несколько безусловных top-level root/pair chains, отдельные root settings,
  direct `base.Configure`, parentheses, null-forgiving `!`, inert unrelated
  statements и compiler-selected `#if` branches как positive grammar matrix;
- semantic exclusion одноимённых сторонних builders и methods;
- root alias, assignment/storage, helper argument, local function, delegate,
  capture, repeated builder argument, third-party fluent method и split root
  flow с exact `MORPH0017` locations и cascade deduplication;
- `if`, conditional/switch expression, loops, `switch`, `try` и early control
  transfer, включая `if (condition) return; Map(...)`, с semantic reachability
  diagnostics;
- напрямую видимые и скрытые registrations при root-escape, complete throwing
  contracts только известных legal pairs, отсутствие guessed artifacts и
  сохранение независимого mapper-а;
- assignment, storage, helper/delegate, alias continuation, сторонний fluent
  method и conditional pair fragment после `Map` с exact pair identity,
  locations и one-diagnostic-per-independent-break cardinality `MORPH0018`;
- pair-local throwing recovery `MORPH0018`, сохранение независимой исполнимой
  pair и обе operation stubs независимо от `MappingMode`;
- отсутствие category-4 обхода callback bodies и правильное сохранение
  ownership категорий 5–11;
- precedence с `MORPH0005`–`MORPH0014`, отсутствие `MORPH0018` в отброшенном
  duplicate chain и совместную публикацию независимых mapper-wide/pair-local
  flow breaks;
- deterministic order, suppression/изменение severity без изменения recovery
  и отсутствие недостоверных downstream diagnostics;
- add/remove/replace own/base body, root escape и pair escape при одном
  сохранённом incremental driver-е.

Package-like integration-категория независимо проверяет:

- mapper без собственного source-bodied override и mapper только с inherited
  override: exact `MORPH0015` и полное отсутствие generated artifacts;
- source-connected и metadata-only base `Configure`: для недоступного body
  сохраняются только известные contracts/surfaces, а реальные вызовы обеих
  operations бросают `MappingConfigurationException`;
- suppressed `MORPH0017`: напрямую видимые pairs имеют throwing contracts,
  hidden helper/delegate registrations не получают artifacts;
- suppressed `MORPH0018`: затронутая pair бросает на обеих operations, а
  независимая pair остаётся исполнимой;
- callbacks с declarative contents не создают category-4 diagnostic, а
  одноимённый сторонний API не влияет на Morphant generation;
- реальное `.editorconfig`/MSBuild suppression или severity override для
  каждой recovery-family без изменения generated artifact set.

### 6.39. Категория 5: общий contract

Категория «Локальная композиция mapping plan» содержит ровно две diagnostics:

| ID | Title | Message format |
|---|---|---|
| `MORPH0019` | `Duplicate mapping plan fragment` | `Mapping plan fragment '{0}' is configured more than once for contract '{1}' in mapper '{2}'.` |
| `MORPH0020` | `Manual and declarative mapping cannot be combined` | `Manual Convert cannot be combined with declarative Construct or Members for contract '{0}' in mapper '{1}'.` |

Для обеих diagnostics действует общий descriptor contract:

- category — `Morphant.Composition`;
- default severity — `Error`;
- diagnostic включена по умолчанию и не имеет `NotConfigurable`;
- description и help link отсутствуют, custom tags пусты;
- анализируется только authoritative local chain первой registration canonical
  pair после успешных structural и builder-flow gates категорий 1–4;
- effective settings, `MappingMode`, `UnmappedMemberValidation` и кажущаяся
  достижимость отдельной `Create`- либо `Update`-ветки не меняют факт
  противоречивой локальной композиции;
- suppression либо изменение severity меняет только compiler presentation и
  не выбирает один из конфликтующих callbacks как скрытый fallback.

Fragment name `{0}` в `MORPH0019` — точное имя семейства `Construct`,
`Members` или `Convert`. Mapping contract форматируется по canonical identity
категории 3 как
`global::Morphant.ITypeMapper<{canonicalSource}, {canonicalDestination}>`, а
mapper type — по fully-qualified nullable-aware display категории 2.

### 6.40. Fragment identity и допустимая локальная композиция

Local fragment — успешно связанный Morphant-вызов `Construct`, `Members` либо
`Convert` в authoritative chain конкретной registration. Разные generated
overloads одного метода принадлежат одному fragment family. Порядок fragments
соответствует порядку выполнения fluent chain слева направо; иные pair methods
между ними этот порядок не меняют.

Допустимы ровно следующие локальные наборы fragments:

- ни одного fragment;
- один `Construct`;
- один `Members`;
- по одному `Construct` и `Members` в любом порядке;
- один `Convert` без `Construct` и `Members`.

Второй и каждый следующий вызов одного family создаёт `MORPH0019`. Наличие
`Convert` вместе хотя бы с одним `Construct` либо `Members` независимо от
порядка создаёт `MORPH0020`.

В категорию 5 входят только invocations, которые semantic model однозначно
связал с настоящим Morphant pair API. Одноимённые сторонние methods
игнорируются. Invocation с неразрешившейся или неоднозначной overload либо с
callback conversion, ошибочность которой уже полностью объясняет C# compiler,
не считается fragment и не получает дублирующую Morphant diagnostic.

Pair-level settings не являются plan fragments; корректность их значений и
применимость к manual/declarative plan относится к категории 6. `IncludeBase`
и перенесённые им `Members` также не считаются local fragments; их
взаимодействие с локальным plan относится к категории 7. Содержимое успешно
связанных callbacks категория 5 не анализирует: переносимость и семантика
`Construct`, `Members`, `Convert` принадлежат категориям 8–11.

### 6.41. `MORPH0019`: duplicate mapping plan fragment

Diagnostic публикуется на втором и каждом следующем локальном invocation
одного fragment family в authoritative chain pair. Primary location —
identifier текущего лишнего `Construct`, `Members` либо `Convert`;
единственная additional location — identifier первого invocation того же
family.

Три `Members` дают две diagnostics: на втором и третьем вызовах, обе со
ссылкой на первый. Смешение разных overloads не образует разные families:
например, два применимых `Construct` с разными delegate signatures всё равно
дают одну diagnostic на втором вызове.

Diagnostic identity включает mapper, authoritative registration, fragment
family и location конкретного лишнего invocation. Одинаковые duplicates в
разных canonical pairs независимы. Chains поздних registrations, уже
отброшенные `MORPH0013`, не анализируются и собственных `MORPH0019` не
получают.

`MORPH0019` и `MORPH0020` являются независимыми причинами. Например, два
`Convert` вместе с `Construct` дают одну duplicate diagnostic на втором
`Convert` и одну mixed diagnostic на pair: исправление любой одной причины не
должно впервые открывать вторую.

### 6.42. `MORPH0020`: manual и declarative fragments

Diagnostic публикуется ровно один раз на authoritative pair, содержащую хотя
бы один локальный `Convert` и хотя бы один локальный `Construct` либо
`Members`. Количество invocations каждого family не увеличивает cardinality
`MORPH0020`; duplicates независимо публикуют собственные `MORPH0019`.

Primary location — identifier первого invocation семейства, появившегося
вторым: manual family содержит `Convert`, declarative family — `Construct` и
`Members`. Поэтому `Construct(...).Convert(...)` указывает на `Convert`, а
`Convert(...).Members(...)` — на `Members`. Если до первого `Convert` уже
встретились и `Construct`, и `Members`, primary остаётся первым `Convert`; если
после `Convert` declarative fragments начинаются с `Construct`, primary — этот
`Construct` независимо от последующего `Members`.

Additional locations содержат identifiers первых участвующих `Construct`,
`Members` и `Convert` в фиксированном порядке `Construct`, `Members`,
`Convert`; отсутствующий family пропускается. Span, совпадающий с primary
location, также сохраняется в additional locations, чтобы дополнительный
список всегда полностью описывал первый локальный состав конфликтующего plan.

В `{0}` передаётся canonical mapping contract, в `{1}` — mapper type.
Diagnostic identity — mapper и authoritative canonical pair. Local `Convert`
не конфликтует в категории 5 только с imported `Members` или иным plan за
`IncludeBase` boundary: такой effective-plan вопрос остаётся категорией 7.

### 6.43. Recovery, precedence, порядок и suppression

Любая `MORPH0019` либо `MORPH0020` делает локальный plan затронутой pair
неисполнимым. Pair сохраняет полный
`ITypeMapper<TSource, TDestination>` contract и independently legal
construction/member/extension surfaces, но обе операции бросают
`MappingConfigurationException`. Recovery одинаков независимо от
`MappingMode`, effective settings и кажущейся применимости только одного
fragment к одной operation.

Morphant не выбирает первый или последний duplicate callback и не отдаёт
приоритет manual либо declarative family. Если одна pair имеет обе
diagnostics, один pair-level throwing recovery применяется без дополнительных
вариантов. Независимые корректные pairs mapper-а сохраняют исполнимые plans.

Compilation-wide gate категории 1, mapper-wide structural gates категории 2,
pair exclusion/unsupported-root recovery категории 3 и mapper-/pair-wide
builder-flow gates категории 4 подавляют недостоверный анализ категории 5 в
своей области. В частности, анализируется только chain первой authoritative
registration после `MORPH0013`; исключённый либо unsupported contract и flow,
который невозможно однозначно восстановить, не получают composition
diagnostics.

Ошибки значений settings, inheritance/effective composition и callback bodies
не переопределяются категорией 5 и сохраняют ownership категорий 6–11.
Downstream diagnostic, которому необходим единый исполнимый local plan,
подавляется как каскад; независимо доказуемая причина соседней категории
остаётся видимой. Точная downstream applicability дополнительно фиксируется в
контракте соответствующей категории.

Publication order — по ID, затем ordinal stable mapper identity, canonical
pair и source location invocation. Для `MORPH0020` одна pair сохраняет одну
diagnostic независимо от traversal order; primary и additional locations
вычисляются из утверждённого fluent order.

Suppression либо понижение severity не меняет throwing recovery, generated
artifact set или анализ независимых pairs. Добавление, удаление, замена,
перестановка либо смена overload plan fragment полностью пересчитывается при
actualization без сохранения прежнего conflict state.

### 6.44. Самостоятельная тестовая матрица категории 5

Unit-категория composition независимо фиксирует:

- exact descriptors `MORPH0019`–`MORPH0020`: ID, title, category, default
  severity, enabled/configurable flags, message formats и fragment/contract/
  mapper parameters;
- отсутствие fragments, один `Construct`, один `Members`, один `Convert` и
  `Construct` + `Members` в обоих порядках как полную positive matrix;
- два и три вызова каждого family, смешение overloads, interleaved pair
  settings, exact primary/first-invocation additional locations и две
  diagnostics для трёх duplicates `MORPH0019`;
- `Convert` + `Construct`, `Convert` + `Members` и все три fragment families в
  каждом значимом порядке, exact first-second-family primary location,
  фиксированный полный additional-location list и ровно одну `MORPH0020` на
  pair;
- совместные duplicate и mixed conflicts, включая два `Convert` +
  `Construct`, с независимой cardinality обоих IDs;
- semantic exclusion одноимённых сторонних methods, compiler-owned
  unresolved/ambiguous overloads и invalid callback conversions;
- анализ только первой authoritative registration, отсутствие diagnostics в
  отброшенных `MORPH0013` chains и независимую pair с собственным plan;
- exclusion `IncludeBase` и imported `Members` из local fragment set,
  ownership pair settings и отсутствие обхода callback contents;
- полный generated result recovery: complete mapper contract и legal DSL
  surfaces, обе throwing operations независимо от `MappingMode`, отсутствие
  выбранного first/last/manual/declarative fallback и сохранение независимой
  исполнимой pair;
- precedence с gates `MORPH0001`–`MORPH0018`, подавление недостоверных
  downstream diagnostics и сохранение независимо доказуемых соседних причин;
- deterministic order, suppression/изменение severity без изменения recovery
  и generated artifact set;
- add/remove/reorder/replace каждого fragment family и overload при одном
  сохранённом incremental driver-е.

Package-like integration-категория независимо проверяет:

- suppressed duplicate каждого fragment family: mapper и DSL surfaces
  компилируются, обе operations бросают `MappingConfigurationException`, ни
  первый, ни последний callback не исполняется;
- suppressed manual/declarative conflict в обоих порядках: обе operations
  бросают без выбора family, а независимая pair того же mapper-а остаётся
  исполнимой;
- `Construct` + `Members` остаются исполнимой declarative composition, а
  local fragment рядом с imported plan не получает ложную category-5
  diagnostic;
- реальное `.editorconfig`/MSBuild suppression или severity override для
  duplicate и mixed recovery без изменения generated artifact set.

## 7. Реализация и тесты

Этап 3 выполняется вертикальными срезами по согласованным категориям:
detection, diagnostic publication, locations, deduplication, recovery,
самостоятельные unit- и integration-тесты и соответствующая документация
входят в один coherent change.

Каждая тестовая категория должна независимо проверять наличие и отсутствие
diagnostics, точные ID/severity/message/location, подавление каскадов,
детерминизм, полный generated result и компилируемость либо исполняемость
recovery-кода. Тесты одной категории не считаются доказательством другой.

## 8. Финальный аудит

Этап 4 проверяет план и реализацию в обе стороны:

- каждая diagnostic каталога реализована, документирована и самостоятельно
  протестирована;
- каждая diagnostic production-кода присутствует в каталоге;
- каждое ошибочное состояние core v0 либо имеет compile-time diagnostic, либо
  явно отнесено к C# compiler, analyzer host или runtime failure;
- не осталось молчаливых отказов, скрытых fallback, лишних каскадов и
  недетерминированного порядка;
- IDs, categories, severity, terminology, locations и recovery согласованы;
- generated contracts, public XML, conceptual docs, roadmap и tests описывают
  одну семантику.

Usage analyzers над вызывающим кодом, включая warning об игнорировании
авторитетного результата `Update`, остаются post-v0 и в этот финальный аудит
source-generator diagnostics не входят.
