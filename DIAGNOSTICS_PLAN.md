# План compile-time diagnostics Morphant v0

Дата составления: 7 августа 2026 года.

Статус: этап 2, категория 1, ожидает ревью.

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
| 2 | Полный каталог и точный контракт каждой diagnostic по одной категории за раз | Категория 1 ожидает ревью; категории 2–12 не начаты |
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
