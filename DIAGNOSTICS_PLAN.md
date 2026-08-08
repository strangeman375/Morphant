# План compile-time diagnostics Morphant v0

Дата составления: 7 августа 2026 года.

Последнее обновление: 8 августа 2026 года.

Статус: работа приостановлена до пользовательского принятия реализованного
callback API и последующей coherent revision диагностического каталога.

Этот документ является отдельным рабочим планом этапа 23 из
[`MAPPING_API_IMPLEMENTATION_PLAN.md`](MAPPING_API_IMPLEMENTATION_PLAN.md).
Нормативную mapping-семантику задаёт
[`MAPPING_API_DESIGN.md`](MAPPING_API_DESIGN.md), а уже реализованную границу
runtime failures и recovery-stubs — раздел 14.2 того же документа и
[`docs/observable-failures.md`](docs/observable-failures.md).

После согласованной API-ревизии callback surface состоит из structured
`Construct` / `Resolve` / `Members`, runtime `ConstructUsing` /
`ResolveUsing` / `Convert` и compile-time `MappingContextMarker`. Вложенный
`ByFactory` и direct-формы `Construct` / `Resolve` удаляются. Нормативный
контракт также ограничивает read-only member proxy только применимыми
non-opaque reference-type nested destinations. Оба уточнения зафиксированы в
`MAPPING_API_DESIGN.md` и отдельном разделе
`MAPPING_API_IMPLEMENTATION_PLAN.md`.

Категории 1, 4, 5, 7 и черновик категории 8 ниже были составлены до финальной
формы этого surface. Их callback-зависимые части больше не являются готовым
контрактом: после пользовательского принятия API они пересматриваются одним
coherent catalog revision. Принятые IDs и общие диагностические законы
категорий 1–7 сохраняются до такого пересмотра, но категория 8 снята со статуса
`ожидает ревью`, а этап 3 заблокирован.

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
| 2 | Полный каталог и точный контракт каждой diagnostic по одной категории за раз | Приостановлен: категории 1–7 требуют callback-синхронизации; категория 8 возвращена в черновик; категории 9–12 не начаты |
| 3 | Реализация, recovery, самостоятельные unit- и integration-тесты вертикальными срезами | Заблокирован завершением пользовательского API и этапом 2 |
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
| 7 | Наследование конфигурации и `IncludeBase` | `base.Configure(builder)`, configuration chain, typed `IncludeBase`, level-aware поиск base pair, совместимость, повторное включение и переносимость effective inherited rules. |
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

Перед назначением шестой группы 8 августа 2026 года тем же способом проверены
`MORPH0021`–`MORPH0023`. Внешних публичных .NET/Roslyn diagnostics с этими ID
не найдено.

Перед назначением седьмой группы 8 августа 2026 года тем же способом проверены
`MORPH0024`–`MORPH0028`. Внешних публичных .NET/Roslyn diagnostics с этими ID
не найдено. Найденные case-insensitive web-совпадения `Morph0026`–`Morph0028`
являются specimen identifiers из биологических каталогов, а не analyzer
diagnostics, поэтому коллизией не считаются.

Перед назначением восьмой группы 8 августа 2026 года тем же способом проверены
`MORPH0029`–`MORPH0033`. Внешних публичных .NET/Roslyn diagnostics с этими ID
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

- количество `base.Configure(builder)` и перенос настроек между connected
  levels — категория 7;
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
и любые перенесённые им plan fragments также не считаются local fragments; их
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
не конфликтует в категории 5 с любым imported plan за `IncludeBase` boundary:
model precedence такого effective plan остаётся категорией 7.

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
- exclusion `IncludeBase` и всех imported plan fragments из local fragment
  set, ownership pair settings и отсутствие обхода callback contents;
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

### 6.45. Категория 6: общий contract

Категория «Значения и применимость settings» содержит ровно три diagnostics:

| ID | Title | Message format |
|---|---|---|
| `MORPH0021` | `Invalid mapping setting value` | `Mapping setting '{0}' must be a supported compile-time constant.` |
| `MORPH0022` | `Invalid MSBuild mapping setting value` | `MSBuild property '{0}' must name a supported mapping setting value.` |
| `MORPH0023` | `Mapping setting is not applicable` | `Mapping setting '{0}' is not applicable to {1} for contract '{2}' in mapper '{3}'.` |

Для всех трёх diagnostics действует общий descriptor contract:

- category — `Morphant.Settings`;
- default severity — `Error`;
- diagnostic включена по умолчанию и не имеет `NotConfigurable`;
- description и help link отсутствуют, custom tags пусты;
- анализируются шесть settings core v0: `MappingMode`,
  `NullSourceHandling`, `NullDestinationHandling`, `ConstructorSelection`,
  `MemberSelection` и `UnmappedMemberValidation`;
- suppression либо изменение severity меняет только compiler presentation и
  не превращает invalid value в наследуемый `Default`, не делает
  неприменимую policy активной и не меняет recovery.

В `{0}` передаётся точное публичное имя setting. Для `MORPH0022` это имя
compiler-visible MSBuild property с префиксом `Morphant`. Mapping contract в
`MORPH0023` форматируется по canonical identity категории 3 как
`global::Morphant.ITypeMapper<{canonicalSource}, {canonicalDestination}>`, а
mapper type — по fully-qualified nullable-aware display категории 2.

### 6.46. Effective value, precedence и допустимая grammar

Effective value каждой setting разрешается независимо в точном порядке:

1. текущая pair;
2. pair, подключённые через typed `IncludeBase`, в порядке effective
   composition;
3. root текущего mapper-а;
4. roots подключённых base mapper-ов от ближайшего к дальнему;
5. compiler-visible MSBuild property assembly-level;
6. library default.

На каждом C#-уровне учитывается только последний успешно связанный вызов
конкретной setting, включая последний `Default`. Для `MappingMode` map-level
origin является argument authoritative `Map<TSource, TDestination>`; omitted
optional argument создаёт корректный implicit `Default`, но не explicit
origin. Root-level повторные calls следуют тому же last-call-wins закону.

Корректный `Default` продолжает поиск на следующем менее конкретном уровне.
Invalid final value текущего уровня, напротив, останавливает поиск: Morphant не
подменяет ошибку outer value или library default. Более конкретное корректное
non-`Default` значение полностью перекрывает invalid outer origin. Более
конкретный `Default` снова делает outer origin достижимым. Если invalid origin
перекрыт на всех применимых pair/path, diagnostic на него не публикуется.

C# setting argument корректен только после успешного C# binding и при
выполнении следующей grammar:

- expression является compile-time constant соответствующего enum type;
- `MappingMode` не содержит битов, не принадлежащих объявленным атомарным
  флагам. Атомарным считается объявленное nonzero значение с одним битом;
  `Default = 0` и любая комбинация таких флагов допустимы независимо от
  наличия отдельного именованного composite member;
- остальные enum-ы допускают только значения явно объявленных members,
  включая `Default`;
- non-constant local/property/method result, отрицательное либо иное
  неподдерживаемое numeric value и неизвестный `MappingMode` bit являются
  invalid values.

Таким образом, текущие `MappingMode.Create`, `Update`, `CreateAndUpdate` и
эквивалентные constant expressions допустимы. Формулировка намеренно не
фиксирует numeric range `0–3`: после появления нового атомарного flag, например
`Project`, его комбинации с уже объявленными flags должны стать допустимыми
без обязательного именованного composite member.

Invocation с неразрешившейся или неоднозначной overload, несовместимым
argument type либо иной ошибкой binding/conversion, которую точно и достаточно
объясняет C# compiler, не получает дублирующий `MORPH0021`. Вызов, успешно
связанный с Morphant API, но получивший C#-legal non-constant либо unsupported
enum constant, принадлежит Morphant.

Assembly-level участвует ровно через следующие compiler-visible properties:

| Setting | MSBuild property |
|---|---|
| `MappingMode` | `MorphantMappingMode` |
| `NullSourceHandling` | `MorphantNullSourceHandling` |
| `NullDestinationHandling` | `MorphantNullDestinationHandling` |
| `ConstructorSelection` | `MorphantConstructorSelection` |
| `MemberSelection` | `MorphantMemberSelection` |
| `UnmappedMemberValidation` | `MorphantUnmappedMemberValidation` |

Значение property обрезается по краям и сопоставляется
case-insensitively с точным именем объявленного enum member. Missing, empty и
`Default` продолжают precedence chain. Numeric forms, comma-separated flags,
qualified enum names и иные строки не принимаются; для текущего composite
mode используется имя `CreateAndUpdate`. Итоговый property value уже разрешён
MSBuild imports до запуска generator-а, поэтому повторные определения в
`.props`/`.targets` не являются отдельными origins Morphant.

Library defaults остаются `MappingMode.CreateAndUpdate`,
`NullSourceHandling.ReturnNull`, `NullDestinationHandling.Create`,
`ConstructorSelection.Unambiguous`, `MemberSelection.Auto` и
`UnmappedMemberValidation.None`.

### 6.47. Статическая применимость settings

Invalid value получает `MORPH0021` либо `MORPH0022` только когда его origin
становится effective хотя бы для одной применимой authoritative pair/path.
Применимость определяется статической mapping model и operation capability, а
не конкретным runtime argument:

| Setting | Declarative mapping | Manual `Convert` |
|---|---|---|
| `MappingMode` | Применяется ко всему contract и задаёт enabled operations | Единственная применимая effective setting |
| `NullSourceHandling` | Применяется ко всем enabled operations до mapping plan | Не применяется |
| `NullDestinationHandling` | Применяется только к enabled `Update` | Не применяется |
| `MemberSelection` | Применяется ко всем enabled operations | Не применяется |
| `ConstructorSelection` | Применяется только к reachable convention либо explicit `ByConvention` creation path | Не применяется |
| `UnmappedMemberValidation` | Применяется только к category-12 warning-анализу effective declarative plan | Не применяется |

Nullability source/destination и фактическое отсутствие `null` во время
исполнения не скрывают invalid null policy: наличие соответствующей enabled
declarative operation является достаточной статической применимостью.
Аналогично отсутствие convention member candidates не делает
`MemberSelection` либо `UnmappedMemberValidation` неприменимыми. Для
`ConstructorSelection` необходим именно reachable convention/`ByConvention`
creation path; полностью explicit branches и Update существующего destination
от этой setting не зависят.

Invalid `MappingMode` не позволяет доказать enabled declarative operations,
поэтому зависимые invalid values той же pair не получают diagnostics только
через этот недостоверный path. Если тот же origin достигает другой pair с
валидным effective `MappingMode`, он публикуется один раз по обычным правилам.
Model-level неприменимость explicit setting по `MORPH0023` от валидности
`MappingMode` не зависит.

Несколько независимо effective invalid settings при доказанной применимости
публикуются совместно. Execution order null handling, construction и members
не превращает одну invalid policy в объяснение другой. Полностью перекрытые,
manual no-effect, operation-disabled и explicit-only creation origins
diagnostics значений не получают.

### 6.48. `MORPH0021`: invalid C# setting argument

Diagnostic публикуется на final C# setting invocation, чьё успешно связанное
argument expression не удовлетворяет grammar раздела 6.46 и становится
effective хотя бы на одном применимом path.

Primary location — полное argument expression, а не identifier метода или
всего invocation. Additional locations отсутствуют: affected pairs не
размножаются ни как diagnostics, ни как additional spans. В `{0}` передаётся
имя соответствующей setting, например `MappingMode` или
`ConstructorSelection`.

Diagnostic identity — setting и source origin, заданный syntax tree и span
final argument expression. Один root либо included-base origin, достигший
нескольких pairs, generic substitutions или derived mapper-ов, даёт одну
diagnostic. Одинаковые invalid expressions в разных invocations являются
разными origins и диагностируются независимо.

Ранее перекрытый вызов того же setting на одном C#-уровне не является final
origin и не диагностируется, даже если его argument invalid. Chain поздней
registration, отброшенный `MORPH0013`, недостоверный builder flow и
неприменимый effective path также не создают `MORPH0021`.

Если тот же final explicit map-level invocation принципиально неприменим к
выбранной model, публикуется только `MORPH0023`: удаление вызова полностью
исправляет причину, поэтому дополнительный invalid-value diagnostic был бы
каскадом.

### 6.49. `MORPH0022`: invalid MSBuild setting value

Diagnostic публикуется один раз на compiler-visible MSBuild property, чьё
final normalized value не удовлетворяет grammar раздела 6.46 и достигает хотя
бы одной применимой pair/path после всех более конкретных C# levels.

Primary location — `Location.None`, additional locations отсутствуют:
`AnalyzerConfigOptionsProvider` сообщает generator-у final property name и
value, но не source span исходного `.csproj`, `.props` либо `.targets`. В `{0}`
передаётся точное имя property без `build_property.`, например
`MorphantMappingMode`.

Diagnostic identity — имя property в compilation. Один invalid property,
затронувший любое количество mapper-ов и pairs, даёт одну diagnostic. Если все
применимые paths имеют более конкретный корректный override либо setting
везде неприменима, `MORPH0022` отсутствует. Несколько invalid properties
диагностируются независимо в ordinal property-name order внутри одного ID.

Отсутствующее, empty и `Default` значение не являются invalid; они продолжают
поиск library default. Morphant не пытается восстановить исходный MSBuild
import или публиковать отдельную diagnostic на каждое промежуточное
переопределение property.

### 6.50. `MORPH0023`: explicit setting не применяется к model

Diagnostic публикуется для final explicit current-pair setting, которую
выбранная mapping model принципиально не исполняет:

- у pair с локальным `Convert` неприменимы `NullSourceHandling`,
  `NullDestinationHandling`, `ConstructorSelection`, `MemberSelection` и
  `UnmappedMemberValidation`;
- у direct-construction destination неприменима explicit
  `ConstructorSelection`;
- `MappingMode` применим к обеим models и `MORPH0023` не получает;
- explicit `Default` тоже является неприменимой setting: само наличие
  pair-level policy противоречит выбранной model, даже если значение
  продолжило бы inheritance.

Root, connected-base-root, assembly и inherited pair settings в этих случаях
остаются безвредными no-op: один уровень может обслуживать другие declarative
pairs. `MORPH0023` относится только к final explicit setting текущей pair.
Перекрытый более поздним вызовом local invocation не диагностируется.

Diagnostic публикуется один раз на каждую неприменимую final setting, а не
один раз на pair. Поэтому пять explicit non-`MappingMode` settings рядом с
`Convert` дают пять `MORPH0023`. Invalid value того же invocation не добавляет
`MORPH0021`.

Primary location — identifier setting-вызова. Единственная additional
location для manual model — identifier authoritative local `Convert`; для
direct-construction model — соответствующий destination type argument
authoritative `Map<TSource, TDestination>`. В `{1}` передаётся соответственно
`manual Convert` либо `direct construction`; `{2}` и `{3}` содержат canonical
contract и mapper type.

Diagnostic identity включает mapper, authoritative canonical pair, setting и
final invocation. Одинаковая setting в разных pairs независима. Imported
setting, взаимодействие local и inherited plan models и иные вопросы
effective composition остаются категорией 7; callback содержимое категория 6
не анализирует.

### 6.51. Recovery, precedence, порядок и suppression

`MORPH0021` и `MORPH0022` используют одинаковый policy-specific recovery для
каждого affected path:

| Invalid effective setting | Recovery |
|---|---|
| `MappingMode` | Обе operations полного `ITypeMapper<,>` contract бросают `MappingConfigurationException` |
| `NullSourceHandling` | Все enabled declarative operations бросают `MappingConfigurationException`; disabled operation сохраняет обычный `MappingOperationNotSupportedException` |
| `NullDestinationHandling` | Enabled declarative `Update` бросает `MappingConfigurationException`; `Create` сохраняется, а disabled `Update` остаётся operation-not-supported |
| `MemberSelection` | Все enabled declarative operations бросают `MappingConfigurationException` |
| `ConstructorSelection` | Только reachable convention/`ByConvention` creation paths бросают `MappingConfigurationException`; explicit creation branches и Update существующего destination сохраняются |
| `UnmappedMemberValidation` | Runtime mapping не меняется; category-12 warning-анализ affected plan не выполняется |

Path-sensitive `ConstructorSelection` recovery распространяется и на
no-previous branch `Update`, открытый `NullDestinationHandling.Create`, но не
требует включённого public `Create`. Если операция содержит как explicit, так
и convention creation branches, корректные branches остаются исполнимыми, а
throw возникает только при фактическом входе в invalid convention path.

Несколько invalid settings могут публиковать несколько diagnostics, даже если
их recovery сходится в одну throwing operation. Morphant не исполняет
недоступный callback и не выбирает outer value как fallback; одна operation
всё равно бросает один детерминированный `MappingConfigurationException`.
Независимые operations и pairs сохраняют исполнимые plans.

Любая `MORPH0023` делает всю затронутую pair неисполнимой. Pair сохраняет
полный `ITypeMapper<TSource, TDestination>` contract и independently legal DSL
surfaces, но обе operations бросают `MappingConfigurationException`
независимо от effective `MappingMode`. Неприменимая setting не удаляется
молча и не переключает manual/direct mapping на declarative либо convention
fallback.

Compilation-wide gate категории 1, mapper-wide structural gates категории 2,
pair exclusion/unsupported-root recovery категории 3, mapper-/pair-wide
builder-flow gates категории 4 подавляют недостоверный category-6 анализ в
своей области. Неисполнимый local plan категории 5 подавляет только
applicability, для которой необходимо выбрать единую manual/declarative model;
независимо доказуемый invalid setting origin сохраняет category-6 ownership.
Category-7 failure configuration chain либо `IncludeBase` подавляет только
анализ settings, который зависит от этой недостоверной composition;
независимо доказуемый local origin сохраняется.

`MORPH0023` имеет precedence над `MORPH0021` для одного invocation. Invalid
`MappingMode` подавляет зависимый reachability-анализ остальных effective
values по правилам раздела 6.47, но не скрывает независимо доказуемую
model-level `MORPH0023`. Ошибки callbacks, construction, members и nested
mapping не переопределяются категорией 6 и сохраняют ownership категорий
8–11. `UnmappedMemberValidation` управляет только категорией 12.

Publication order — по ID. Внутри `MORPH0021` origins упорядочиваются по
source location, затем setting name; `MORPH0022` — по ordinal property name;
`MORPH0023` — по ordinal stable mapper identity, canonical pair, setting name
и source location. Origin-based deduplication не зависит от traversal order.

Suppression либо понижение severity не меняет effective-value resolution,
recovery, generated artifact set или анализ независимых pairs. Добавление,
удаление, перестановка, замена argument, изменение MSBuild property,
`MappingMode`, mapping model либо capability полностью actualizes diagnostics
и recovery без сохранения прежнего invalid state.

### 6.52. Самостоятельная тестовая матрица категории 6

Unit-категория settings diagnostics независимо фиксирует:

- exact descriptors `MORPH0021`–`MORPH0023`: ID, title, category, default
  severity, enabled/configurable flags, message formats и параметры;
- полную C# grammar всех шести settings: каждый declared enum member,
  `Default`, constant aliases/expressions, non-constant values, negative и
  unknown numeric values;
- `MappingMode` atomic/composite law без numeric-range assumption: zero,
  каждый объявленный atomic flag, их выраженная комбинация и unknown bits;
- successful Morphant binding против compiler-owned unresolved/ambiguous
  overload, argument conversion и одноимённого стороннего API;
- last-call-wins на map/root levels, включая invalid earlier call, final
  `Default`, invalid final value без outer fallback и более конкретный
  корректный override invalid root/assembly value;
- полную precedence matrix current pair -> included base pair -> current root
  -> connected roots -> assembly -> library default для каждого setting;
- exact argument-expression location `MORPH0021`, отсутствие additional
  pair locations и одну diagnostic на root/included origin при нескольких
  pairs, generic substitutions и derived mapper-ах;
- все шесть MSBuild properties: trimmed case-insensitive declared names,
  missing/empty/`Default`, numeric/qualified/comma/unknown forms,
  `Location.None`, одна diagnostic на property и отсутствие fan-out;
- статическую applicability matrix declarative/manual, enabled/disabled
  operations, nullability-independent null policies, convention/explicit/
  `ByConvention` construction paths и category-12-only
  `UnmappedMemberValidation`;
- полностью перекрытые и inactive origins без diagnostic, один origin с
  несколькими affected paths и совместную публикацию нескольких независимо
  invalid effective settings;
- все пять запрещённых explicit settings у `Convert`, explicit
  `ConstructorSelection` у direct destination, `Default`, last-call-wins,
  одну `MORPH0023` на setting и отсутствие её у inherited/root policies;
- exact primary/additional locations и model/contract/mapper parameters
  `MORPH0023`, а также только `MORPH0023` для invalid неприменимого
  invocation;
- полный generated recovery каждой policy: обе invalid-mode stubs,
  operation-specific null/member stubs, path-sensitive constructor failure,
  сохранённый runtime plan без category-12 warnings и полный pair recovery
  `MORPH0023`;
- precedence с `MORPH0001`–`MORPH0020`, category-7 composition boundary,
  сохранение независимо доказуемых причин и отсутствие downstream cascade;
- deterministic order, suppression/изменение severity без изменения recovery
  и generated artifact set;
- actualization каждого C# origin, MSBuild property, override, operation gate,
  mapping model и destination capability при одном сохранённом incremental
  driver-е.

Package-like integration-категория независимо проверяет:

- suppressed invalid C# value каждой recovery-family: complete mapper и legal
  DSL surfaces компилируются, недоступные paths бросают
  `MappingConfigurationException`, а сохранённые operations реально
  исполняются;
- invalid compiler-visible MSBuild property с `Location.None`, реальным
  override на одной pair и origin-based deduplication на другой;
- manual `Convert` с каждой suppressed неприменимой setting и direct
  destination с `ConstructorSelection`: обе operations бросают, callbacks не
  выполняются, независимая pair остаётся исполнимой;
- invalid `UnmappedMemberValidation`: runtime mapping исполняется, а
  category-12 warning-анализ affected plan отсутствует;
- реальное `.editorconfig`/MSBuild suppression или severity override для всех
  трёх IDs без изменения generated artifact set и effective recovery.

### 6.53. Категория 7: общий contract

Категория «Наследование конфигурации и `IncludeBase`» содержит ровно пять
diagnostics:

| ID | Title | Message format |
|---|---|---|
| `MORPH0024` | `Duplicate base configuration call` | `Base configuration is included more than once in Configure of mapper '{0}'.` |
| `MORPH0025` | `Duplicate IncludeBase call` | `IncludeBase is configured more than once for contract '{0}' in mapper '{1}'.` |
| `MORPH0026` | `Included mapping pair not found` | `Included mapping contract '{0}' was not found for contract '{1}' in mapper '{2}'.` |
| `MORPH0027` | `Included mapping type is incompatible` | `Current {0} type '{1}' is not assignable to included {0} type '{2}' for contract '{3}' in mapper '{4}'.` |
| `MORPH0028` | `Inherited mapping expression is inaccessible` | `Inherited {0} expression for contract '{1}' cannot be accessed from mapper '{2}'.` |

Для всех пяти diagnostics действует общий descriptor contract:

- category — `Morphant.Inheritance`;
- default severity — `Error`;
- diagnostic включена по умолчанию и не имеет `NotConfigurable`;
- description и help link отсутствуют, custom tags пусты;
- анализируется только source-visible configuration текущей compilation;
  cross-assembly typed `IncludeBase` остаётся post-v0, а недоступный body
  connected base mapper-а принадлежит `MORPH0016`;
- effective settings, `MappingMode`, `UnmappedMemberValidation` и
  достижимость отдельной operation не скрывают structural composition error;
- suppression либо изменение severity меняет только compiler presentation и
  не выбирает первый edge, не игнорирует ошибочный edge и не делает
  недоступное inherited expression исполнимым.

Mapping contracts в messages форматируются по canonical identity категории 3
как `global::Morphant.ITypeMapper<{canonicalSource},
{canonicalDestination}>`, mapper types — по fully-qualified nullable-aware
display категории 2. `{0}` в `MORPH0027` равно `source` либо `destination`, а
типы `{1}` и `{2}` используют соответствующий fully-qualified display.
Fragment name `{0}` в `MORPH0028` — точное имя семейства `Construct`,
`Members` либо `Convert`.

Категория 7 связывает уже распознанные configuration levels и authoritative
pairs. Она не исполняет configuration code, не ищет application-wide runtime
registrations и не превращает одноимённые methods стороннего API в Morphant
edges.

### 6.54. Configuration levels, composition nodes и lookup

Configuration level — source-bodied `Configure` конкретного mapper type,
доступный по законам категории 4. Один поддерживаемый прямой
`base.Configure(builder)` соединяет level с семантически выбранным base
override. Вызовы на разных levels образуют нормальную connected chain и не
считаются повторами; второй и каждый следующий прямой base-call внутри одного
level создаёт `MORPH0024`.

Composition node имеет identity
`(constructed mapper level, authoritative canonical pair)`. Constructed level
учитывает подстановку generic type arguments, поэтому, например,
`BaseMapper<int>` и `BaseMapper<string>` являются разными узлами effective
graph, хотя используют один source `Configure` definition.

Единственный успешно связанный
`IncludeBase<TBaseSource, TBaseDestination>()` задаёт requested canonical pair
и effective edge. Lookup выполняется в точном порядке:

1. исключить только текущий composition node;
2. найти requested exact pair среди остальных authoritative registrations
   текущего constructed level независимо от declaration order;
3. при отсутствии совпадения пройти connected base levels от ближайшего к
   дальнему и выбрать первое exact совпадение;
4. не учитывать chains поздних duplicate registrations, уже отброшенные
   `MORPH0013`.

Исключается узел, а не canonical pair как таковая. Поэтому локальная
`Map<Order, OrderDto>()` не находит саму себя, но может найти
`Order -> OrderDto` на connected base level. Если другой current-level
кандидат и connected candidate существуют одновременно, current-level
кандидат имеет приоритет; exact same-pair inheritance всегда идёт к другому
mapper-level.

Если candidate не найден, публикуется `MORPH0026` и compatibility не
проверяется. Отдельной diagnostic для «неподдерживаемого type argument
`IncludeBase`» нет:

- зарегистрированная requested pair считается найденной, даже если её
  registration сама получает `MORPH0011` либо `MORPH0012`; category-3 origin
  остаётся единственной первичной причиной, а `MORPH0026` и `MORPH0027` не
  добавляются;
- если requested pair вообще не зарегистрирована, публикуется `MORPH0026`
  независимо от того, являлись бы указанные roots поддерживаемыми в отдельном
  `Map<,>`;
- type argument, который уже отвергнут C# binding/conversion, остаётся только
  compiler diagnostic и не образует успешно связанный IncludeBase edge.

Для найденной structurally legal pair отдельно проверяются source и
destination relations. Current type должен допускать существующую v0
base-type conversion к соответствующему included type. Нарушение каждой
relation получает собственную `MORPH0027`; отсутствие нарушения обеих
relations открывает composition.

Отдельной cycle diagnostic нет. Внутри одного level совместимый edge идёт от
current types к равным либо базовым included types; после исключения текущего
узла хотя бы одна координата strict. Между levels edge всегда идёт вверх по
ациклической C# base-chain, включая exact same-pair. Поэтому совместимый v0
graph ацикличен по построению. Same-pair без подходящего ancestor получает
`MORPH0026`, обратное несовместимое ребро — `MORPH0027`, а циклическая
C#-иерархия полностью принадлежит compiler-у.

### 6.55. Effective inherited plan и model precedence

Успешный IncludeBase edge всегда импортирует все map-level settings candidate
pair и структуру её дальнейшей effective IncludeBase chain. Settings
разрешаются по полному порядку категории 6:

`current pair -> included pairs -> current root -> connected roots ->
assembly -> library default`.

Импорт mapping plan зависит от отношения current и candidate nodes:

| Relation | Импортируемый plan |
|---|---|
| Cross-pair, например `Dog -> DogDto` из `Animal -> AnimalDto` | Effective `Members`; `Construct` и `Convert` не импортируются, conventions и constructor selection вычисляются заново для current pair |
| Exact same-pair на connected base level | Весь applicable effective plan, включая `Construct` либо `Convert` и `Members`, без casts и callback adapters |

Exact same-pair не переносит выбранный runtime result через type boundary:
source и destination types совпадают, поэтому inherited callbacks сохраняют
исходные delegate contracts. Cross-pair никогда не пытается привести result
base `Construct`/`Convert` к более конкретному destination и не меняет
поведение в зависимости от формы structured/direct/factory callback-а.

Локальные fragments разрешают model после импорта так:

| Local plan | Effective behavior |
|---|---|
| Нет локальных `Construct`, `Members`, `Convert` | Exact same-pair полностью сохраняет inherited plan; cross-pair использует imported `Members` и заново построенные current conventions/construction |
| Локальный `Convert` | Полностью заменяет inherited mapping plan; imported settings остаются в precedence chain, но неприменимые manual policies следуют категории 6 |
| Локальный `Construct` и/или `Members` | Выбирает declarative model и отбрасывает inherited `Convert` |
| Declarative plan с обеих сторон | Inherited `Construct` является fallback, local `Construct` его перекрывает; `Members` объединяются по destination member с локальным приоритетом |

Imported и local `Members` объединяются независимо от формы overload-а.
Local expression, `Auto()` либо `Ignore()` перекрывает inherited rule того же
destination member; conventions заполняют только остаток. Dependencies
строятся заново для оставшихся effective rules. Imported fragments не
становятся local fragments категории 5: inherited `Construct` рядом с local
`Construct` не является duplicate, а намеренно отброшенный inherited
`Convert` рядом с local declarative plan не создаёт mixed-model diagnostic.

Composition транзитивна, но каждый edge переносит только свой effective
slice. Поэтому ошибка base plan влияет на consumer только если consumer
действительно сохраняет соответствующий slice:

- cross-pair consumer не зависит от base `Construct`/`Convert` и их ошибок;
- local `Convert` отбрасывает imported declarative plan;
- local declarative plan отбрасывает inherited `Convert`, а локально
  перекрытый `Construct` или member rule удаляет заменённый slice;
- invalid included settings сохраняют ownership и policy-specific recovery
  категории 6;
- ambiguity либо invalid composition оставшегося imported slice делает
  transitive consumer неисполнимым, но origin diagnostic не размножается по
  каждому consumer-у.

Только после model precedence проверяется доступность оставшихся inherited
callbacks из конечного mapper-а. Effective `Construct`, `Members` и `Convert`
испускаются в его generated partial type. Private base mapper members, явный
`base.` и иные references, недоступные по обычным C# rules из target mapper-а,
создают `MORPH0028`. Полностью перекрытый или отброшенный callback и его
dependencies не проверяются и diagnostic не получают. Остальная
переносимость callback syntax, captures и declarative grammar принадлежит
категории 8.

### 6.56. `MORPH0024`: повторный `base.Configure`

Diagnostic публикуется на втором и каждом следующем успешно распознанном
прямом `base.Configure(builder)` одного source configuration level. Primary
location — identifier `Configure` текущего лишнего call; единственная
additional location — identifier первого прямого base-call этого level.

Три прямых вызова дают две diagnostics на втором и третьем, обе со ссылкой на
первый. Один base-call в каждом из нескольких successive overrides является
валидной chain и diagnostics не создаёт. Parentheses, null-forgiving и
statement-/expression-bodied формы сохраняют identity вызова по grammar
категории 4.

Diagnostic identity — declaring source configuration level и location
конкретного лишнего invocation. Generic level, достигнутый несколькими
constructed mapper-ами и substitutions, даёт одну origin diagnostic, а не
fan-out. В `{0}` передаётся declaring mapper definition этого source level.

Неподдерживаемый indirect/conditional builder flow остаётся `MORPH0017` и не
считается прямым base-call. Если единственный target body недоступен,
публикуется `MORPH0016`; повтор того же source edge независимо добавляет
`MORPH0024`, но не размножает `MORPH0016`.

Первый call служит только location anchor. Recovery не исполняет его один раз
как fallback и не игнорирует последующие calls.

### 6.57. `MORPH0025`: повторный `IncludeBase`

Diagnostic публикуется на втором и каждом следующем успешно связанном
Morphant `IncludeBase` authoritative pair независимо от generic arguments
вызовов. Primary location — identifier текущего лишнего `IncludeBase`;
единственная additional location — identifier первого вызова pair.

Три вызова дают две diagnostics. Вызовы на разных authoritative pairs либо на
разных mapper-level-ах независимы. Одноимённый сторонний method и invocation,
который уже полностью отвергнут C# binding, не участвуют в cardinality.

Diagnostic identity — source mapper, authoritative registration и location
лишнего invocation. Generic source pair, достигнутая несколькими constructed
consumers, даёт одну origin diagnostic. Mapping contract `{0}` отражает
source-declared authoritative pair, mapper `{1}` — её declaring mapper.

При нескольких calls Morphant не выбирает первый, последний или совпадающий
по type arguments edge. Поэтому lookup, compatibility и inherited-expression
анализ этой pair недостоверны и зависимые `MORPH0026`–`MORPH0028` не
публикуются. Независимые local composition/settings diagnostics категорий 5–6
сохраняются по их собственным правилам.

### 6.58. `MORPH0026`: requested pair не найдена

Diagnostic публикуется ровно один раз на единственный effective IncludeBase
edge, для которого lookup раздела 6.54 не нашёл authoritative exact pair.
Primary location — identifier `IncludeBase`; additional locations
отсутствуют.

В `{0}` передаётся requested mapping contract после generic substitution, в
`{1}` — current constructed contract, в `{2}` — конечный mapper type.
Diagnostic identity — constructed current node и effective edge. Один source
IncludeBase в generic configuration может поэтому дать несколько diagnostics
для разных constructed substitutions, если они являются разными
пользовательскими contracts.

Отсутствие прямого `base.Configure(builder)` не имеет отдельной diagnostic:
pair, существующая только в неподключённом base mapper-е, просто не входит в
lookup и получает `MORPH0026`. Точно так же same-pair current node без другого
same-level candidate или connected ancestor считается отсутствующей pair.

Регистрация, уже получившая `MORPH0011` либо `MORPH0012`, остаётся найденным,
но ошибочным origin: `MORPH0026` поверх неё не публикуется. Напротив,
unsupported-looking type arguments без какой-либо registration не получают
новую eligibility diagnostic и завершаются именно `MORPH0026`. Если часть
base chain неизвестна из-за `MORPH0016`, отсутствие candidate за этим edge
нельзя доказать и `MORPH0026` подавляется как каскад.

### 6.59. `MORPH0027`: несовместимый included type

Diagnostic публикуется после успешного lookup structurally legal candidate
отдельно для каждой нарушенной base-type relation:

- current source не assignable к included source;
- current destination не assignable к included destination.

Если нарушены обе relation, одна pair получает две diagnostics одного ID.
Если candidate не найден, публикуется только `MORPH0026`; если current либо
candidate registration уже structurally отвергнута категорией 3,
compatibility diagnostic не добавляется.

Primary location — соответствующий source либо destination type argument
`IncludeBase`. Единственная additional location — соответствующий current type
argument authoritative `Map<TSource, TDestination>`. `{0}` равно `source` или
`destination`, `{1}` содержит current type, `{2}` — included type, `{3}` —
current contract, `{4}` — конечный mapper.

Diagnostic identity — constructed current node, effective edge и relation
role. Reference nullable annotations не создают отдельную canonical pair;
identity, class/interface base conversions и поддерживаемая v0 boxing
assignability проверяются той же semantic conversion policy, что effective
IncludeBase composition. Numeric и user-defined conversions не подменяют
base-type relation.

`MORPH0027` не пытается выбрать другой candidate после неудачной
compatibility: exact lookup уже завершён. В частности, несовместимый
current-level candidate не заставляет продолжить поиск одноимённой pair на
дальнем base level.

### 6.60. `MORPH0028`: inaccessible inherited expression

Diagnostic публикуется для каждого originating inherited `Construct`,
`Members` либо `Convert` invocation, чей callback остался effective в
конечной pair и содержит хотя бы одну reference, недоступную из generated
partial конечного mapper-а.

Primary location — identifier effective `IncludeBase` конечной pair.
Additional locations имеют детерминированный порядок:

1. identifier originating `Construct`, `Members` либо `Convert` invocation;
2. все недоступные reference expressions этого callback-а в source order.

Несколько inaccessible references одного callback-а дают одну diagnostic с
несколькими additional locations. Разные originating invocations дают
отдельные diagnostics. Один origin, достигший двух конечных constructed pairs,
может получить две `MORPH0028`, поскольку target accessibility и recovery
являются context-dependent; transitive промежуточный consumer без generated
contract сам по себе fan-out не создаёт.

В `{0}` передаётся fragment family, `{1}` — конечный contract, `{2}` —
конечный mapper. Diagnostic identity включает конечный constructed node и
origin invocation. Primary intentionally указывает на composition boundary,
а additional locations показывают конфигурацию и точные inaccessible
references, которые нужно сделать доступными либо перекрыть.

Локальный `Convert`, declarative/manual precedence, локальный `Construct` и
member-level override применяются до этой проверки. Поэтому discarded base
`Convert`, заменённый `Construct`, полностью перекрытые member expressions и
их более не нужные dependencies diagnostic не получают. Та же reference в
локальном callback-е не является inheritance failure и анализируется обычной
callback/grammar категорией 8 либо C# compiler-ом.

### 6.61. Recovery, propagation, precedence, порядок и suppression

`MORPH0024` имеет mapper-wide recovery. Все непосредственно известные legal
pairs каждого generated mapper-а, чья connected configuration chain содержит
повторный base-call, сохраняют полный `ITypeMapper<,>` contract и independently
legal DSL surfaces, но обе operations бросают
`MappingConfigurationException` независимо от `MappingMode`. Registrations за
недоступным source edge не угадываются; независимые mapper-ы и не подключённые
sibling chains не затрагиваются.

`MORPH0025`–`MORPH0028` имеют pair-level recovery. Затронутая pair сохраняет
полный C#-legal contract и independently legal surfaces, но `Create` и
`Update` бросают `MappingConfigurationException` независимо от
`MappingMode`. Тот же recovery распространяется на transitive consumers,
которые импортируют ошибочный effective slice. Consumer, полностью
отбросивший этот slice по разделу 6.55, сохраняет собственный plan.

Origin diagnostics already-invalid base configuration не размножаются по
каждому transitive consumer-у: duplicate fragment, invalid setting либо другая
первичная причина сохраняет ownership своей категории и source location.
Recovery consumer-а всё равно становится throwing, если его effective slice
зависит от причины. `MORPH0028` является исключением не по fan-out, а по
природе: недоступность возникает заново относительно конкретного конечного
mapper-а и поэтому диагностируется на его IncludeBase edge.

Morphant не выбирает первый duplicate edge, не пропускает unresolved либо
incompatible edge, не вставляет runtime cast, не заменяет inaccessible
callback conventions и не использует outer setting как fallback. Несколько
independent diagnostics одной pair сходятся в один детерминированный throwing
plan; независимые pairs сохраняются.

Precedence действует так:

- compilation-wide gate категории 1, mapper-wide structural gates категории
  2 и exclusion/unsupported-root recovery категории 3 подавляют недостоверный
  category-7 анализ в своей области;
- `MORPH0015`, `MORPH0017` и mapper-/pair-wide builder-flow gates категории 4
  подавляют edges, которые невозможно достоверно восстановить;
  `MORPH0016` не скрывает независимо видимый duplicate base-call, но скрывает
  lookup conclusions о недоступной части chain;
- `MORPH0025` подавляет `MORPH0026`–`MORPH0028` той же pair, поскольку
  authoritative edge отсутствует;
- `MORPH0026` подавляет compatibility и plan analysis, а `MORPH0027` —
  inherited-plan/accessibility analysis соответствующего edge;
- independently provable local fragment conflicts категории 5 и local setting
  origins категории 6 сохраняются; invalid local model подавляет только ту
  часть inherited-expression анализа, которой нужен однозначный effective
  plan;
- general callback portability категории 8 и construction/member/nested
  semantics категорий 9–11 анализируются только для оставшегося effective
  inherited plan; `MORPH0028` владеет исключительно accessibility, утраченной
  при переносе expression между mapper-level-ами;
- `UnmappedMemberValidation` управляет только категорией 12 и не скрывает
  inheritance errors.

Независимые `MORPH0024` и `MORPH0025` могут публиковаться совместно. Две
relations `MORPH0027`, несколько inaccessible callbacks `MORPH0028` и
независимые pairs сохраняют собственную cardinality даже при общем throwing
recovery.

Publication order — по ID. Внутри ID diagnostics упорядочиваются по ordinal
stable final mapper identity, configuration-level order от derived к base,
canonical pair, source location primary и relation/fragment name. Origin-only
`MORPH0024`/`MORPH0025` не размножаются constructed traversal-ом;
context-dependent `MORPH0026`–`MORPH0028` дедуплицируются по identities своих
разделов. Discovery и incremental invalidation не меняют порядок.

Suppression либо понижение severity не меняет lookup, composition, recovery
или generated artifact set. Добавление/удаление/перестановка base-call,
`IncludeBase`, pair registration, mapper level, generic substitution, local
override либо referenced-member accessibility полностью actualizes
diagnostics и effective recovery при одном сохранённом incremental driver-е.

### 6.62. Самостоятельная тестовая матрица категории 7

Unit-категория inheritance diagnostics независимо фиксирует:

- exact descriptors `MORPH0024`–`MORPH0028`: ID, title, category, default
  severity, enabled/configurable flags, message formats и все parameters;
- zero/one direct `base.Configure`, по одному call на successive levels,
  statement-/expression-bodied формы, parentheses/null-forgiving, два и три
  calls одного level с exact primary/first-call additional locations и
  origin-based generic deduplication `MORPH0024`;
- совместную публикацию одного `MORPH0016` и duplicate base-call, а также
  mapper-wide throwing recovery всех известных pairs connected consumers;
- zero/one/two/three `IncludeBase`, разные generic arguments, сторонние и
  compiler-rejected invocations, exact locations, две diagnostics для трёх
  calls и отсутствие dependent lookup diagnostics при `MORPH0025`;
- composition-node identity, exclusion только current node, valid exact
  same-pair из nearest connected base level, same-pair без ancestor как
  `MORPH0026`, current-level order independence и precedence над connected
  candidate;
- lookup по multi-level chain nearest-first, отсутствие неявного доступа без
  `base.Configure`, transitive composition, generic substitutions и nested
  mapper levels;
- одну `MORPH0026` на unresolved constructed edge, exact requested/current/
  mapper parameters, отсутствие separate missing-chain diagnostic и
  suppression при неизвестной chain за `MORPH0016`;
- registered unavailable/unsupported candidate с origin-only
  `MORPH0011`/`MORPH0012` против полностью отсутствующей unsupported-looking
  requested pair с `MORPH0026`;
- source-only, destination-only и double incompatibility, exact type-argument
  primary/current-`Map` additional locations и две `MORPH0027` при нарушении
  обеих relations;
- positive compatibility identity/class/interface/boxing matrix,
  reference-nullability canonicalization и rejection numeric/user-defined
  conversions без поиска более дальнего fallback candidate;
- отсутствие cycle descriptor/state: valid same-pair chain через несколько
  mapper-level-ов, compatible same-level partial order, reverse incompatible
  edge с `MORPH0027` и compiler-owned cyclic type hierarchy;
- inheritance всех шести map-level settings и полную precedence matrix через
  transitive included pairs и connected roots;
- cross-pair import только `Members`, повторное применение current
  conventions/construction и отсутствие переноса structured/direct/factory
  `Construct` и `Convert`;
- exact same-pair full-plan inheritance отдельно для structured/direct
  `Construct`, declarative `Members` и manual `Convert`, без casts/adapters;
- полную local-model matrix: no fragments, local `Convert`, local
  `Construct`, local `Members`, `Construct` + `Members`, inherited
  declarative/manual plans, Construct fallback/override и member-level
  expression/`Auto`/`Ignore` precedence;
- slice-sensitive propagation: ignored cross-pair `Construct`/`Convert`
  errors, local `Convert` dropping declarative errors, local declarative plan
  dropping inherited `Convert`, overridden expressions and transitive error
  recovery without origin diagnostic fan-out;
- `MORPH0028` для effective inherited `Construct`, `Members` и `Convert`:
  private member, explicit `base.`, public/internal/protected positives,
  multiple inaccessible references in one diagnostic, multiple callback
  origins and context-dependent constructed consumers;
- отсутствие `MORPH0028` у discarded `Convert`, overridden `Construct`,
  полностью overridden member rules/dependencies и local callback, а также
  правильный boundary с category-8 grammar/captures;
- полный generated recovery: mapper-wide `MORPH0024`, pair-level
  `MORPH0025`–`MORPH0028`, transitive consumers, обе throwing operations
  независимо от `MappingMode`, complete legal surfaces и сохранение
  independent mapper/pair;
- precedence с `MORPH0001`–`MORPH0023`, самостоятельную cardinality
  duplicates/двух relations/нескольких callbacks, deterministic order и
  отсутствие downstream cascade;
- suppression/изменение severity без изменения lookup, plan, recovery и
  artifacts;
- actualization каждого base edge, IncludeBase edge, candidate registration,
  generic substitution, type relation, local override и accessibility change
  при одном сохранённом incremental driver-е.

Package-like integration-категория независимо проверяет:

- suppressed `MORPH0024`: mapper contracts и legal surfaces компилируются,
  обе operations всех affected known pairs бросают, independent mapper
  реально исполняется;
- suppressed `MORPH0025`, `MORPH0026`, source/destination/double
  `MORPH0027` и каждый callback family `MORPH0028`: affected pair и
  transitive consumer бросают без fallback, independent pair исполняется;
- exact same-pair inheritance реально исполняет base result policy, `Members`
  либо `Convert`, local model precedence заменяет их по таблице раздела 6.55,
  а cross-pair переносит только `Members`;
- same-level/nearest-level/transitive lookup, generic/nested mapper
  substitution и class/interface/boxing compatibility на обычном
  analyzer-backed consumer build;
- inaccessible base helpers не исполняются, полностью перекрытые callbacks
  не блокируют mapping, а origin diagnostics imported errors не размножаются;
- реальное `.editorconfig`/MSBuild suppression или severity override для всех
  пяти IDs без изменения generated artifact set и effective recovery.

### 6.63. Категория 8: замороженный pre-revision draft

Разделы 6.63–6.71 ниже сохраняют последний полный черновик diagnostic
contract только как материал для будущей согласованной переработки. Они не
описывают финальный callback API, не ожидают ревью и не являются входом для
реализации. В частности, все ссылки на direct `Construct` / `Resolve`,
`ByFactory`, три вместо четырёх `Members` overload-ов и прежнюю классификацию
context должны быть заменены после завершения API-среза.

Категория «Переносимость callbacks и declarative grammar» содержит ровно пять
diagnostics:

| ID | Title | Message format |
|---|---|---|
| `MORPH0029` | `Declarative callback must be a lambda` | `Declarative {0} callback for contract '{1}' must be an inline lambda.` |
| `MORPH0030` | `Callback capture cannot be transferred` | `{0} callback for contract '{1}' captures '{2}', which cannot be transferred to generated mapper '{3}'.` |
| `MORPH0031` | `Unsupported declarative syntax` | `Declarative {0} callback for contract '{1}' contains unsupported syntax '{2}'.` |
| `MORPH0032` | `Declarative destination input is read-only` | `Declarative destination input '{0}' for contract '{1}' is read-only and cannot be mutated.` |
| `MORPH0033` | `Declarative marker is unavailable in runtime callback` | `Declarative marker '{0}' is unavailable in runtime {1} callback for contract '{2}'.` |

Для всех пяти diagnostics действует общий descriptor contract:

- category — `Morphant.Callbacks`;
- default severity — `Error`;
- diagnostic включена по умолчанию и не имеет `NotConfigurable`;
- description и help link отсутствуют, custom tags пусты;
- анализируется только оставшийся effective callback plan после категорий
  4–7 и согласованной overload-ревизии из
  [`MAPPING_API_IMPLEMENTATION_PLAN.md`](MAPPING_API_IMPLEMENTATION_PLAN.md);
- `MappingMode`, declarative null handling и статическая достижимость влияют
  на анализ отдельных execution paths; хотя бы один достижимый invalid path
  сохраняет diagnostic;
- `UnmappedMemberValidation` не скрывает callback error и не превращает его в
  warning;
- suppression либо изменение severity меняет только compiler presentation и
  не разрешает непереносимый код, не расширяет declarative grammar и не
  превращает marker в runtime API.

Mapping contracts форматируются по canonical identity категории 3. Mapper
types используют fully-qualified nullable-aware display категории 2.
Callback names в parameters — точные family names `Construct`, `Resolve`,
`Members`, `ByFactory` либо `Convert`; marker names — `Auto`, `Ignore`, `Map`,
`Create`, `Update`, `ByConvention` либо `ByFactory` без generic arity.
Unsupported syntax получает стабильное англоязычное имя пользовательской
конструкции, например `for statement`, `assignment` либо `local function`, а
не внутреннее имя Roslyn operation/syntax kind.

Категория 8 не выполняет configuration code и не интерпретирует arbitrary C#
как declarative plan. Она использует bound generated overload и symbol
identity: одноимённые delegates, methods и marker-подобные вызовы стороннего
API не принадлежат Morphant callback grammar.

### 6.64. Pre-revision declarative и runtime callback classes

В сохранённом pre-revision draft callbacks делились на два класса:

| Callback | Class | Анализ |
|---|---|---|
| Structured `Construct(source)` | Declarative | Inline lambda и конечная construction-plan grammar |
| Structured `Resolve(source, previous)` | Declarative | Inline lambda и конечная construction-plan grammar |
| Все три `Members` overloads | Declarative | Inline lambda и конечная member-plan grammar |
| Direct `Construct`, обе overloads | Runtime | Обычный синхронный C# callable, переносимый целиком |
| Direct `Resolve`, обе overloads | Runtime | Обычный синхронный C# callable, переносимый целиком |
| Тело `ByFactory`, обе overloads | Runtime | Обычный синхронный C# callable выбранной factory branch |
| Все три `Convert` overloads | Runtime | Обычный синхронный C# callable, полностью заменяющий declarative pipeline |

Для declarative lambda поддерживаются expression-body и конечная block
grammar:

- initialized locals, `const`, pattern variables и nested blocks;
- полные `if` / `else if` / `else`, statement `switch`, несколько `return` и
  явный `throw`;
- conditional- и switch-expressions для whole plan, strategy, rule и marker;
- object initializer construction plan и `DestinationMembers` `with` overlay;
- обычные expressions, доступные mapper/static members, compile-time
  Configure constants и точный `nameof(...)`;
- declarative `Auto`, `Ignore`, `Map`, `Create`, `Update`, `ByConvention` и
  внешний `ByFactory` в тех plan positions, где их семантику проверяют
  категории 9–11.

За declarative boundary остаются locals без initializer-а, последующая
mutation locals, deconstruction/compound assignment, `++` / `--`, loops,
`break` / `continue`, side-effect-only statements, внешние local functions,
`try` / `catch` / `finally`, `using`, `lock`, labels / `goto`, `ref` / `using`
locals, `unsafe` / `fixed`, `async` / `await` и `yield`. Точная C# binding
ошибка по-прежнему принадлежит compiler-у; `MORPH0031` появляется только для
успешно связанного Morphant declarative callback-а, который generator не может
понизить по своей DSL grammar.

Runtime callback принимает expression lambda, синхронную block lambda,
natural method group либо materialized delegate-expression. Внутри допустимы
обычные C# locals, assignments, mutation, loops, `try` / `finally`, nested
local functions, constructors, factories и record `with`. Ограничением
является не statement grammar, а возможность перенести callable в lifetime
generated mapper-а и отсутствие compile-time Morphant markers в исполняемом
body.

Mapper instance/static members, static API, type references, method groups и
compile-time constants переносимы. Source, previous, result и context текущего
callback-а, declarative locals и pattern variables, а также captures тела
`ByFactory` из его enclosing declarative plan передаются generator-ом явно и
не являются configuration-time captures. Configure-local runtime values,
параметр `builder` и local functions, объявленные во внешнем `Configure`, не
существуют в lifetime mapping execution и не переносятся. Delegate, хранимый в
доступном mapper field/property, допустим; delegate в Configure-local — нет.

Transparent parentheses, null-forgiving expression и явный cast к ожидаемому
delegate type не меняют callback class, origin либо inline-lambda identity.
Полностью перекрытый, отброшенный model precedence либо доказанно
недостижимый callback slice не анализируется. Один source callback, импортированный
несколькими consumers, сохраняет один origin diagnostic; recovery независимо
распространяется только на consumers, чей effective plan оставил invalid
slice.

### 6.65. `MORPH0029`: declarative callback должен быть inline lambda

`MORPH0029` публикуется, когда bound structured `Construct`, structured
`Resolve` либо `Members` получает не inline lambda, а method group,
materialized delegate, Configure-local callback, mapper member с delegate либо
другое delegate-valued expression. Morphant не исполняет такой callable во
время configuration и не пытается декомпилировать method body в declarative
plan.

Primary location — core callback expression после исключения transparent
outer wrappers. Diagnostic identity — callback origin; один и тот
же origin получает одну diagnostic независимо от generic substitutions,
числа reachable operations и inherited consumers. Additional locations нет.

Direct `Construct`, direct `Resolve`, `ByFactory` body и `Convert` той же
формы `MORPH0029` не получают: они являются runtime callbacks и могут быть
method group/materialized delegate. Inline declarative lambda с cast,
parentheses либо null-forgiving wrapper остаётся inline.

Поскольку внутренний plan non-lambda callback-а недоступен, `MORPH0029`
подавляет `MORPH0030`–`MORPH0032` того же callback origin. Весь effective
declarative fragment становится invalid; generator не вызывает callback во
время `Configure` и не подставляет conventions вместо него.

### 6.66. `MORPH0030`: callback capture нельзя перенести

`MORPH0030` публикуется для каждого symbol, на который ссылается effective
declarative либо runtime callback, когда значение symbol существует только во
время выполнения `Configure` и не может быть воспроизведено в generated
mapper. Это включает:

- non-const Configure-local либо parameter enclosing local function;
- сам параметр `builder` и alias на него;
- local delegate либо local function, объявленные во внешнем `Configure`;
- значение, транзитивно полученное из такого symbol и захваченное nested
  runtime callback-ом.

Compile-time `const`, значение `nameof`, доступный instance/static mapper
member и delegate из mapper field/property допустимы. Local function,
объявленная внутри direct/factory/manual runtime block, переносится вместе с
этим block и также допустима. Capture source/previous/result/context текущего
callback-а либо enclosing declarative local из `ByFactory` body является
mapping-time dependency, а не `MORPH0030`.

Primary location — первая по source order effective reference захваченного
symbol внутри callback-а. Declaration symbol-а, если она source-visible, и
остальные effective references внутри того же callback-а становятся
additional locations в source order. Diagnostic identity —
`(callback origin, captured symbol)`;
несколько references дают одну diagnostic, разные symbols и callback origins
— независимые diagnostics.

Для declarative callback invalid становится только достижимый slice,
зависящий от capture. Для runtime callback callable переносится целиком,
поэтому capture делает invalid весь callback и все paths, фактически его
вызывающие. Generator не замораживает configuration-time runtime value, не
вызывает local function во время generation и не заменяет capture `default`.

### 6.67. `MORPH0031`: неподдерживаемая declarative syntax

`MORPH0031` публикуется для каждого внешнего независимого syntax node, который
находится внутри effective declarative lambda, успешно связан C# compiler-ом,
но не входит в grammar раздела 6.64. В частности, diagnostic получают:

- local без initializer-а, последующая/deconstruction/compound assignment
  declarative local-а и `++` / `--`;
- `for`, `foreach`, `while`, `do`, `break` и `continue`;
- invocation, assignment либо другая statement-expression только ради side
  effect;
- local function во внешнем declarative block;
- `try` / `catch` / `finally`, `using`, `lock`, label и `goto`;
- `ref` / `using` local, `unsafe`, `fixed`, `await` и `yield`.

Primary location — наиболее конкретный keyword/operator либо полный node,
если у конструкции нет отдельного устойчивого token-а. `{2}` в message
называет именно этот внешний node. Additional locations нет. Diagnostic
identity — syntax origin; несколько независимых outer nodes дают отдельные
diagnostics.

После diagnostic generator не обходит вложенное содержимое уже
неподдерживаемого `for`, `try`, local function и аналогичного outer node ради
каскада дополнительных grammar diagnostics. Mutation previous/result на том
же site принадлежит более точной `MORPH0032`; обычная mutation declarative
local/source остаётся `MORPH0031`.

Поддерживаемые expressions внутри возвращаемого plan-а сохраняют обычную C#
семантику и side effects при фактическом вычислении. `MORPH0031` не является
общим purity analyzer-ом и не пытается классифицировать вызываемый method как
mutating; она ограничивает только явно наблюдаемую declarative statement и
mutation grammar.

### 6.68. `MORPH0032`: destination inputs declarative callback-а read-only

`MORPH0032` публикуется на каждую явную попытку изменить normalized
destination input `previous` либо фактический `result` внутри declarative
callback-а. Mutation включает simple/compound/deconstruction assignment,
`++` / `--`, передачу storage как `ref` / `out` и запись в property, field
либо indexer через сам input или явно прослеживаемый alias.

Alias прослеживается через transparent wrappers, identity/implicit reference
conversion, initialized local, pattern variable и успешно извлечённое
destination value из `previous`. Trace не проходит через arbitrary method
return, user-defined conversion либо неизвестный delegate. Вызов method-а на
destination сам по себе не объявляется mutation: standalone side-effect call
получает `MORPH0031`, а вызов внутри допустимого value expression сохраняет
обычную C# semantics.

Primary location — assignment/inc-dec operator либо `ref` / `out` keyword
конкретного mutation site. `{0}` равно исходному input `previous` либо
`result`, даже если запись выполнена через alias. Additional locations нет;
каждый mutation site получает отдельную diagnostic.

Object initializer возвращаемого `DestinationConstruction`, assignment в
возвращаемом `DestinationMembers` plan и record `with`, создающий новый plan,
не мутируют фактический destination input и разрешены. Новый независимый local
destination также не становится read-only только из-за совпадения типа.

`MORPH0032` имеет приоритет над общей `MORPH0031` на том же mutation site.
Invalid становится только достижимый declarative slice, зависящий от записи;
generator не выполняет mutation как скрытый imperative update и не считает её
эквивалентом возвращённого member rule.

### 6.69. `MORPH0033`: declarative marker недоступен в runtime callback

`MORPH0033` публикуется на каждый bound Morphant marker invocation внутри
direct `Construct`, direct `Resolve`, тела `ByFactory` либо `Convert`:

- `Auto` и `Ignore`;
- adaptive `Map` и explicit nested `Create` / `Update`;
- `ByConvention` и `ByFactory`.

Primary location — invoked marker name; generic type arguments и argument list
в location не входят. Diagnostic identity — invocation origin, поэтому каждый
marker call получает собственную diagnostic. `{1}` в message равно runtime
callback family `Construct`, `Resolve`, `ByFactory` либо `Convert`.

Внешний `ByFactory(...)`, формирующий structured construction plan, остаётся
допустимым declarative marker-ом; запрещены marker calls внутри переданного
factory callback-а. `context.Mapper.Map(...)` является обычным runtime API и
разрешён. Одноимённый method стороннего типа либо пользовательского mapper-а,
который не связывается с точным Morphant marker symbol, игнорируется.

Runtime callback переносится целиком, поэтому хотя бы один `MORPH0033` делает
invalid весь callable и все paths, фактически его вызывающие. Generator не
lower-ит marker внутри arbitrary runtime control flow, не выполняет его как
заглушечный method и не заменяет runtime callback declarative conventions.

### 6.70. Recovery, precedence, порядок и suppression

Все пять diagnostics сохраняют полный C#-legal mapping contract и независимо
допустимые generated surfaces. Invalid execution path использует typed stub,
бросающий `MappingConfigurationException`; независимые paths, operations,
pairs и mapper-ы остаются исполнимыми.

Recovery максимально path-sensitive для declarative plan:

- invalid structured `Construct` затрагивает только reachable no-previous
  creation paths; existing Update reuse сохраняется;
- invalid structured `Resolve` затрагивает только его достижимые selection
  branches;
- invalid `Members` expression бросает только на path, где rule/dependency
  действительно требуется; невыбранные rules и остальные operations
  сохраняются;
- полностью overridden member/creation expression, discarded inherited slice
  и статически недостижимая branch не получают diagnostic и stub.

Runtime callable является атомарным:

- invalid direct `Construct` затрагивает все no-previous paths, которые его
  вызывают, но не existing Update reuse;
- invalid direct `Resolve` затрагивает все operations, вызывающие effective
  selector;
- invalid `ByFactory` body затрагивает только paths, выбравшие эту factory
  branch; альтернативный constructor/previous path сохраняется;
- invalid `Convert` затрагивает все разрешённые `MappingMode` operations pair,
  поскольку каждая выбранная overload полностью реализует manual mapping.

Origin diagnostic не размножается по inherited consumers. Consumer получает
throwing recovery только если его effective plan сохранил invalid slice;
local override либо model precedence может полностью удалить зависимость.
Несколько diagnostics одного callback-а сходятся в один deterministic recovery
plan без попытки выбрать «первую» допустимую часть runtime callable.

Precedence действует так:

- compilation/mapper/pair gates категорий 1–3 и недостоверный builder flow
  категории 4 подавляют category-8 анализ в своей области;
- duplicate/mixed local fragments категории 5, invalid effective settings
  категории 6 и invalid inheritance edge категории 7 разрешаются до анализа
  callback contents; discarded callback diagnostic не получает;
- `MORPH0028` владеет переносом доступности между mapper-level-ами;
  `MORPH0030` — lifetime capture уже доступного effective callback-а;
- `MORPH0029` подавляет `MORPH0030`–`MORPH0032` того же declarative origin;
  `MORPH0032` заменяет `MORPH0031` на том же mutation site;
- независимые capture, grammar и marker origins публикуются совместно, даже
  если их recovery сходится;
- точная C# binding/type error остаётся compiler diagnostic; Morphant не
  публикует content diagnostic для callback/marker, symbol identity которого
  невозможно установить;
- construction/member/nested diagnostics категорий 9–11 анализируют только
  оставшийся valid slice и не каскадируют поверх category-8 stub;
- category-12 warning-анализ не выполняется для invalid affected slice, но
  сохраняется для независимого effective plan.

Publication order — по ID. Внутри ID diagnostics упорядочиваются по ordinal
stable mapper identity, canonical pair, callback origin, primary source
location и captured symbol/syntax/marker name. Additional locations
`MORPH0030` сохраняют source order. Generic construction, inheritance fan-out,
discovery order и incremental invalidation не меняют cardinality или порядок.

Suppression либо понижение severity не меняет callback classification,
effective plan, recovery и generated artifact set. Замена overload-а,
destination capability, callback form/body, captured symbol, local override,
`MappingMode`, constant branch либо marker binding полностью actualizes
diagnostics и affected stubs при одном сохранённом incremental driver-е.

### 6.71. Самостоятельная тестовая матрица категории 8

Unit-категория callback diagnostics независимо фиксирует:

- exact descriptors `MORPH0029`–`MORPH0033`: ID, title, category, default
  severity, enabled/configurable flags, message formats и все parameters;
- классификацию structured/direct destination и всех согласованных
  `Construct`, `Resolve`, `Members`, `ByFactory`, `Convert` overloads без
  зависимости от тестов generated-surface категорий;
- `MORPH0029` для structured `Construct`, structured `Resolve` и каждой
  `Members` arity через natural method group, mapper delegate,
  Configure-local delegate и произвольное delegate-expression;
- отсутствие `MORPH0029` у inline expression/block lambda с parentheses,
  null-forgiving и explicit delegate cast, а также у runtime
  method-group/materialized callbacks;
- одну `MORPH0029` на callback origin при generic substitutions и inherited
  fan-out, exact callback-argument location и подавление content diagnostics
  недоступного body;
- `MORPH0030` отдельно для runtime Configure-local, `builder`, builder alias,
  external local delegate/function и транзитивного nested capture во всех
  пяти callback families;
- capture cardinality `(callback, symbol)`, first-reference primary,
  declaration/remaining-reference additional locations, несколько symbols и
  одинаковый symbol в нескольких callbacks;
- positive transfer mapper instance/static members, static API, compile-time
  constants, `nameof`, mapper delegate field/property, callback parameters,
  declarative locals/patterns, `ByFactory` enclosing dependencies и runtime
  local function внутри переносимого block-а;
- полный positive declarative grammar: expression/block lambdas, initialized
  locals, nested blocks, complete if/switch, multiple return/throw,
  conditional/switch expressions, object initializer, member `with`, patterns
  и допустимые marker positions;
- `MORPH0031` для каждого класса неподдерживаемой grammar: uninitialized/
  mutated/deconstructed locals, compound assignment/inc-dec, every loop,
  break/continue, side-effect statement, external local function, try/catch/
  finally, using/lock, label/goto, ref/using local, unsafe/fixed, await/yield;
- outer-node cardinality и exact keyword/operator locations, отсутствие
  nested cascade внутри уже unsupported node и совместную публикацию
  независимых grammar breaks;
- отсутствие `MORPH0031` для arbitrary synchronous statements внутри direct
  `Construct`, direct `Resolve`, `ByFactory` и каждой `Convert` overload;
- `MORPH0032` для simple/compound/deconstruction assignment, inc-dec,
  ref/out и member/indexer writes через `previous`, `result`, initialized
  alias, pattern alias и извлечённое previous value;
- per-site `MORPH0032`, root-input message parameter, exact operator/ref-out
  location и precedence над `MORPH0031`;
- positive object/member plan initializer, `with` copy, independent same-type
  local и method-call boundary без попытки purity inference;
- каждую marker family `MORPH0033` внутри direct `Construct`, direct
  `Resolve`, nested runtime local function, обе `ByFactory` forms и все
  `Convert` forms, включая несколько invocations одного callback-а;
- отсутствие `MORPH0033` у внешнего structured `ByFactory`,
  `context.Mapper.Map`, одноимённого foreign/user method и compiler-unbound
  invocation;
- overload-invariant applicability: обе `Construct` forms только на
  no-previous paths, обе `Resolve` forms на полном result-selection surface,
  три `Members` forms с dependency-driven lifecycle и три `Convert` forms во
  всех enabled operations;
- reachability через `MappingMode`, null handling, constant conditions,
  Create/Update specialization, member override, local model precedence и
  exact/cross-pair inheritance;
- path-sensitive declarative recovery отдельно для construction branches,
  member rules/dependencies, previous reuse, replacement, terminal null и
  statically unreachable expressions;
- atomic runtime recovery отдельно для direct no-previous `Construct`, full
  direct `Resolve`, selected factory branch и all-enabled-operations
  `Convert`;
- полный generated result при каждой diagnostic: legal mapper contracts и
  surfaces, typed throwing stubs, сохранённые independent operations/pairs/
  mapper-ы и отсутствие downstream category-9–12 cascade;
- precedence и совместную cardinality с `MORPH0001`–`MORPH0028`, C# compiler
  diagnostics, deterministic publication order и origin-only inherited
  diagnostics;
- реальное suppression/изменение severity без изменения callback plan,
  recovery либо artifacts;
- actualization callback overload/class/body, destination capability,
  captured symbol/declaration, mapper member conversion, marker binding,
  reachability, override и inheritance при одном сохранённом incremental
  driver-е.

Package-like integration-категория независимо проверяет:

- suppressed `MORPH0029` для каждой declarative family: affected path бросает,
  existing Update либо независимая branch исполняется, independent pair
  сохраняется;
- suppressed `MORPH0030` для inline declarative, direct `Construct` /
  `Resolve`, `ByFactory`, всех `Convert` forms и materialized local delegate с
  соответствующим path-sensitive либо atomic recovery;
- suppressed representative `MORPH0031` / `MORPH0032`: invalid member/
  construction path бросает без выполнения side effect, valid branches и
  operations реально возвращают результат;
- suppressed `MORPH0033` для каждого runtime callback family, отсутствие
  исполнения marker body и сохранение альтернативной factory/reuse branch;
- natural method groups и delegates из mapper members реально исполняются во
  всех runtime overloads, а context-aware callbacks используют
  `context.Mapper` в том же scope;
- source-only/previous-aware/context-aware `Convert` реально имеют одинаковую
  operation applicability и различаются только доступными inputs;
- local override и exact/cross-pair inheritance удаляют только discarded
  invalid slice без origin fan-out, а retained transitive consumer получает
  recovery;
- реальное `.editorconfig`/MSBuild suppression или severity override для всех
  пяти IDs без изменения generated artifact set и effective recovery.

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
