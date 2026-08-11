# План compile-time diagnostics Morphant v0

Дата составления: 7 августа 2026 года.

Последнее обновление: 11 августа 2026 года.

Статус: таксономия и полный контракт категорий 1–12 приняты. Перечисленные в
разделе 7 предварительные выравнивания production-model приняты пользователем.
Vertical slices категорий 1–2 реализованы и приняты пользователем. Vertical
slice категории 3 реализован и ожидает пользовательского ревью; следующий
slice после его принятия — категория 4.

Этот документ является отдельным рабочим планом этапа 23 из
[`MAPPING_API_IMPLEMENTATION_PLAN.md`](MAPPING_API_IMPLEMENTATION_PLAN.md).
Нормативную mapping-семантику задаёт
[`MAPPING_API_DESIGN.md`](MAPPING_API_DESIGN.md), а уже реализованную границу
runtime failures и recovery-stubs — раздел 14.2 того же документа и
[`docs/observable-failures.md`](docs/observable-failures.md).

Актуальный callback surface состоит из structured
`Construct` / `Resolve` / `Members`, runtime `ConstructUsing` /
`ResolveUsing` / `Convert` и compile-time `MappingContextMarker`. Вложенный
`ByFactory` и direct-формы `Construct` / `Resolve` удаляются. Нормативный
контракт также ограничивает read-only member proxy только применимыми
non-opaque reference-type nested destinations. Оба уточнения зафиксированы в
`MAPPING_API_DESIGN.md` и отдельном разделе
`MAPPING_API_IMPLEMENTATION_PLAN.md`.

Ревизия от 10 августа 2026 года синхронизировала категории 1–8 с этим surface,
opaque roots, generated `Supports(Type, Type)`, exact `Value<T>`, terminal
compile-time markers и fail-closed compiler preflight. IDs
`MORPH0001`–`MORPH0033` сохранены; `MORPH0012` сужена до root type parameter,
а новый mapper-level конфликт generated `Supports` получил следующий ID
`MORPH0034` без перенумерации уже согласованного диапазона.

Ревизия от 11 августа 2026 года зафиксировала category-9 construction contract
с `MORPH0035`–`MORPH0039`: отсутствие construction policy, convention
selection, explicit constructor-parameter rules, unavailable previous и
`null` / `default` structured plan.

Следующая ревизия от 11 августа 2026 года зафиксировала category-10 member
contract с `MORPH0040`–`MORPH0043`: invalid explicit rule, неудовлетворённый
`required`, неприменимая lifecycle-фаза и `null` / `default` structured member
plan. Required/init blockers теперь имеют точный первичный ownership и
подавляют производную construction diagnostic.

Третья ревизия от 11 августа 2026 года зафиксировала category-11 nested
mapping contract с `MORPH0044`–`MORPH0046`: статически неопределимая nested
pair, несовместимый result и недопустимый destination nested Update. Lookup и
фактическая runtime-совместимость широкого current slot-а остаются runtime
contract-ом.

Четвёртая ревизия от 11 августа 2026 года завершила каталог category-12
warnings `MORPH0047`–`MORPH0048`: pair-wide source/destination completeness,
semantic source-use, точный compile-time source discard и destination
occupancy. Warnings не меняют generated mapping либо recovery.

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
| 2 | Полный каталог и точный контракт каждой diagnostic по одной категории за раз | Принят: категории 1–12 полностью специфицированы |
| 3 | Реализация, recovery, самостоятельные unit- и integration-тесты вертикальными срезами | В работе: категории 1–2 приняты, категория 3 реализована и ожидает ревью |
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

Таксономия содержит 12 категорий. Таблица фиксирует ownership и границы;
отдельные IDs, messages и полный перечень состояний принятых категорий задаёт
каталог раздела 6.

| № | Категория | Граница ответственности |
|---:|---|---|
| 1 | Окружение компиляции и обязательный contract Morphant | Глобальные prerequisites, без которых generator не может корректно интерпретировать ни один mapper: минимальная эффективная версия C# и наличие однозначного совместимого набора обязательных Morphant symbols. |
| 2 | Объявление mapper-а и формируемость generated contract | Форма пользовательского mapper type и возможность корректно объявить его partial implementation, interfaces, operation contracts и обязательные infrastructure members. Конфликты между зарегистрированными pair относятся к категории 3. |
| 3 | Регистрация mapping pair и допустимость типов | Вызовы `Map<TSource, TDestination>`, canonical identity, eligibility pair types, повторные pair текущего mapper-а и межпарные конфликты generated contract. Одинаковые pair в разных mapper types здесь не запрещаются. |
| 4 | Обнаружение конфигурации и builder flow | Возможность однозначно восстановить поддерживаемый прямой линейный flow `Configure`. Вынос Morphant builder-а в alias, helper, delegate либо неподдерживаемый control flow не игнорируется молча. |
| 5 | Локальная композиция mapping plan | Взаимная совместимость трёх local slots: одной result policy (`Construct` / `Resolve` / `ConstructUsing` / `ResolveUsing`), `Members` и `Convert` до анализа callback contents. |
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

Перед назначением `MORPH0034` 10 августа 2026 года повторён exact-search в
публичном GitHub code, web index и индексируемых NuGet/analyzer pages. Внешних
публичных .NET/Roslyn diagnostics с этим ID не найдено. ID назначен после
диапазона категории 8: принятые номера не сдвигаются и не переиспользуются
даже до первой package-публикации каталога.

Перед назначением девятой группы 11 августа 2026 года тем же способом
проверены `MORPH0035`–`MORPH0039`. Внешних публичных .NET/Roslyn diagnostics с
этими ID не найдено. Case-insensitive совпадение `Morph0035` является specimen
identifier из биологического каталога, а не analyzer diagnostic, поэтому
коллизией не считается.

Перед назначением десятой группы 11 августа 2026 года тем же способом
проверены `MORPH0040`–`MORPH0043`. Внешних публичных .NET/Roslyn diagnostics с
этими ID не найдено.

Перед назначением одиннадцатой группы 11 августа 2026 года тем же способом
проверены `MORPH0044`–`MORPH0046`. Внешних публичных .NET/Roslyn diagnostics с
этими ID не найдено. Case-insensitive совпадения `Morph0044` и `Morph0045`
являются specimen identifiers из биологических каталогов, а не analyzer
diagnostics, поэтому коллизией не считаются.

Перед назначением двенадцатой группы 11 августа 2026 года тем же способом
проверены `MORPH0047`–`MORPH0048`. Внешних публичных .NET/Roslyn diagnostics с
этими ID не найдено.

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
- `Morphant.TypeMapper`, его exact
  `void Configure(Morphant.MapperBuilder)`, infrastructure method
  `bool Supports(System.Type, System.Type)` и полный compile-time intrinsic
  surface `Value` / `Auto` / `Ignore` / `Map` / `Create` / `Update` /
  `ByConvention`;
- `Morphant.MapperBuilder`, `Morphant.MapperBuilderBase<T>` и
  `Morphant.MapperBuilder<TSource, TDestination>`;
- instance registration method
  `MapperBuilder<TSource, TDestination> Map<TSource, TDestination>(MappingMode)`;
- runtime contracts, непосредственно связываемые либо называемые generated
  code: `ITypeMapper<,>`, `IMapper`, `Option<>`, `Context.MappingContext`,
  `Context.MappingContextMarker`, `Context.MappingOperation` и mapping-scope
  entry points;
- exact delegate families и generic arities:
  `Delegates.Construct<,>` / `Construct<,,>`,
  `Resolve<,,>` / `Resolve<,,,>`,
  `ConstructUsing<,>` / `ConstructUsing<,,>`,
  `ResolveUsing<,,>` / `ResolveUsing<,,,>`,
  `Members<,>` / `Members<,,>` / `Members<,,,>` / `Members<,,,,>` и
  `Convert<,>` / `Convert<,,>` / `Convert<,,,>`;
- constructor/member wrappers and markers, включая exact
  `Markers.ValueMarker<T>`, typed/untyped member markers и
  `ByConventionMarker`;
- актуальную exception hierarchy и обязательные constructor/property shapes,
  используемые runtime и generated branches, включая abstract
  `Exceptions.MappingException`, `MappingConfigurationException`,
  operation/null/nested failures и `InvalidMappingContextException`.

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

Revision metadata ещё не реализовывалась и не публиковалась в package, поэтому
синхронизация manifest-а с уже принятым pre-release surface не требует
повышения revision. Первой опубликованной формой остаётся revision `1`;
устаревшие `ByFactory`, direct-callback signatures и прежние delegate arities
в неё не входят.

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
ровно семь diagnostics. `MORPH0034` назначена после уже согласованного
диапазона категории 8 и поэтому намеренно не соседствует по номеру с
`MORPH0005`–`MORPH0010`:

| ID | Title | Message format |
|---|---|---|
| `MORPH0005` | `Mapper must derive from TypeMapper` | `Mapper '{0}' must derive from 'Morphant.TypeMapper'.` |
| `MORPH0006` | `Mapper must be partial` | `Mapper '{0}' must be declared partial so Morphant can generate its mapping contract.` |
| `MORPH0007` | `Containing type must be partial` | `Containing type '{0}' must be declared partial so Morphant can generate nested mapper contracts.` |
| `MORPH0008` | `File-local mapper declaration is not supported` | `File-local type '{0}' cannot declare or contain a generated Morphant mapper contract.` |
| `MORPH0009` | `Mapping contract is already declared` | `Mapping contract '{0}' is already declared by mapper '{1}'. Remove the interface declaration or the Map registration.` |
| `MORPH0010` | `Mapping contract conflicts with a declared interface` | `Mapping contract '{0}' can unify with an interface contract declared by mapper '{1}'.` |
| `MORPH0034` | `Mapper member conflicts with generated Supports` | `Mapper '{0}' declares 'Supports(System.Type, System.Type)', which conflicts with the Morphant-generated mapping contract.` |

Для всех семи diagnostics действует общий descriptor contract:

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
и всегда привязываются к первой регистрации этой pair. `MORPH0034`
дедуплицируется по mapper symbol и набору собственных конфликтующих methods.
Одинаковые mapper либо pair names в разных symbol identities остаются
независимыми.

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

### 6.17a. `MORPH0034`: собственный member конфликтует с generated `Supports`

Diagnostic публикуется, когда mapper в любой своей partial declaration сам
объявляет ordinary method с C#-signature `Supports(System.Type, System.Type)`:
имя `Supports`, нулевая generic arity, два value-parameter-а exact
`System.Type` без `ref` / `in` / `out`. Return type, accessibility,
`static` / instance, `new` и `override` не различают C# member signature и не
устраняют конфликт с обязательным generated override.

Primary location — identifier первого такого собственного `Supports` в
stable source order. Identifiers остальных собственных methods с той же
signature становятся additional locations. В `{0}` передаётся
fully-qualified mapper type. Несколько declarations дают одну diagnostic
mapper-а; уже malformed пользовательский duplicate set по-прежнему получает
собственную compiler diagnostic, но не заставляет Morphant публиковать один и
тот же conflict несколько раз.

Inherited `Supports`, включая override generated base mapper-а либо вручную
объявленный member base class, разрешён: новый generated override законно
переопределяет его и вызывает `base.Supports`. Собственный overload с другим
числом/типом/ref-kind parameters или с generic arity также не конфликтует.

Morphant не принимает пользовательский `Supports` как replacement и не
пытается вывести из его body registered pairs. Diagnostic блокирует executable
`TypeMapper` artifact целиком, поскольку infrastructure dispatch mapper-а уже
невозможно испустить без C# member conflict. Independently legal
construction/member/extension surfaces известных registrations сохраняются,
а категории 3–12 mapper-а подавляются так же, как при
`MORPH0006`–`MORPH0008`.

### 6.18. Precedence, порядок и suppression

`MORPH0005` подавляет остальные mapper diagnostics. Без него независимые
`MORPH0006`, `MORPH0007` и `MORPH0008` могут публиковаться вместе; наличие хотя
бы одной из них либо `MORPH0034` подавляет pair-local
`MORPH0009`/`MORPH0010` и категории 3–12 затронутого mapper-а. Для одной
canonical pair exact `MORPH0009` имеет приоритет над unifiable `MORPH0010`.
Pair-local contract conflict не скрывает independent duplicate/eligibility
либо builder-flow reason, но останавливает анализ содержимого mapping plan,
который generator всё равно не сможет испустить.

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

- exact descriptors `MORPH0005`–`MORPH0010` и `MORPH0034`: ID, title,
  category, default severity, enabled/configurable flags, message formats и
  parameters;
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
- собственные `Supports(System.Type, System.Type)` с `override`, `new`,
  `static`, отличающимся return type/accessibility, несколько partial origins,
  exact primary/additional locations и единственную `MORPH0034`; разрешённые
  inherited `Supports` и собственные non-conflicting overloads;
- exact primary/additional locations, canonical message normalization,
  deduplication первой `Map` registration и стабильный порядок diagnostics;
- полный generated result каждого recovery: отсутствие executable artifact
  при mapper-wide failure, сохранение independently legal DSL surfaces,
  исключение только конфликтующей pair и сохранение независимых pairs;
- suppression/изменение severity без возобновления запрещённого artifact и
  отсутствие diagnostics категорий 3–12, которые стали недостоверными;
- add/change/remove/restore `TypeMapper` base, `partial`, containing/file-local
  modifiers, direct interface graph и own `Supports` signature при одном
  сохранённом incremental driver-е.

Package-like integration-категория независимо проверяет:

- compilable generated contracts для abstract, closed generic, nested,
  private/protected и non-sealed mapper forms;
- non-partial mapper, non-partial container и file-local chain с exact
  diagnostics без каскадных C# errors в сохранённых DSL surfaces;
- собственный конфликтующий `Supports` с отсутствующим executable mapper
  artifact, сохранёнными pair-owned DSL surfaces и без duplicate-member error
  из `.g.cs`; inherited base override остаётся исполнимым;
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
`Nullable<T>`-обёртка. В core v0 единственной unsupported root category
остаётся root type parameter независимо от constraints. Стабильная reason
phrase `{2}` равна `a root type parameter`.

Tuple, array, sequence/collection/buffer, delegate, expression-tree,
task/deferred и observable roots являются eligible. Если любой из этих roots
делает pair opaque, pair получает полный `ITypeMapper<,>` contract и
pair-specific `ConstructUsing`, `ResolveUsing` и `Convert`, но не получает
structured `Construct` / `Resolve`, `Members` либо conventions. Эти roots
участвуют в canonical duplicate и unification-анализе наравне с другими
eligible pairs; Morphant не приписывает collection element, await, invocation
или subscription semantics единому opaque value.

`Envelope<Task<T>>`, `Page<List<int>>` и другие nameable nominal roots с такой
формой только внутри generic arguments следуют capability model outer type-а.
`string` остаётся обычным scalar root. Nullable reference annotations и снятая
верхнеуровневая `Nullable<T>`-обёртка не меняют классификацию type parameter.

### 6.24. `MORPH0012`: unsupported mapping root

Diagnostic публикуется для каждой root-позиции первой registration pair,
прошедшей nameability gate и являющейся root type parameter по разделу 6.23.
Primary location — полный syntax соответствующего type argument; additional
locations отсутствуют. Две unsupported позиции одной pair дают две
diagnostics с одинаковым ID и разными role, type name и location.

Pair сохраняет полный executable
`ITypeMapper<TSource, TDestination>` contract. Обе операции независимо от
effective `MappingMode` бросают `MappingConfigurationException` с
детерминированной причиной: сначала source, затем destination. Generated
construction, member и pair-extension surfaces отсутствуют, включая
`ConstructUsing`, `ResolveUsing` и `Convert`; unsupported registration не
получает скрытый opaque, manual либо runtime fallback.

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
  constructed generic с type parameter внутри известного nominal root;
- positive opaque-capability matrix: tuple/`ValueTuple`/`ITuple`, array,
  sync/async collection и buffer, delegate, expression tree, task/value-task/
  lazy, observable и nullable wrappers; exact `ITypeMapper`,
  `ConstructUsing` / `ResolveUsing` / `Convert` и отсутствие structured /
  `Members` / convention surfaces;
- private, private-protected, protected и file-local root, containing type и
  nested generic argument; разрешённые public/internal/protected-internal
  forms; обе roles, exact type-argument locations и pair-local deduplication
  `MORPH0011`;
- malformed generic arguments с достаточной C# diagnostic без дублирующей
  Morphant diagnostic и сохранение независимой legal pair;
- root type parameter напрямую и под `Nullable<T>` там, где C# допускает
  constructed form, source и destination, обе позиции одновременно, exact
  role/reason parameters и suppression semantic root diagnostic при
  `MORPH0011`;
- отсутствие `MORPH0012` для всех opaque roots и для такой категории только
  внутри legal nominal root, включая `string` и пользовательские
  implementations отложенных contracts;
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
  root type parameter, executable opaque contracts с runtime/manual surface,
  first-plan ownership duplicate registration, исключение всех unification
  participants, сохранение legal DSL surfaces и независимых pairs;
- suppression/изменение severity без изменения recovery и отсутствие
  недостоверных downstream diagnostics;
- add/change/remove/restore accessibility, root category, duplicate и
  unifiable shape при одном сохранённом incremental driver-е.

Package-like integration-категория независимо проверяет:

- unavailable nested/file-local type с exact `MORPH0011`, отсутствующими
  artifacts только затронутой pair и сохранённой независимой pair;
- root type parameter хотя бы в одной из двух positions, обе positions вместе
  и реальный вызов обеих операций полного suppressed-error contract-а с
  `MappingConfigurationException`; construction/member/extension surfaces
  отсутствуют;
- каждую прежнюю unsupported root family как реальную opaque pair: runtime
  result policy и `Convert` исполняются, structured/member surfaces не
  компилируются, а automatic collection/await/invocation semantics не
  появляются;
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

Arguments и bodies уже распознанных `Construct`, `Resolve`,
`ConstructUsing`, `ResolveUsing`, `Members` и `Convert` callbacks не обходятся
категорией 4. Весь callback argument, включая conditional delegate
expression, cast, method group и materialized delegate, передаётся категории
8 как единое содержимое. Ссылка на внешний `builder` внутри callback-а
является lifetime capture, а не builder-flow escape. Nested `Map` / `Create` /
`Update` markers внутри structured callbacks принадлежат категории 11 после
успешной transfer/grammar-проверки.

Pair-specific method, которого нет на generated capability surface pair,
остаётся обычной compiler diagnostic: категория 4 не синтезирует отсутствующий
`Construct` / `Resolve` / `Members` и не переименовывает такой вызов в flow
break.

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
- состав result-policy slot (`Construct` / `Resolve` / `ConstructUsing` /
  `ResolveUsing`), `Members` и `Convert` одной pair и допустимость их сочетания
  — категория 5;
- значения arguments Morphant settings и их применимость — категория 6;
- переносимость callback arguments/bodies, locals, captures и control flow
  внутри них — категория 8;
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
- отсутствие category-4 обхода arguments/bodies всех шести callback families,
  включая `builder` capture и conditional/materialized delegate, и правильное
  сохранение ownership категорий 5–11;
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
- все шесть callback families, включая conditional/materialized runtime
  delegate и callback capture самого `builder`, не создают category-4
  diagnostic; одноимённый сторонний API не влияет на Morphant generation;
- реальное `.editorconfig`/MSBuild suppression или severity override для
  каждой recovery-family без изменения generated artifact set.

### 6.39. Категория 5: общий contract

Категория «Локальная композиция mapping plan» содержит ровно две diagnostics:

| ID | Title | Message format |
|---|---|---|
| `MORPH0019` | `Duplicate mapping plan slot` | `Mapping plan slot '{0}' is configured more than once for contract '{1}' in mapper '{2}'.` |
| `MORPH0020` | `Convert cannot be combined with result policy or Members` | `Convert cannot be combined with a result policy or Members for contract '{0}' in mapper '{1}'.` |

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

Slot name `{0}` в `MORPH0019` равно `result policy`, `Members` или `Convert`.
Mapping contract форматируется по canonical identity категории 3 как
`global::Morphant.ITypeMapper<{canonicalSource}, {canonicalDestination}>`, а
mapper type — по fully-qualified nullable-aware display категории 2.

### 6.40. Slot identity и допустимая локальная композиция

Local slot occurrence — успешно связанный Morphant-вызов в authoritative chain
конкретной registration. Четыре result-policy families `Construct`, `Resolve`,
`ConstructUsing` и `ResolveUsing` занимают один общий slot. `Members` и
`Convert` образуют ещё по одному slot. Разные generated overloads одного
метода не меняют identity; порядок occurrences соответствует fluent chain
слева направо, а иные pair methods между ними порядок не меняют.

Допустимы ровно следующие локальные наборы slots:

- ни одного slot;
- одна result policy;
- один `Members`;
- одна result policy и один `Members` в любом порядке;
- один `Convert` без result policy и `Members`.

Второй и каждый следующий occurrence одного slot создаёт `MORPH0019`.
Например, `Construct(...).Resolve(...)` и две разные overload-ы
`ConstructUsing` являются одинаковым duplicate result-policy slot. Наличие
`Convert` вместе хотя бы с одной result policy либо `Members` независимо от
порядка создаёт `MORPH0020`.

В категорию 5 входят только invocations, которые semantic model однозначно
связал с настоящим Morphant pair API. Одноимённые сторонние methods
игнорируются. Invocation с неразрешившейся или неоднозначной overload либо с
callback conversion, ошибочность которой уже полностью объясняет C# compiler,
не считается slot occurrence и не получает дублирующую Morphant diagnostic.

Pair-level settings не являются plan slots; корректность их значений и
применимость к manual/declarative plan относится к категории 6. `IncludeBase`
и любые перенесённые им plan slices также не считаются local occurrences; их
взаимодействие с локальным plan относится к категории 7. Содержимое успешно
связанных callbacks категория 5 не анализирует: переносимость и семантика всех
шести callback families принадлежат категориям 8–11.

### 6.41. `MORPH0019`: duplicate mapping plan slot

Diagnostic публикуется на втором и каждом следующем локальном invocation
одного slot в authoritative chain pair. Primary location — identifier
текущего лишнего `Construct`, `Resolve`, `ConstructUsing`, `ResolveUsing`,
`Members` либо `Convert`; единственная additional location — identifier
первого invocation того же slot.

Три occurrences дают две diagnostics: на втором и третьем, обе со ссылкой на
первый. Для result-policy slot имена могут различаться:
`Construct(...).ResolveUsing(...).ConstructUsing(...)` даёт diagnostics на
`ResolveUsing` и `ConstructUsing`, обе с additional location первого
`Construct`. Смешение overloads одной family следует тому же закону.

Diagnostic identity включает mapper, authoritative registration, slot и
location конкретного лишнего invocation. Одинаковые duplicates в
разных canonical pairs независимы. Chains поздних registrations, уже
отброшенные `MORPH0013`, не анализируются и собственных `MORPH0019` не
получают.

`MORPH0019` и `MORPH0020` являются независимыми причинами. Например, два
`Convert` вместе с `ResolveUsing` дают одну duplicate diagnostic на втором
`Convert` и одну mixed diagnostic на pair: исправление любой одной причины не
должно впервые открывать вторую.

### 6.42. `MORPH0020`: `Convert` и declarative-plan slots

Diagnostic публикуется ровно один раз на authoritative pair, содержащую хотя
бы один локальный `Convert` и хотя бы одну локальную result policy либо
`Members`. Количество invocations каждого slot не увеличивает cardinality
`MORPH0020`; duplicates независимо публикуют собственные `MORPH0019`.

Primary location — identifier первого invocation стороны конфликта,
появившейся второй: manual side содержит `Convert`, declarative-plan side —
общий result-policy slot и `Members`. Поэтому
`Resolve(...).Convert(...)` указывает на `Convert`, а
`Convert(...).ConstructUsing(...)` — на `ConstructUsing`. Если до первого
`Convert` уже встретились result policy и `Members`, primary остаётся первым
`Convert`; если после `Convert` declarative plan начинается с `Members`,
primary — этот `Members` независимо от последующей result policy.

Additional locations содержат identifiers первых участвующих result policy,
`Members` и `Convert` в фиксированном slot-порядке `result policy`, `Members`,
`Convert`; отсутствующий slot пропускается. Span, совпадающий с primary
location, также сохраняется, чтобы additional list полностью описывал первый
локальный состав конфликтующего plan.

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
slot к одной operation.

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
перестановка, смена result-policy family либо overload любого plan slot
полностью пересчитывается при actualization без сохранения прежнего conflict
state.

### 6.44. Самостоятельная тестовая матрица категории 5

Unit-категория composition независимо фиксирует:

- exact descriptors `MORPH0019`–`MORPH0020`: ID, title, category, default
  severity, enabled/configurable flags, message formats и slot/contract/
  mapper parameters;
- отсутствие slots, каждую из четырёх result-policy families отдельно, один
  `Members`, один `Convert` и result policy + `Members` в обоих порядках как
  полную positive matrix;
- два и три вызова каждого slot, все пары разных result-policy names,
  смешение overloads, interleaved pair settings, exact primary/first-slot
  additional locations и две diagnostics для трёх duplicates `MORPH0019`;
- `Convert` + каждая result policy, `Convert` + `Members` и все три slots в
  каждом значимом порядке, exact first-second-side primary location,
  фиксированный полный additional-location list и ровно одну `MORPH0020` на
  pair;
- совместные duplicate и mixed conflicts, включая два `Convert` +
  `Construct`, с независимой cardinality обоих IDs;
- semantic exclusion одноимённых сторонних methods, compiler-owned
  unresolved/ambiguous overloads и invalid callback conversions;
- анализ только первой authoritative registration, отсутствие diagnostics в
  отброшенных `MORPH0013` chains и независимую pair с собственным plan;
- exclusion `IncludeBase` и всех imported plan slices из local slot set,
  ownership pair settings и отсутствие обхода callback contents;
- полный generated result recovery: complete mapper contract и legal DSL
  surfaces, обе throwing operations независимо от `MappingMode`, отсутствие
  выбранного first/last/manual/declarative fallback и сохранение независимой
  исполнимой pair;
- precedence с gates `MORPH0001`–`MORPH0018`, подавление недостоверных
  downstream diagnostics и сохранение независимо доказуемых соседних причин;
- deterministic order, suppression/изменение severity без изменения recovery
  и generated artifact set;
- add/remove/reorder/replace каждого slot, result-policy family и overload при
  одном сохранённом incremental driver-е.

Package-like integration-категория независимо проверяет:

- suppressed duplicate каждого slot, включая mixed-name result policies:
  mapper и DSL surfaces
  компилируются, обе operations бросают `MappingConfigurationException`, ни
  первый, ни последний callback не исполняется;
- suppressed `Convert`/declarative-plan conflict в обоих порядках: обе
  operations бросают без выбора slot, а независимая pair того же mapper-а
  остаётся исполнимой;
- каждая result policy + `Members` остаётся исполнимой declarative
  composition, а local slot рядом с imported plan не получает ложную category-5
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

| Setting | Declarative pipeline | Manual `Convert` |
|---|---|---|
| `MappingMode` | Применяется ко всему contract и задаёт enabled operations | Единственная применимая effective setting |
| `NullSourceHandling` | Применяется ко всем enabled operations до structured либо runtime result policy и `Members` | Не применяется |
| `NullDestinationHandling` | Применяется только к enabled `Update` до normalization `previous` для `Resolve` / `ResolveUsing` / `Members` | Не применяется |
| `MemberSelection` | Управляет supported body-members после structured либо runtime result policy; отсутствие кандидатов не отменяет applicability | Не применяется |
| `ConstructorSelection` | Применяется только к reachable convention либо explicit `ByConvention` creation path | Не применяется |
| `UnmappedMemberValidation` | Применяется только к category-12 warning-анализу effective Morphant-built plan; runtime result callback сам не анализируется как member plan | Не применяется |

`ConstructUsing` и `ResolveUsing` остаются result policies declarative
pipeline: они получают normalized inputs после null handling, а выбранный ими
result затем проходит применимый общий `Members` plan и convention rules, если
pair имеет соответствующую capability. Opaque pair остаётся eligible pair с
runtime result-policy и manual capabilities; opacity сама по себе не выбирает
`Convert`. При declarative model null policies и остальные declarative
settings сохраняют свою stage semantics, хотя structured construction и
`Members` surface у opaque pair отсутствуют.

Nullability source/destination и фактическое отсутствие `null` во время
исполнения не скрывают invalid null policy: наличие соответствующей enabled
declarative operation является достаточной статической применимостью.
Аналогично отсутствие convention member candidates не делает
`MemberSelection` либо `UnmappedMemberValidation` неприменимыми. Для
`ConstructorSelection` необходим именно reachable convention/`ByConvention`
creation path; полностью explicit branches, runtime result policy и Update
существующего destination от этой setting не зависят.

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
- у pair без structured construction capability, включая opaque,
  interface/abstract/factory-only destination без поддерживаемого constructor
  surface, неприменима explicit `ConstructorSelection`;
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
pair без structured construction capability — соответствующий destination
type argument authoritative `Map<TSource, TDestination>`. В `{1}` передаётся
соответственно `manual Convert` либо
`mapping without structured construction capability`; `{2}` и `{3}` содержат
canonical contract и mapper type.

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
молча и не переключает manual mapping либо mapping без structured capability
на declarative, structured или convention fallback.

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
- статическую applicability matrix structured/runtime-result/manual,
  enabled/disabled operations, nullability-independent null policies,
  `ConstructUsing` / `ResolveUsing` с последующим `Members`, convention/
  explicit/`ByConvention` paths, opaque pairs и category-12-only
  `UnmappedMemberValidation`;
- полностью перекрытые и inactive origins без diagnostic, один origin с
  несколькими affected paths и совместную публикацию нескольких независимо
  invalid effective settings;
- все пять запрещённых explicit settings у `Convert`, explicit
  `ConstructorSelection` у pair без structured construction capability,
  включая opaque/interface/abstract/factory-only destination, `Default`,
  last-call-wins, одну `MORPH0023` на setting и отсутствие её у
  inherited/root policies;
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
- manual `Convert` с каждой suppressed неприменимой setting и pair без
  structured construction capability с `ConstructorSelection`: обе
  operations бросают, callbacks не выполняются, независимая pair остаётся
  исполнимой;
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
| `MORPH0028` | `Inherited mapping callback is inaccessible` | `Inherited {0} callback for contract '{1}' cannot be accessed from mapper '{2}'.` |

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
  недоступный inherited callback исполнимым.

Mapping contracts в messages форматируются по canonical identity категории 3
как `global::Morphant.ITypeMapper<{canonicalSource},
{canonicalDestination}>`, mapper types — по fully-qualified nullable-aware
display категории 2. `{0}` в `MORPH0027` равно `source` либо `destination`, а
типы `{1}` и `{2}` используют соответствующий fully-qualified display.
Callback name `{0}` в `MORPH0028` — точное имя family `Construct`, `Resolve`,
`ConstructUsing`, `ResolveUsing`, `Members` либо `Convert`.

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
| Cross-pair, например `Dog -> DogDto` из `Animal -> AnimalDto` | Effective `Members` plan: member rules и compile-time source discards из `Members`; никакая result policy, source discard из result policy и `Convert` не импортируются, conventions и constructor selection вычисляются заново для current pair |
| Exact same-pair на connected base level | Весь applicable effective plan: одна из `Construct` / `Resolve` / `ConstructUsing` / `ResolveUsing`, `Members` либо `Convert`, без casts и callback adapters |

Exact same-pair не переносит выбранный runtime result через type boundary:
source и destination types совпадают, поэтому inherited callbacks сохраняют
исходные delegate contracts. В частности, opaque exact same-pair может
наследовать `ConstructUsing`, `ResolveUsing` либо `Convert`. Cross-pair никогда
не пытается привести result любой base result policy/`Convert` к более
конкретному destination и не меняет поведение в зависимости от structured или
runtime callback class.

Локальные slots разрешают model после импорта так:

| Local plan | Effective behavior |
|---|---|
| Нет локальной result policy, `Members` или `Convert` | Exact same-pair полностью сохраняет inherited plan; cross-pair использует imported `Members` plan и заново построенные current conventions/construction |
| Локальный `Convert` | Полностью заменяет inherited mapping plan; imported settings остаются в precedence chain, но неприменимые manual policies следуют категории 6 |
| Локальная result policy и/или `Members` | Выбирает declarative model и отбрасывает inherited `Convert` |
| Declarative plan с обеих сторон | Inherited result policy любого имени является fallback; локальная result policy любого другого либо того же имени перекрывает её как общий slot. `Members` объединяются по destination member с локальным приоритетом |

Imported и local `Members` объединяются независимо от формы overload-а.
Local expression, `Auto()` либо `Ignore()` перекрывает inherited rule того же
destination member; conventions заполняют только остаток. Dependencies
строятся заново для оставшихся effective rules. Compile-time source discard из
`Members` является independent plan item, сохраняется при cross-pair import и
дедуплицируется по exact source member после substitutions; source discard из
cross-pair result policy не импортируется. Imported plan slices не
становятся local slots категории 5: inherited `Construct` рядом с local
`ResolveUsing` не является duplicate, а намеренно отброшенный inherited
`Convert` рядом с local declarative plan не создаёт mixed-model diagnostic.

Composition транзитивна, но каждый edge переносит только свой effective
slice. Поэтому ошибка base plan влияет на consumer только если consumer
действительно сохраняет соответствующий slice:

- cross-pair consumer не зависит от любой base result policy/`Convert` и их
  ошибок;
- local `Convert` отбрасывает imported declarative plan;
- local declarative plan отбрасывает inherited `Convert`, а локально
  перекрытая result policy или member rule удаляет заменённый slice;
- invalid included settings сохраняют ownership и policy-specific recovery
  категории 6;
- ambiguity либо invalid composition оставшегося imported slice делает
  transitive consumer неисполнимым, но origin diagnostic не размножается по
  каждому consumer-у.

Только после model precedence проверяется доступность оставшихся inherited
callbacks из конечного mapper-а. Effective callback любой из шести families
испускается в его generated partial type. Private base mapper members, явный
`base.` и иные references, доступность которых потеряна именно при переносе из
originating base mapper-а в target mapper, создают `MORPH0028`. Полностью
перекрытый или отброшенный callback и его dependencies не проверяются и
diagnostic не получают.

File-local symbol и иная форма, физически неназываемая из любого generated
syntax tree, не является inheritance-specific accessibility loss и
принадлежит общему transfer failure `MORPH0030`. Остальная переносимость
callback syntax, captures, extension binding и structured grammar также
принадлежит категории 8.

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
по type arguments edge. Поэтому lookup, compatibility и inherited-callback
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

### 6.60. `MORPH0028`: inaccessible inherited callback

Diagnostic публикуется для каждого originating inherited `Construct`,
`Resolve`, `ConstructUsing`, `ResolveUsing`, `Members` либо `Convert`
invocation, чей callback остался effective в конечной pair и содержит хотя бы
одну reference, доступную из originating mapper-а, но недоступную из generated
partial конечного mapper-а.

Primary location — identifier effective `IncludeBase` конечной pair.
Additional locations имеют детерминированный порядок:

1. identifier originating callback invocation;
2. все недоступные reference expressions этого callback-а в source order.

Несколько inaccessible references одного callback-а дают одну diagnostic с
несколькими additional locations. Разные originating invocations дают
отдельные diagnostics. Один origin, достигший двух конечных constructed pairs,
может получить две `MORPH0028`, поскольку target accessibility и recovery
являются context-dependent; transitive промежуточный consumer без generated
contract сам по себе fan-out не создаёт.

В `{0}` передаётся callback family, `{1}` — конечный contract, `{2}` —
конечный mapper. Diagnostic identity включает конечный constructed node и
origin invocation. Primary intentionally указывает на composition boundary,
а additional locations показывают конфигурацию и точные inaccessible
references, которые нужно сделать доступными либо перекрыть.

Локальный `Convert`, declarative/manual precedence, локальная result policy и
member-level override применяются до этой проверки. Поэтому discarded base
`Convert`, заменённая result policy, полностью перекрытые member expressions и
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
каждому transitive consumer-у: duplicate slot, invalid setting либо другая
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
- independently provable local slot conflicts категории 5 и local setting
  origins категории 6 сохраняются; invalid local model подавляет только ту
  часть inherited-callback анализа, которой нужен однозначный effective
  plan;
- general callback portability категории 8 и construction/member/nested
  semantics категорий 9–11 анализируются только для оставшегося effective
  inherited plan; `MORPH0028` владеет исключительно accessibility, утраченной
  при переносе callback между mapper-level-ами;
- `UnmappedMemberValidation` управляет только категорией 12 и не скрывает
  inheritance errors.

Независимые `MORPH0024` и `MORPH0025` могут публиковаться совместно. Две
relations `MORPH0027`, несколько inaccessible callbacks `MORPH0028` и
независимые pairs сохраняют собственную cardinality даже при общем throwing
recovery.

Publication order — по ID. Внутри ID diagnostics упорядочиваются по ordinal
stable final mapper identity, configuration-level order от derived к base,
canonical pair, source location primary и relation/callback name. Origin-only
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
- cross-pair import только effective `Members` plan, включая retained source
  discards из `Members`, повторное применение current conventions/construction
  и отсутствие переноса любой result policy, source discard из неё и
  `Convert`;
- exact same-pair full-plan inheritance отдельно для `Construct`, `Resolve`,
  `ConstructUsing`, `ResolveUsing`, declarative `Members` и manual `Convert`,
  включая opaque runtime-policy pair, без casts/adapters;
- полную local-model matrix: no slots, local `Convert`, local
  result policy каждой family, local `Members`, result policy + `Members`,
  inherited declarative/manual plans, cross-name result-policy
  fallback/override и member-level expression/`Auto`/`Ignore` precedence;
- slice-sensitive propagation: ignored cross-pair result-policy/`Convert`
  errors, local `Convert` dropping declarative errors, local declarative plan
  dropping inherited `Convert`, overridden policies/expressions and transitive
  error recovery without origin diagnostic fan-out;
- `MORPH0028` для effective inherited callbacks всех шести families:
  private member, explicit `base.`, public/internal/protected positives,
  multiple inaccessible references in one diagnostic, multiple callback
  origins and context-dependent constructed consumers;
- отсутствие `MORPH0028` у discarded `Convert`, overridden result policy,
  полностью overridden member rules/dependencies, local callback и
  file-local symbol; правильный boundary с category-8 transfer/grammar;
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
  `MORPH0027` и каждая из шести callback families `MORPH0028`: affected pair и
  transitive consumer бросают без fallback, independent pair исполняется;
- exact same-pair inheritance реально исполняет base result policy, `Members`
  либо `Convert`, local model precedence заменяет их по таблице раздела 6.55,
  а cross-pair переносит только `Members` plan, включая compile-time source
  discards без runtime getter evaluation;
- same-level/nearest-level/transitive lookup, generic/nested mapper
  substitution и class/interface/boxing compatibility на обычном
  analyzer-backed consumer build;
- inaccessible base helpers не исполняются, полностью перекрытые callbacks
  не блокируют mapping, а origin diagnostics imported errors не размножаются;
- реальное `.editorconfig`/MSBuild suppression или severity override для всех
  пяти IDs без изменения generated artifact set и effective recovery.

### 6.63. Категория 8: общий contract

Категория «Переносимость callbacks и declarative grammar» содержит ровно пять
diagnostics:

| ID | Title | Message format |
|---|---|---|
| `MORPH0029` | `Structured callback must be a lambda` | `Structured {0} callback for contract '{1}' must be an inline lambda.` |
| `MORPH0030` | `Callback cannot be transferred` | `{0} callback for contract '{1}' cannot be transferred to generated mapper '{2}': {3}.` |
| `MORPH0031` | `Unsupported structured callback syntax` | `Structured {0} callback for contract '{1}' contains unsupported syntax '{2}'.` |
| `MORPH0032` | `Structured destination input is read-only` | `Structured destination input '{0}' for contract '{1}' is read-only and cannot be mutated.` |
| `MORPH0033` | `Invalid compile-time marker use` | `Compile-time marker '{0}' cannot be used as a runtime value or outside a supported terminal DSL position in {1} callback for contract '{2}'.` |

Для всех пяти diagnostics действует общий descriptor contract:

- category — `Morphant.Callbacks`;
- default severity — `Error`;
- diagnostic включена по умолчанию и не имеет `NotConfigurable`;
- description и help link отсутствуют, custom tags пусты;
- анализируется только оставшийся effective callback plan после категорий
  4–7;
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
`ConstructUsing`, `ResolveUsing`, `Members` либо `Convert`; marker names —
`Value`, `Auto`, `Ignore`, `Map`, `Create`, `Update`, `ByConvention` либо
`MappingContextMarker` без generic arity.
Unsupported syntax получает стабильное англоязычное имя пользовательской
конструкции, например `for statement`, `assignment` либо `local function`, а
не внутреннее имя Roslyn operation/syntax kind.

Категория 8 не выполняет configuration code и не интерпретирует arbitrary C#
как declarative plan. Она использует bound generated overload и symbol
identity: одноимённые delegates, methods и marker-подобные вызовы стороннего
API не принадлежат Morphant callback grammar.

### 6.64. Structured и runtime callback classes

Bound overload однозначно задаёт callback class:

| Callback | Class | Анализ |
|---|---|---|
| Обе `Construct` overloads | Structured DSL | Inline lambda и construction-plan grammar |
| Обе `Resolve` overloads | Structured DSL | Inline lambda и construction-plan grammar |
| Все четыре `Members` overloads | Structured DSL | Inline lambda и member-plan grammar |
| Обе `ConstructUsing` overloads | Runtime | Обычный переносимый C# callable; no-previous result policy |
| Обе `ResolveUsing` overloads | Runtime | Обычный переносимый C# callable; full result selector |
| Все три `Convert` overloads | Runtime | Обычный переносимый C# callable; полная manual model |

Structured lambda поддерживает expression body и конечную outer block grammar:

- initialized locals, `const`, pattern variables и nested blocks;
- полные `if` / `else if` / `else`, statement `switch`, несколько `return` и
  явный `throw`;
- conditional/switch/query expressions, standard bound `System.Linq` query
  operators, nested lambdas и local function, объявленную целиком внутри
  переносимой deferred lambda;
- object initializer construction plan и `DestinationMembers` `with` overlay;
- compile-time source discard точной формы `_ = source.Member;` как direct
  statement callback body structured lambda, где `_` семантически является
  discard, receiver — exact source parameter текущего callback-а, а `Member`
  — direct supported source property либо field;
- обычные expressions, доступные mapper/static members, compile-time
  Configure constants, caller-info calls, `nameof(...)`, ambient unsafe
  pointer/function-pointer expressions и collision-safe declarations;
- terminal `Value`, `Auto`, `Ignore`, `Map`, `Create`, `Update` и
  `ByConvention` в plan positions, семантику которых проверяют категории
  9–11.

За structured outer boundary остаются local без initializer-а, последующая
mutation local-а, deconstruction/compound assignment, `++` / `--`, loops,
`break` / `continue`, side-effect-only statements кроме точного compile-time
source discard, local function внешнего structured block-а, `try` / `catch` /
`finally`, `using`, `lock`, labels /
`goto`, `ref` / `using` local, outer `unsafe` / `fixed`, `await` и `yield`.
Точная C# binding error принадлежит compiler-у; `MORPH0031` появляется только
для успешно связанного structured callback-а, который generator не может
понизить по своей DSL grammar.

Runtime callback принимает expression/block lambda, включая `async` /
`static async` и ambient/local unsafe code, natural method group, conditional
delegate expression либо materialized delegate. Внутри допустим обычный C#:
locals, assignment/mutation, loops, `try` / `finally`, nested local functions,
constructors, factories и record `with`. Ограничение задаёт не statement
grammar, а точное сохранение binding/lifetime и отсутствие compile-time
markers в исполняемом callable.

Для обоих classes перенос обязан сохранять исходный C# binding, lexical
warning/nullable state, modifier `async`, необходимый unsafe context, caller-
info values и evaluation count. Symbols квалифицируются по semantic binding;
поддерживаемый conditional extension call lower-ится с однократным вычислением
receiver-а. Standard LINQ query сохраняется, а extension method group, custom
extension query/pattern binding и иная форма, которую нельзя доказуемо
перенести, завершаются `MORPH0030`, а не ошибкой из `.g.cs`.

Mapper instance/static members, static API, type references, method groups и
compile-time constants переносимы. Parameters текущего callback-а, structured
locals/pattern variables и local function внутри переносимой runtime/deferred
lambda являются mapping-time values. Configure-local runtime value, параметр
`builder`, external local delegate/function и транзитивная зависимость от них
не существуют в lifetime mapping execution. Delegate из доступного mapper
field/property допустим; Configure-local delegate — нет.

`previous`, `result` и чтение `MappingContextMarker.Operation` нельзя
откладывать во вложенную lambda, anonymous method либо local function. Прямо
вычисленный ordinary snapshot и извлечённый `MappingOperation` являются
переносимыми values; попытка перенести сам marker object принадлежит
`MORPH0033`. Transparent parentheses, null-forgiving expression и явный cast к
ожидаемому delegate type не меняют class/origin либо inline-lambda identity.
Полностью перекрытый, отброшенный model precedence либо доказанно
недостижимый callback slice не анализируется.

### 6.65. `MORPH0029`: structured callback должен быть inline lambda

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

`ConstructUsing`, `ResolveUsing` и `Convert` той же формы `MORPH0029` не
получают: они являются runtime callbacks и могут быть method group,
conditional либо materialized delegate. Inline structured lambda с cast,
parentheses либо null-forgiving wrapper остаётся inline.

Поскольку внутренний plan non-lambda callback-а недоступен, `MORPH0029`
подавляет `MORPH0030`–`MORPH0033` того же callback origin. Весь effective
structured fragment становится invalid; generator не вызывает callback во
время `Configure` и не подставляет conventions вместо него.

### 6.66. `MORPH0030`: callback нельзя перенести без изменения C# semantics

`MORPH0030` является общим transfer failure. Она публикуется, когда effective
structured либо runtime callback или его достижимый slice успешно связан C#
compiler-ом, но generator не может доказуемо воспроизвести его binding,
lifetime либо lexical context в generated mapper. К причинам относятся:

- Configure-local runtime value, parameter enclosing function, сам `builder`,
  его alias, external local delegate/function или транзитивная зависимость от
  такого symbol;
- deferred capture `previous` / `result` либо deferred evaluation
  `MappingContextMarker.Operation` во вложенной lambda, anonymous method или
  local function; предварительно вычисленные ordinary result/operation
  snapshots допустимы;
- file-local type/member либо другой symbol, который C#-доступен в исходном
  callback-е, но физически неназываем из generated syntax tree;
- extension method group, custom extension query/`foreach` pattern или иное
  binding, которое нельзя квалифицировать/lower-ить без изменения overload
  choice, null propagation, evaluation count или language semantics;
- warning/error, впервые появившаяся в compiler preflight после rewrite и не
  существовавшая в исходном callback context.

Стабильный `{3}` reason имеет одну из форм:

- `configuration-time value '{symbol}' is unavailable during mapping`;
- `destination input '{previous|result}' cannot be captured by deferred code`;
- `MappingContextMarker.Operation cannot be evaluated by deferred code`;
- `file-local symbol '{symbol}' is unavailable to generated code`;
- `extension binding for '{construct}' cannot be preserved`;
- `transferred code introduces compiler diagnostic '{id}'`;
- `C# binding cannot be preserved` для неатрибутируемого fail-closed остатка.

`{symbol}` использует стабильный symbol display, а `{construct}` —
пользовательское имя формы (`extension method group`, `custom query pattern`,
`extension foreach pattern`), не raw Roslyn kind и не полный source text.

Для symbol/capture failure primary location — первая effective reference
внутри callback-а; declaration symbol-а и остальные references становятся
additional locations в source order. Для binding failure primary указывает на
наименьший offending source node. Compiler preflight обязан сопоставить
diagnostic исходному callback node через probe location; если точная
атрибуция невозможна, primary — callback argument, а весь fragment считается
атомарно invalid. Diagnostic identity включает callback origin, reason kind и
offending symbol/node/diagnostic ID; повторные references одной причины
дедуплицируются, независимые причины публикуются отдельно.

Исходная видимая compiler warning не является transfer failure: она остаётся
compiler-owned, а её generated duplicate узко подавляется на перенесённом
фрагменте. Локальные `#pragma warning` и nullable warning state переносятся
только на affected generated callback. `MORPH0030` появляется лишь для новой
проблемы generated form; существующая source binding/type error не получает
дублирующую Morphant diagnostic.

Generator не замораживает configuration-time value, не вызывает local
function во время generation, не добавляет namespace-wide `using`, способный
изменить соседний overload binding, и не оставляет failing source в `.g.cs`.

### 6.67. `MORPH0031`: неподдерживаемая structured syntax

`MORPH0031` публикуется для каждого внешнего независимого syntax node, который
находится внутри effective structured lambda, успешно связан C# compiler-ом,
но не входит в grammar раздела 6.64. В частности, diagnostic получают:

- local без initializer-а, последующая/deconstruction/compound assignment
  structured local-а и `++` / `--`;
- `for`, `foreach`, `while`, `do`, `break` и `continue`;
- invocation, assignment либо другая statement-expression только ради side
  effect, кроме exact compile-time source discard `_ = source.Member;`;
- local function во внешнем declarative block;
- `try` / `catch` / `finally`, `using`, `lock`, label и `goto`;
- `ref` / `using` local, outer `unsafe`, `fixed`, `await` и `yield`.

Primary location — наиболее конкретный keyword/operator либо полный node,
если у конструкции нет отдельного устойчивого token-а. `{2}` в message
называет именно этот внешний node. Additional locations нет. Diagnostic
identity — syntax origin; несколько независимых outer nodes дают отдельные
diagnostics.

После diagnostic generator не обходит вложенное содержимое уже
неподдерживаемого `for`, `try`, local function и аналогичного outer node ради
каскада дополнительных grammar diagnostics. Mutation previous/result на том
же site принадлежит более точной `MORPH0032`; обычная mutation structured
local/source остаётся `MORPH0031`.

Поддерживаемые expressions внутри возвращаемого plan-а сохраняют обычную C#
семантику и side effects при фактическом вычислении. `MORPH0031` не является
общим purity analyzer-ом и не пытается классифицировать вызываемый method как
mutating; она ограничивает только явно наблюдаемую structured statement и
mutation grammar.

Compile-time source discard является единственным специальным standalone
assignment statement. Он распознаётся только как direct child statement list-а
body structured `Construct`, `Resolve` либо `Members`, когда left-hand `_`
связан как настоящий C# discard, а right-hand side после снятия только
parentheses является прямой property/field reference exact source parameter-а
текущего callback-а. Member обязан входить в category-12 supported source
universe. Generator сохраняет source-discard observation, но полностью удаляет
statement из runtime lowering: receiver и getter не вычисляются.

Member chain, indexer, conditional access, conversion, invocation, tuple,
несколько expressions, reference через alias либо `previous` / `result`,
control-flow/nested-block statement, а также unsupported source member exact
source-discard contract не получают и остаются `MORPH0031` как обычный
side-effect-only statement. Discard внутри вложенной lambda/local function
принадлежит обычному runtime/deferred C# и не является outer structured
instruction. В `ConstructUsing` и `ResolveUsing`
тот же текст является обычным C# read; `Convert` category-12 validation не
запускает.

### 6.68. `MORPH0032`: destination inputs structured callback-а read-only

`MORPH0032` публикуется на каждую явную попытку изменить normalized
destination input `previous` либо фактический `result` внутри structured
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
Прямая mutation во внешнем structured plan принадлежит `MORPH0032`, а чтение
либо mutation через deferred capture lambda/anonymous method/local function —
`MORPH0030`. Invalid становится только достижимый structured slice, зависящий
от записи;
generator не выполняет mutation как скрытый imperative update и не считает её
эквивалентом возвращённого member rule.

### 6.69. `MORPH0033`: compile-time marker выходит за DSL boundary

`MORPH0033` публикуется для каждого exact Morphant compile-time marker,
который пытается стать runtime value либо используется не в terminal
DSL-position:

- `Value`, `Auto`, `Ignore`, `Map`, `Create`, `Update` или `ByConvention`
  внутри `ConstructUsing`, `ResolveUsing` либо `Convert`;
- non-terminal structured use, например `Helper(Map(...))`, marker method
  group/delegate, alias, cast, comparison, pattern, return либо capture;
- сам parameter/object `MappingContextMarker` как alias, helper argument,
  cast, comparison/null check, pattern, return или capture. Прямое чтение
  `MappingContextMarker.Operation` и дальнейшая работа с полученным обычным
  `MappingOperation` разрешены.

Primary location — invoked marker name, marker method-group reference либо
конкретная reference parameter-а `MappingContextMarker`; generic arguments и
argument list invocation в location не входят. Diagnostic identity — marker
use origin, поэтому несколько invalid uses получают отдельные diagnostics.
`{1}` равно одной из шести callback families.

Terminal marker допускается через поддерживаемые transparent wrappers,
conditional/switch expression и structured local только когда lowering может
однозначно связать его с final constructor parameter/member/result position.
`context.Mapper.Map(...)` в context-aware runtime callback является обычным
runtime API и разрешён. Одноимённый foreign/user method, который не связывается
с exact Morphant marker symbol, игнорируется.

Generator не оставляет marker invocation/object в generated C#, не выполняет
phantom method как runtime stub и не допускает повторный overload binding после
lowering. Invalid marker в runtime callback делает callable атомарно invalid;
в structured plan recovery следует достижимому terminal site по разделу 6.70.

### 6.70. Recovery, precedence, порядок и suppression

Все пять diagnostics сохраняют полный C#-legal mapping contract и независимо
допустимые generated surfaces. Invalid execution path использует typed stub,
бросающий `MappingConfigurationException`; независимые paths, operations,
pairs и mapper-ы остаются исполнимыми.

Recovery максимально path-sensitive для structured plan, когда offending node
однозначно связан с branch/rule:

- invalid structured `Construct` затрагивает только reachable no-previous
  creation paths; existing Update reuse сохраняется;
- invalid structured `Resolve` затрагивает только его достижимые selection
  branches;
- invalid `Members` expression бросает только на path, где rule/dependency
  действительно требуется; невыбранные rules и остальные operations
  сохраняются;
- полностью overridden member/creation expression, discarded inherited slice
  и статически недостижимая branch не получают diagnostic и stub.

Если structured callback невозможно разобрать целиком (`MORPH0029`) либо
compiler preflight не может атрибутировать transfer failure более узкому node,
весь соответствующий callback fragment считается атомарно invalid. Это не
разрешает silently перейти к conventions.

Runtime callable является атомарным:

- invalid `ConstructUsing` затрагивает все no-previous paths, которые его
  вызывают, но не existing Update reuse;
- invalid `ResolveUsing` затрагивает все operations, вызывающие effective
  selector;
- invalid `Convert` затрагивает все разрешённые `MappingMode` operations pair,
  поскольку каждая выбранная overload полностью реализует manual mapping.

Pair-wide unmapped preflight failure делает обе operations pair throwing: если
generator не может установить безопасную меньшую область, он не испускает
частично проверенный transferred code.

Origin diagnostic не размножается по inherited consumers. Consumer получает
throwing recovery только если его effective plan сохранил invalid slice;
local override либо model precedence может полностью удалить зависимость.
Несколько diagnostics одного callback-а сходятся в один deterministic recovery
plan без попытки выбрать «первую» допустимую часть runtime callable.

Precedence действует так:

- compilation/mapper/pair gates категорий 1–3 и недостоверный builder flow
  категории 4 подавляют category-8 анализ в своей области;
- duplicate/mixed local slots категории 5, invalid effective settings
  категории 6 и invalid inheritance edge категории 7 разрешаются до анализа
  callback contents; discarded callback diagnostic не получает;
- `MORPH0028` владеет только accessibility, потерянной между mapper-level-ами;
  file-local/lifetime/binding/preflight failures уже доступного effective
  callback-а принадлежат `MORPH0030`;
- `MORPH0029` подавляет `MORPH0030`–`MORPH0033` того же structured origin;
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
location и reason/symbol/syntax/marker name. Additional locations
`MORPH0030` сохраняют source order. Generic construction, inheritance fan-out,
discovery order и incremental invalidation не меняют cardinality или порядок.

Suppression либо понижение severity не меняет callback classification,
effective plan, recovery и generated artifact set. Замена overload-а,
destination capability, callback form/body, captured symbol, local override,
`MappingMode`, constant branch, lexical warning/nullable state, extension
binding либо marker binding полностью actualizes diagnostics и affected stubs
при одном сохранённом incremental driver-е.

### 6.71. Самостоятельная тестовая матрица категории 8

Unit-категория callback diagnostics независимо фиксирует:

- exact descriptors `MORPH0029`–`MORPH0033`: ID, title, category, default
  severity, enabled/configurable flags, message formats и все parameters;
- классификацию всех согласованных overloads: две `Construct`, две `Resolve`,
  четыре `Members`, две `ConstructUsing`, две `ResolveUsing` и три `Convert`,
  без зависимости от тестов generated-surface категорий;
- `MORPH0029` для structured `Construct`, structured `Resolve` и каждой
  `Members` arity через natural method group, mapper delegate,
  Configure-local delegate и произвольное delegate-expression;
- отсутствие `MORPH0029` у inline expression/block lambda с parentheses,
  null-forgiving и explicit delegate cast, а также у runtime
  method-group/conditional/materialized callbacks;
- одну `MORPH0029` на callback origin при generic substitutions и inherited
  fan-out, exact callback-argument location и подавление content diagnostics
  недоступного body;
- `MORPH0030` отдельно для Configure-local value, `builder`/alias, external
  local delegate/function и транзитивной зависимости во всех шести callback
  families; capture cardinality, first-reference primary,
  declaration/remaining-reference additional locations и несколько origins;
- deferred capture `previous` / `result` и deferred
  `MappingContextMarker.Operation` через lambda, anonymous method и local
  function; разрешённые direct snapshots и обычный captured
  `MappingOperation`;
- file-local helper/type, extension method group, custom extension query и
  `foreach` pattern, неатрибутируемый fallback, exact reason forms/locations и
  pair-wide fail-closed preflight без C# errors в generated source;
- compiler preflight: новый warning/error получает `MORPH0030`, source-visible
  warning остаётся compiler-owned, а локально подавленная warning/nullable
  state переносится только на affected generated fragment;
- positive transfer mapper instance/static members, static API, compile-time
  constants, `nameof`, mapper delegate field/property и callback parameters;
- standard LINQ query syntax, collision-safe pattern/range/`out var`
  declarations, caller-info materialization, local function внутри deferred
  lambda и direct snapshot перед deferred capture;
- ambient unsafe pointer/function-pointer code, `async` / `static async`
  runtime callbacks для task-like opaque destination и coexistence unsafe/
  async mappings без mapper-wide unsafe context;
- value- и void-returning conditional extension calls с exact binding, null
  propagation и однократным вычислением receiver-а;
- полную positive structured grammar: expression/block lambdas, initialized
  locals, nested blocks, complete if/switch, multiple return/throw,
  conditional/switch/query expressions, object initializer, member `with`,
  patterns, допустимые terminal marker positions и direct compile-time source
  discard во всех трёх structured families;
- `MORPH0031` для каждого класса неподдерживаемой grammar: uninitialized/
  mutated/deconstructed locals, compound assignment/inc-dec, every loop,
  break/continue, side-effect statement, external local function, try/catch/
  finally, using/lock, label/goto, ref/using local, outer unsafe/fixed,
  await/yield;
- отсутствие `MORPH0031` и runtime getter evaluation для exact direct
  `_ = source.Member;`; несколько direct fields/properties и retained
  inherited origin, а также `MORPH0031` для control-flow/nested-block,
  chain/indexer/conditional access/conversion/invocation/tuple/alias/
  unsupported-member variants;
- outer-node cardinality и exact keyword/operator locations, отсутствие
  nested cascade внутри уже unsupported node и совместную публикацию
  независимых grammar breaks;
- отсутствие `MORPH0031` для arbitrary C# statements, `async` и local unsafe
  внутри `ConstructUsing`, `ResolveUsing` и каждой `Convert` overload;
- `MORPH0032` для simple/compound/deconstruction assignment, inc-dec,
  ref/out и member/indexer writes через `previous`, `result`, initialized
  alias, pattern alias и извлечённое previous value;
- per-site `MORPH0032`, root-input message parameter, exact operator/ref-out
  location и precedence над `MORPH0031`;
- positive object/member plan initializer, `with` copy, independent same-type
  local и method-call boundary без попытки purity inference;
- `MORPH0033` для `Value` / `Auto` / `Ignore` / `Map` / `Create` / `Update` /
  `ByConvention` во всех runtime families, nested runtime local function,
  non-terminal structured `Helper(Map(...))`, marker method group/alias/cast/
  comparison/return/capture и нескольких uses одного callback-а;
- `MappingContextMarker` как alias/helper argument/cast/comparison/null check/
  pattern/return/capture; разрешённое прямое `Operation`, его local snapshot,
  `context.Mapper.Map`, foreign/user methods и terminal recursive lowering;
- overload-invariant applicability: обе `Construct` forms только на
  no-previous paths, обе `Resolve` forms на полном result-selection surface,
  четыре `Members` forms с dependency-driven lifecycle, обе `Using` families
  согласно result-policy lifecycle и три `Convert` forms во всех enabled
  operations;
- reachability через `MappingMode`, null handling, constant conditions,
  Create/Update specialization, member override, local model precedence и
  exact/cross-pair inheritance;
- path-sensitive structured recovery отдельно для construction branches,
  member rules/dependencies, previous reuse, replacement, terminal null и
  statically unreachable expressions;
- atomic runtime recovery отдельно для no-previous `ConstructUsing`, full
  `ResolveUsing`, all-enabled-operations `Convert` и неатрибутируемого
  pair-wide preflight;
- полный generated result при каждой diagnostic: legal mapper contracts и
  surfaces, typed throwing stubs, сохранённые independent operations/pairs/
  mapper-ы и отсутствие downstream category-9–12 cascade;
- precedence и совместную cardinality с `MORPH0001`–`MORPH0028`, C# compiler
  diagnostics, deterministic publication order и origin-only inherited
  diagnostics;
- реальное suppression/изменение severity без изменения callback plan,
  recovery либо artifacts;
- actualization callback overload/class/body, destination capability,
  captured symbol/declaration, warning/nullable/unsafe context, extension/query
  binding, mapper member conversion, marker binding, source-discard shape и
  semantic binding, reachability, override и inheritance при одном сохранённом
  incremental driver-е.

Package-like integration-категория независимо проверяет:

- suppressed `MORPH0029` для каждой structured family: affected path бросает,
  existing Update либо независимая branch исполняется, independent pair
  сохраняется;
- suppressed `MORPH0030` для structured, `ConstructUsing`, `ResolveUsing`, всех
  `Convert` forms и materialized Configure-local delegate с соответствующим
  path-sensitive, atomic либо pair-wide recovery;
- реальные positive transfer scenarios для query/collisions/caller info,
  unsafe, async, lexical suppressions и conditional extension lowering; custom
  method-group/query/`foreach` binding fail closed до emission;
- suppressed representative `MORPH0031` / `MORPH0032`: invalid member/
  construction path бросает без выполнения side effect, valid branches и
  operations реально возвращают результат;
- exact source discard во всех structured families реально не вызывает getter
  и не меняет generated values; тот же statement в `ConstructUsing` /
  `ResolveUsing` выполняет обычный getter read;
- suppressed `MORPH0033` для каждой runtime family и representative
  non-terminal structured/context-marker use, отсутствие исполнения marker
  body и сохранение независимой reuse/branch;
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

### 6.72. Категория 9: общий contract

Категория «Корректность construction plan» содержит ровно пять diagnostics:

| ID | Title | Message format |
|---|---|---|
| `MORPH0035` | `Destination construction is not configured` | `Destination construction for contract '{0}' is not configured for reachable paths: {1}.` |
| `MORPH0036` | `Convention construction is unavailable` | `Convention construction for contract '{0}' is unavailable with ConstructorSelection.{1}: {2}.` |
| `MORPH0037` | `Constructor parameter rule is invalid` | `Constructor parameter rule for '{0}' in contract '{1}' is invalid: {2}.` |
| `MORPH0038` | `Previous destination is unavailable` | `Previous destination is unavailable for contract '{0}' on reachable paths: {1}.` |
| `MORPH0039` | `Structured construction plan is null` | `Structured construction plan for contract '{0}' cannot be null on reachable paths: {1}.` |

Для всех пяти diagnostics действует общий descriptor contract:

- category — `Morphant.Construction`;
- default severity — `Error`;
- diagnostic включена по умолчанию и не имеет `NotConfigurable`;
- description и help link отсутствуют, custom tags пусты;
- анализируется только оставшийся effective declarative construction plan
  после категорий 1–8;
- `MappingMode`, null handling, специализация `Option.None` /
  `Option.Some`, structured conditions и model precedence определяют
  достижимые paths; хотя бы один достижимый invalid path сохраняет diagnostic;
- manual `Convert` категорией 9 не анализируется, а runtime
  `ConstructUsing` / `ResolveUsing` являются атомарными producers настоящего
  destination и не превращаются в constructor plan;
- `UnmappedMemberValidation` не скрывает construction error и не превращает
  его в warning;
- suppression либо изменение severity меняет только compiler presentation и
  не включает fallback, не делает invalid branch исполнимой и не меняет
  generated recovery.

Mapping contract передаётся в `{0}` у `MORPH0035`, `MORPH0036`, `MORPH0038`
и `MORPH0039`, а у `MORPH0037` — в `{1}`; он форматируется по canonical
identity категории 3. `ConstructorSelection` в `MORPH0036` — resolved
non-`Default` public enum member. Имя parameter-а в `MORPH0037` сохраняет
точное destination spelling без case normalization. Constructor display в
reason использует
fully-qualified nullable-aware containing type и ordered parameter
types/names; aliases и syntactic nullable differences canonical identity не
меняют.

Affected paths имеют три стабильных пользовательских имени:

1. `Create`;
2. `Update without a previous destination`;
3. `Update with a previous destination`.

Если один origin затрагивает несколько path-ов, message перечисляет их в этом
порядке через `, `. Третий path нужен для replacement/null leaves structured
`Resolve`; `MORPH0035` и `MORPH0038` по своей природе используют только первые
два.

### 6.73. Construction origins, reachability и selection model

Category-9 observation всегда принадлежит конкретному effective construction
origin:

- implicit convention construction authoritative pair без result policy;
- каждый terminal `ByConvention` leaf structured `Construct` / `Resolve`;
- каждый terminal explicit constructor leaf и его parameter rules;
- каждый terminal previous leaf structured `Resolve`;
- каждый terminal `null` / `default` leaf structured `Construct` /
  `Resolve`.

Одна callback lambda не является атомарной construction operation. После
category-8 lowering generator строит path-sensitive control-flow plan,
специализирует его отдельно для known no-previous / existing-previous state и
анализирует только leaves, оставшиеся достижимыми. Constant/`HasValue`
branches, short-circuit, statement `if` / `switch`, conditional/switch
expressions, local aliases и `throw` сохраняют принятую semantics. Полностью
overridden result policy, discarded inherited slice и недостижимый leaf
category-9 diagnostic не получают.

Reachable no-previous path существует:

- для enabled `Create` после non-terminal `NullSourceHandling`;
- для enabled `Update` только когда destination может стать `Option.None` и
  effective `NullDestinationHandling.Create` продолжает mapping;
- не существует для `Update` path-а, завершённого
  `NullDestinationHandling.Throw`, и для non-nullable value destination,
  который всегда образует `Option.Some`.

Enabled `Update` с non-null destination независимо образует existing-previous
path. Structured `Resolve` может выбрать на нём previous либо construction
replacement; поэтому constructor и null-plan diagnostics не ограничиваются
no-previous state.

Фактический runtime argument не участвует в статическом решении: reference либо
nullable-value `Update` с `Create` policy имеет no-previous path, даже если
обычно вызывается с non-null destination. `NullSourceHandling`, возвращающий
result либо бросающий на null source, не удаляет path для non-null source.

Если result policy отсутствует, pair со structured construction capability
использует implicit convention origin; pair без capability получает
`MORPH0035`. Structured `Construct` заменяет только no-previous origin,
structured `Resolve` задаёт leaves всех своих reachable paths. Runtime
`ConstructUsing` и `ResolveUsing` закрывают соответствующие paths настоящим
destination result. Explicit constructor leaf не использует
`ConstructorSelection`; только implicit convention и `ByConvention` выполняют
selection.

Supported constructor set совпадает с capability model: только доступные из
общего generated assembly-context instance constructors, которыми реально
можно создать non-opaque, non-abstract destination. Стратегии применяются
точно так:

- `Unambiguous` выбирает единственный parameterized constructor, а при
  отсутствии parameterized — parameterless; несколько parameterized
  constructors дают ambiguity независимо от parameterless;
- `Explicit` запрещает implicit convention и `ByConvention`;
- `Parameterless` требует supported parameterless constructor;
- `Single` требует ровно один supported constructor;
- `Greediest` строит warning-free applicable plans, сравнивает число реально
  emitted arguments и требует unique maximum;
- `Largest` сначала требует unique maximum declared parameter count и только
  затем проверяет применимость выбранного constructor-а.

`Default` до category 9 уже разрешён к effective strategy, library default —
`Unambiguous`. Optional/`params` omission, written `ByConvention` rules,
automatic warning-free conversions и actual invocation binding входят в
applicability. Shape strategies не откатываются к другому constructor-у после
выбора. `Greediest` не считает inapplicable candidate. Required/init member
readiness участвует в applicability computation, но точные member-plan blockers
принадлежат категории 10 по разделам 6.84–6.85.

### 6.74. `MORPH0035`: destination construction не настроена

`MORPH0035` публикуется, когда authoritative declarative pair не имеет
structured construction capability, effective `ConstructUsing` /
`ResolveUsing` отсутствует и остаётся хотя бы один reachable no-previous path.
Сюда входят opaque/scalar destination, interface, abstract либо factory-only
class и другая eligible pair без callable constructor surface.

Pair со structured capability и недоступным convention constructor получает
`MORPH0036`, а не `MORPH0035`. Configured result policy, уже invalid по
категориям 5, 7 либо 8, также не считается «отсутствующей»: её собственная
diagnostic и recovery являются первичной причиной. `Convert` не является
construction fallback; manual model целиком исключает category-9 analysis.

Primary location — identifier `Map` authoritative registration. Additional
locations отсутствуют. Diagnostic identity — mapper и canonical pair; affected
path names агрегируются в одно сообщение. Generic substitutions, both paths и
derived consumers не размножают diagnostic origin.

Recovery заменяет только отсутствующий no-previous result typed
`MappingConfigurationException` stub-ом. `Create` целиком бросает, если это его
единственный reachable result path. `Update without a previous destination`
бросает только после соответствующего null handling; existing-destination
reuse и применимый `Members` plan сохраняются. Generator не выбирает
`default`, runtime conversion, `Convert` либо service lookup.

### 6.75. `MORPH0036`: convention construction недоступна

`MORPH0036` публикуется на каждый reachable implicit-convention либо
`ByConvention` origin, для которого valid effective
`ConstructorSelection` не даёт единственный применимый constructor plan.
Стабильный `{2}` reason имеет одну из форм:

- `automatic constructor selection is disabled`;
- `no supported parameterless constructor is available`;
- `exactly one supported constructor is required, but {count} were found`;
- `more than one supported parameterized constructor is available`;
- `no supported constructor has an applicable convention plan`;
- `multiple applicable constructors have the greatest mapped argument count`;
- `multiple supported constructors have the largest declared parameter count`;
- `selected constructor '{constructor}' has no warning-free convention value for required parameter '{parameter}'`;
- `selected constructor '{constructor}' cannot be invoked without changing C# binding`.

Reason выбирается по реальному этапу алгоритма: strategy gate, shape selection,
applicability первого blocking required parameter в declaration order, затем
invocation probe. Он не сообщает внутренний planner kind и не перечисляет
отброшенные candidates. Один construction origin получает одну
`MORPH0036`; исправление причины полностью actualizes selection и может
открыть следующую независимую observation.

Primary location explicit origin-а — identifier terminal `ByConvention`
marker-а. Для implicit convention primary — identifier `Map` authoritative
registration. Если effective `ConstructorSelection` имеет distinct
source-backed C# origin, его final argument expression является первой
additional location. Для уже выбранного, но неприменимого constructor-а его
source declaration является следующей additional location, если доступна.
MSBuild/library default и metadata constructor additional span не создают.

Diagnostic identity включает mapper, canonical pair, construction origin и
reason kind. Один origin, достигнутый обоими operations, generic substitutions
либо inherited consumers, публикуется один раз с агрегированными paths.
Отдельные `ByConvention` leaves одной lambda диагностируются независимо.

Automatic required parameter без source candidate либо warning-free implicit
conversion принадлежит `MORPH0036`. Явная parameter rule принадлежит более
точной `MORPH0037` и подавляет общий selection failure, когда полностью
объясняет недоступность origin-а. Если unique constructor уже выбран, но
единственный blocker — required/init member plan, `MORPH0036` не публикуется:
категория 10 указывает точный member и phase. При shape ambiguity constructor
ещё не выбран, поэтому downstream parameter/member diagnostics подавляются.

Recovery заменяет только affected convention leaf typed
`MappingConfigurationException` stub-ом. Другой constructor не выбирается,
`ByConvention` не превращается в explicit/default construction, а
independent explicit constructor, previous и runtime result leaves
сохраняются.

### 6.76. `MORPH0037`: constructor parameter rule invalid

`MORPH0037` публикуется для explicit parameter rule terminal explicit
constructor либо `ByConvention` leaf-а, когда C# binding callback surface
успешен, но правило неприменимо к фактически выбранному constructor plan.
Причины:

- `Auto` не разрешает unique readable source member с warning-free implicit
  conversion;
- `Ignore` пытается опустить required non-optional, non-`params` parameter;
- written `ByConvention` rule именует parameter, которого нет у selected
  constructor-а;
- typed `Auto` / `Ignore` / `Value<T>` marker успешно прошёл C# binding,
  например через более широкий `object` либо nullability conversion, но
  утверждает target type, не совпадающий точно с конечным parameter type;
- rule проходит generated wrapper surface, но actual selected constructor
  нельзя вызвать с ним без изменения compiler binding.

Для exact explicit constructor overload selected constructor известен из
исходного compiler binding. Для shape strategies `ByConvention` rule
проверяется после unique selection. Для `Greediest` precise rule diagnostic
публикуется только когда один и тот же rule является доказанным blocker-ом
каждого оставшегося candidate; mixed candidate rejection, отсутствие
applicable plan либо tie остаются одной `MORPH0036`.

`{2}` использует соответственно одну из стабильных reason-форм:

- `Auto does not resolve a unique readable source member with a warning-free implicit conversion`;
- `Ignore can only omit an optional or params parameter`;
- `selected constructor '{constructor}' does not declare this parameter`;
- `marker target type '{actualType}' does not exactly match parameter type '{parameterType}'`;
- `the rule cannot be applied to selected constructor '{constructor}' without changing C# binding`.

Primary location — marker name `Auto` / `Ignore` / `Value` для marker reason,
parameter designator `DestinationConstructorParameters` initializer-а для
missing parameter либо наименьшее rule value expression для binding fallback.
Source declaration selected constructor-а является единственной additional
location, если доступна. `{0}` — точное parameter name selected/declared
rule-а.

Diagnostic identity — mapper, canonical pair, construction origin, rule origin
и reason kind. Независимые invalid rules дают независимые diagnostics; один
rule не размножается по paths или inherited consumers. Automatic unwritten
parameter failure остаётся `MORPH0036`. Invalid nested `Map` / `Create` /
`Update` marker принадлежит категории 11; unsupported terminal-marker use —
категории 8. Обычная C# type/binding error explicit expression остаётся
compiler-owned. Если несовпадение typed marker-а уже отвергнуто compiler-ом,
`MORPH0037` не публикуется.

Invalid становится только construction leaf, зависящий от rule. Его expression
не вычисляется; leaf бросает `MappingConfigurationException`. Другие leaves и
existing-destination paths сохраняются, hidden `Auto`/omission/fallback не
подставляется.

### 6.77. `MORPH0038`: previous destination недоступен

`MORPH0038` публикуется, когда terminal previous leaf structured `Resolve`
остаётся достижимым после специализации с `Option.None`. Это implicit
`Option<TDestination> -> DestinationConstruction` result selection, а не любое
чтение `previous`.

Защищённый `previous.HasValue` branch, удалённый для no-previous state, не
диагностируется. Existing-destination `Update` может выбрать previous.
`ResolveUsing` является обычным runtime C# и в эту diagnostic не входит.
Обычное чтение `previous.Value` внутри пользовательского expression также не
переопределяется этой diagnostic: его observable runtime behavior остаётся
частью написанного C#, если более ранняя категория не признала expression
непереносимым.

Primary location — terminal expression, нормализованное к previous result.
Для local alias primary указывает terminal alias use, а declaration/reference,
связавшая alias с `previous`, является additional location. Diagnostic identity
— callback и terminal previous origin; affected no-previous paths агрегируются
в порядке раздела 6.72 и не создают fan-out по operations либо consumers.

Recovery сохраняет control-flow conditions и side effects до invalid leaf, а
сам leaf бросает `MappingConfigurationException`. Morphant не создаёт новый
destination, не возвращает `default` и не выбирает соседнюю `Resolve` branch.

### 6.78. `MORPH0039`: structured construction plan равен null

`MORPH0039` публикуется, когда reachable terminal leaf structured
`Construct` / `Resolve` статически является `null`, target-typed `default`,
`default(DestinationConstruction)` либо тем же значением через поддерживаемые
transparent wrappers и single-value structured local alias.

Primary location — наименьшее `null` / `default`-producing expression. Terminal
alias uses того же producer-а являются additional locations в source order.
Diagnostic identity — callback и null-producing origin; повторное достижение
producer-а из нескольких paths дедуплицируется, независимые null/default
producers диагностируются отдельно.

`null!`, nullable-disabled context, explicit cast и suppression C# nullable
warning не делают plan допустимым. Compiler warning остаётся независимой
compiler diagnostic; `MORPH0039` сообщает Morphant-specific отсутствие DSL
plan. Явный `throw` является terminal control flow, а не null plan.

Настоящий `null`, возвращённый `ConstructUsing` / `ResolveUsing`, является
авторитетным destination result и завершает member stage без diagnostic.
`null` из `Convert` также полностью принадлежит manual callback. `null` вместо
`DestinationMembers` относится к категории 10, а не дублируется здесь.

Recovery заменяет только null-plan leaf typed
`MappingConfigurationException` stub-ом. Никакого convention, previous,
runtime callback либо default-destination fallback не выполняется.

### 6.79. Recovery, precedence, порядок и suppression

Все пять diagnostics сохраняют C#-legal `ITypeMapper<,>` contract,
capability-specific fluent surfaces и независимые operations/pairs. Invalid
leaf/path бросает `MappingConfigurationException`; independently valid leaves
исполняются с исходным evaluation order.

Recovery granularity:

- `MORPH0035` блокирует только reachable no-previous paths отсутствующей
  policy;
- `MORPH0036` блокирует конкретный implicit/`ByConvention` origin;
- `MORPH0037` блокирует leaf, использующий invalid explicit parameter rule;
- `MORPH0038` блокирует только reachable no-previous previous leaf;
- `MORPH0039` блокирует только reachable null/default plan leaf.

Если control-flow attribution невозможна после valid category-8 lowering,
весь structured result callback считается атомарно invalid и primary ownership
возвращается к `MORPH0030`; category 9 не строит эвристический pair-wide
fallback. Runtime result callback category 9 никогда не разбирает внутри.

Precedence действует так:

- gates категорий 1–4, invalid local composition/settings/inheritance
  категорий 5–7 и invalid callback transfer/grammar категории 8 подавляют
  category-9 analysis соответствующей области;
- invalid effective `MappingMode` / null handling не позволяют доказать paths;
  валидные settings сначала определяют reachability;
- `MORPH0035` и `MORPH0036` взаимоисключаются для одного implicit origin:
  первая означает отсутствие structured capability, вторая — неудачу
  доступной convention model;
- exact `MORPH0037` подавляет производную `MORPH0036` того же
  `ByConvention` origin-а; unrelated selection/rule failures публикуются
  совместно;
- `MORPH0038` и `MORPH0039` являются самостоятельными terminal leaves и могут
  публиковаться рядом с construction diagnostic другой reachable branch;
- selected constructor member blockers, включая unmapped required/init phase,
  принадлежат категории 10 и подавляют общий `MORPH0036`, когда полностью
  объясняют неприменимость; при selection ambiguity member plan ещё
  недостоверен и category-10 cascade подавляется;
- nested result correctness категории 11 анализируется только после valid
  constructor/rule leaf; category-12 warning-анализ исключает invalid affected
  path, но сохраняется для независимого plan;
- точная source C# binding/type error не дублируется Morphant diagnostic-ой.

Origin diagnostic не размножается по exact/cross-pair inheritance consumers.
Retained invalid origin переносит recovery; local override/model precedence,
удалившие origin, удаляют и зависимость. Publication order — по ID. Внутри ID:
ordinal mapper identity, canonical pair, construction callback/origin, primary
source location, reason/parameter и affected paths. Additional locations
сохраняют описанный source order.

Suppression либо severity override не меняет selection, reachability, recovery
или artifact set. Изменение destination capability/constructors, result policy,
`ConstructorSelection`, parameter rule/marker type, `Members` blocker,
control-flow branch, `MappingMode`, null handling, override либо inheritance
полностью actualizes category-9 observations и affected stubs при одном
сохранённом incremental driver-е.

### 6.80. Самостоятельная тестовая матрица категории 9

Unit-категория construction diagnostics независимо фиксирует:

- exact descriptors `MORPH0035`–`MORPH0039`: ID, title, category, default
  severity, enabled/configurable flags, message formats и все parameters;
- canonical contract/constructor/type/parameter/path formatting,
  deterministic ordering и exact primary/additional locations;
- `MORPH0035` для opaque/scalar, interface, abstract и factory-only
  destinations при enabled `Create` и nullable `Update` no-previous paths;
- отсутствие `MORPH0035` для runtime `ConstructUsing` / `ResolveUsing`,
  manual `Convert`, structured capability и Update-only existing non-nullable
  value destination;
- `MORPH0035` reachability matrix для every `MappingMode`,
  `NullDestinationHandling.Create` / `Throw`, reference/non-nullable/nullable
  value destination и source null policies;
- все `ConstructorSelection` strategies для implicit convention и terminal
  `ByConvention`: default `Unambiguous`, `Explicit`, parameterless/single
  shape, parameterized ambiguity, greediest no-plan/tie и largest tie/
  selected-inapplicable;
- optional/`params` argument scoring, warning-free conversion, invocation
  binding, no-fallback after shape selection и exact stable `MORPH0036`
  reasons;
- one `MORPH0036` per construction origin across all reachable Create/Update
  paths, generic substitutions and inherited consumers; distinct diagnostics
  for independent `ByConvention` leaves and setting/constructor additional
  locations;
- `MORPH0037` for explicit-constructor and `ByConvention` `Auto` without a
  unique compatible source, required `Ignore`, rule absent from selected
  constructor, прошедшее C# binding typed `Auto` / `Ignore` / `Value<T>`
  mismatch and final binding incompatibility;
- greediest uniform-rule attribution to `MORPH0037` versus mixed/no-plan
  `MORPH0036`, exact per-rule cardinality/locations and absence for successful
  explicit rules;
- compiler-owned explicit expression errors, category-8 marker/transfer
  failures and category-11 nested markers without duplicate `MORPH0037`;
- `MORPH0038` for direct, conditional, switch, block-return and local-alias
  previous leaves on `Create` / nullable `Update` no-previous paths;
- guarded `previous.HasValue`, existing-only Update, disabled operations,
  runtime `ResolveUsing` and ordinary `previous.Value` expressions without
  `MORPH0038`;
- `MORPH0039` for `null`, `null!`, target-typed/typed `default`, transparent
  casts/wrappers, conditional/switch leaves, local aliases и all three
  Create/Update path forms; producer deduplication and exact use additional
  locations;
- valid terminal `throw`, runtime result-policy null, manual null and
  category-10 `DestinationMembers` null without `MORPH0039`;
- category-10 ownership for required/init blockers and suppression of
  derivative `MORPH0036`; selection ambiguity suppresses unavailable
  downstream member analysis;
- path-sensitive full generated result for every ID: legal interfaces and
  fluent surfaces, typed throwing leaf, preserved existing Update,
  independent branches/operations/pairs/mapper-ы, no hidden fallback and no
  downstream cascade;
- local/exact/cross-pair inheritance origin laws, retained versus discarded
  invalid slices and independent local overrides;
- real suppression/severity override for all five IDs without changing
  recovery/artifacts;
- actualization destination constructors/capability, strategy origin,
  parameter rule/marker type, callback control flow, settings, member blocker,
  override и inheritance при одном incremental driver-е.

Package-like integration-категория независимо проверяет:

- suppressed `MORPH0035` for Create and null-Update: invalid no-previous path
  throws, existing Update instance and its members remain executable;
- suppressed `MORPH0036` for each strategy family and both implicit/
  `ByConvention` origins on Create, null-Update and existing-Update replacement
  paths, without fallback to another constructor;
- suppressed `MORPH0037` representative explicit and convention rules:
  invalid value is not evaluated, valid sibling branch constructs the exact
  destination;
- suppressed `MORPH0038` in `Resolve`: no-previous branch throws while guarded
  previous reuse and independent replacement execute normally;
- suppressed `MORPH0039` for null/default leaves on Create, null-Update and
  existing-Update replacement paths while non-null constructor, previous and
  explicit throw branches preserve control flow/side effects;
- legal runtime `ConstructUsing` / `ResolveUsing` null remains terminal and
  skips `Members`, proving it is not `MORPH0039`;
- Update-only factory/interface mapping with non-null previous remains
  executable without configured creation, while enabling a no-previous path
  deterministically adds `MORPH0035` recovery;
- exact/inherited origin recovery, independent pair isolation and no category
  10–12 cascade from invalid construction leaf;
- real `.editorconfig` / MSBuild suppression or severity override for all five
  IDs without changing generated artifact set and effective recovery.

### 6.81. Категория 10: общий contract

Категория «Корректность member plan» содержит ровно четыре diagnostics:

| ID | Title | Message format |
|---|---|---|
| `MORPH0040` | `Member rule is invalid` | `Member rule for '{0}' in contract '{1}' is invalid: {2}.` |
| `MORPH0041` | `Required destination member is not initialized` | `Required destination member '{0}' in contract '{1}' is not initialized on reachable paths: {2}.` |
| `MORPH0042` | `Member rule cannot be applied` | `Member rule for '{0}' in contract '{1}' cannot be applied: {2}. Reachable paths: {3}.` |
| `MORPH0043` | `Structured member plan is null` | `Structured member plan for contract '{0}' cannot be null on reachable paths: {1}.` |

Для всех четырёх diagnostics действует общий descriptor contract:

- category — `Morphant.Members`;
- default severity — `Error`;
- diagnostic включена по умолчанию и не имеет `NotConfigurable`;
- description и help link отсутствуют, custom tags пусты;
- анализируется только оставшийся effective declarative member plan после
  успешного выбора result категориями 1–9;
- `MappingMode`, null handling, result-policy specialization, structured
  control flow, member-plan overlays и model precedence определяют
  достижимые paths и effective rules до публикации diagnostics;
- manual `Convert` не имеет declarative member stage и категорией 10 не
  анализируется;
- runtime `ConstructUsing` / `ResolveUsing` остаются непрозрачными producers
  настоящего destination: категория 10 не проверяет, как callback создаёт
  объект, но учитывает, что non-null runtime result уже создан до `Members`;
- `UnmappedMemberValidation` не скрывает category-10 error и не понижает её до
  warning;
- suppression либо изменение severity меняет только compiler presentation и
  не делает invalid rule применимой, не инициализирует `required`, не
  превращает `null` в пустой plan и не меняет generated recovery.

Mapping contract передаётся в `{1}` у `MORPH0040`–`MORPH0042` и в `{0}` у
`MORPH0043`; он форматируется по canonical identity категории 3. Имя member-а
в `{0}` у первых трёх diagnostics сохраняет точное destination spelling без
case normalization. Member/type displays внутри reason используют
fully-qualified nullable-aware containing type, member name и конечный
read/write type. Generic substitutions и oblivious/nullability context
сохраняются по законам generated member surface.

Affected paths используют те же три стабильных пользовательских имени, что и
категория 9:

1. `Create`;
2. `Update without a previous destination`;
3. `Update with a previous destination`.

`MORPH0041`–`MORPH0043` перечисляют affected paths в этом порядке через `, `.
Intrinsic `MORPH0040` сохраняет тот же path set для recovery/deduplication, но
не включает его в message и не размножается по operations. Внутри одного
public path независимые member-plan leaves остаются разными origins и не
сливаются только из-за одинакового имени path-а.

### 6.82. Member origins, effective rules и lifecycle model

Category-10 observation принадлежит одному из четырёх source-backed либо
destination-backed origins:

- effective explicit member rule после локального/inherited merge и `with`-
  overlays;
- obligation конкретного `required` destination member-а на конкретном
  structured creation leaf;
- lifecycle boundary между effective member rule и фактическим result origin;
- terminal `null` / `default` leaf structured `Members` callback-а.

Весь `Members` callback не считается одной атомарной member operation. После
category-8 lowering generator строит общий с construction plan path-sensitive
dependency graph, специализирует `Create`, Update без previous и Update с
previous, разрешает conditional/switch branches и удаляет overridden rules с
их неиспользуемыми dependencies. Diagnostic получает только rule либо plan leaf,
который остался effective и достижим. Полностью перекрытый local/inherited
rule, discarded `with` value и недостижимая branch category-10 diagnostic не
получают.

Destination member identity определяется generated surface до анализа rules:

- class/record/struct members следуют base-first declaration order, но новое
  declaration derived destination скрывает одноимённый base slot, даже если
  само declaration непригодно;
- override сохраняет одну member identity и не считается hiding;
- interface member должен иметь единственное most-derived declaration;
- local rule current pair всегда относится к member-у, выбранному generated
  `DestinationMembers` surface C# binding-ом;
- cross-pair imported rule сохраняет identity исходного destination slot-а и
  не перенаправляется по одному имени на новый derived slot;
- local rule того же имени перекрывает imported rule до проверки его
  применимости и тем самым может удалить конфликтующий inherited origin.

Для каждого effective writable/creation-time member применяется следующая
последовательность:

1. Обычная explicit expression должна успешно связаться с generated
   `Member<T>` surface; точную C# type/binding error Morphant не дублирует.
2. Explicit `Auto` обязан разрешить unique readable source member с точным
   case-sensitive именем и warning-free implicit conversion.
3. Typed `Auto<T>` / `Ignore<T>` и `Value<T>` после успешного C# binding
   обязаны утверждать exact final member type. Более широкий `object`, boxing
   либо nullability conversion wrapper-а не ослабляет exact-target law.
4. `Ignore` является допустимым no-op: он сохраняет уже выбранное значение и
   сам по себе не требует assignment. Его влияние на `required` structured
   creation проверяется отдельно `MORPH0041`.
5. Nested `Map` / `Create` / `Update` сначала считается member rule, но
   корректность nested operation/result принадлежит категории 11.

Неуказанный member при `MemberSelection.Auto` получает convention rule только
при unique readable warning-free source candidate. Отсутствующий automatic
candidate сам по себе не является category-10 error: для обычного member-а это
возможный warning категории 12, а для `required` — точная `MORPH0041`.
`MemberSelection.Explicit` просто не создаёт unwritten rules.

Member phase различает три result origin-а:

- structured constructor/convention result ещё допускает object-initializer
  assignments для result-independent `init` и creation-time `required` rules;
- previous result уже создан: обычные setters/mutable fields применяются, а
  `init` rules сохраняют существующее значение, не вычисляя свои expressions;
- non-null result runtime `ConstructUsing` / `ResolveUsing` также уже создан:
  обычные post-construction rules применяются, но явный value-producing
  `init` rule неприменим. Runtime `null` завершает mapping до member stage.

Прямая либо транзитивная dependency value или условия rule-а от `result`
делает rule post-creation. Для обычного setter/mutable field это допустимо.
Для `init` и любого `required` rule-а, который обязан удовлетворить
structured creation, такая dependency возникает до появления result и
принадлежит `MORPH0042`.

### 6.83. `MORPH0040`: member rule invalid

`MORPH0040` публикуется, когда C#-связанный effective explicit rule не может
обозначать заявленный destination member по законам Morphant. Стабильный `{2}`
reason имеет одну из форм:

- `Auto does not resolve a unique readable source member with a warning-free implicit conversion`;
- `marker target type '{actualType}' does not exactly match member type '{memberType}'`;
- `imported rule targets destination member '{targetMember}', which is hidden by '{hidingMember}' in the current destination`.

Первая форма относится только к написанному `Auto()` / `Auto<T>()`. Она
охватывает отсутствие читаемого exact-name source member-а, несколько
равноправных candidates, недоступный candidate и отсутствие warning-free
implicit conversion. Unwritten convention с тем же результатом `MORPH0040` не
получает.

Вторая форма относится к typed `Auto` / `Ignore` / `Value<T>`, которое уже
прошло C# binding generated wrapper-а, например через `object` либо
nullability conversion. Если compiler сам отверг вызов/initializer,
`MORPH0040` не публикуется. Обычное explicit expression, включая его nullable
warning, остаётся compiler-owned и не получает exact-target restriction
marker-а.

Третья форма относится только к retained cross-pair imported rule. Morphant
не перенаправляет его на одноимённый member нового derived destination и не
применяет скрытый base slot через cast. Exact same-pair import сохраняет тот же
destination contract и этой причиной не затрагивается. Явный local rule того
же имени удаляет imported origin до category-10 анализа и является
единственным неявно не меняющим slot способом разрешить конфликт.

Primary location для `Auto`/typed-marker reasons — identifier `Auto`, `Ignore`
либо `Value`. Для hidden imported rule primary — identifier `IncludeBase`
consumer edge. Member designator исходного rule-а и declaration hiding member-а
становятся additional locations в этом порядке, если имеют source spans.

Diagnostic identity включает current mapper, canonical pair, effective member
identity, rule/edge origin и reason kind. Один rule, достигнутый несколькими
operations либо generic substitutions, публикуется один раз с агрегированными
affected paths для recovery. Consumer-specific hiding конфликт диагностируется
отдельно для каждой current pair; сам исходный valid base rule diagnostic не
получает.

Recovery делает invalid только те member-plan leaves, которым нужен rule. При
выборе такого leaf mapping бросает `MappingConfigurationException` до
вычисления value и до assignments member stage. Независимая branch без rule,
previous/creation path, где rule не применяется, другой member plan и pair
сохраняются. Morphant не заменяет explicit `Auto` convention-ом, не вставляет
cast и не выбирает скрытый base member.

### 6.84. `MORPH0041`: required destination member не инициализирован

`MORPH0041` публикуется для каждого `required` property/field, которое не
удовлетворено на хотя бы одном reachable structured constructor/convention
creation leaf. Obligation отсутствует, если selected destination constructor
помечен `[SetsRequiredMembers]`. Constructor argument с тем же именем,
destination field/property initializer и значение по умолчанию сами по себе
не заменяют required object-initializer obligation без этого атрибута.

Member считается удовлетворённым, когда на leaf существует effective valid
result-independent rule, который Morphant может выполнить в creation-time
initializer: explicit expression/`Value`, valid `Auto`, valid nested rule либо
применимая unwritten convention. `MORPH0041` возникает, в частности, когда:

- `MemberSelection.Explicit` оставляет required member неуказанным;
- `MemberSelection.Auto` не находит unique warning-free source candidate;
- explicit `Ignore` сохраняет значение вместо required initializer-а;
- выбранная conditional/switch member-plan branch не содержит required rule;
- required member отсутствует в writable generated surface из-за
  accessibility, unsupported/reserved shape либо hiding.

Previous-result и result runtime `ConstructUsing` / `ResolveUsing` уже были
созданы до Morphant member stage, поэтому category 10 не проверяет, как их
`required` members были удовлетворены. C# проверяет написанный пользователем
`new` внутри runtime callback-а, а другой factory/cache остаётся обычным
runtime C#. Existing previous branch также не получает `MORPH0041`.

Если exact rule уже invalid по `MORPH0040`, неприменим по `MORPH0042` либо его
nested marker invalid по категории 11, производная `MORPH0041` того же
member/path подавляется. Explicit `Ignore` при этом является valid rule и
потому получает именно `MORPH0041`, а не `MORPH0040`. Один и тот же required
member может независимо иметь valid previous/runtime paths и invalid
structured replacement path.

Primary location выбирается по наиболее конкретной причине:

- marker name `Ignore` для effective explicit ignore;
- terminal `DestinationMembers` plan expression для explicit plan leaf,
  который не задаёт member;
- declaration required destination member-а для implicit/no-`Members` plan;
- identifier `Map` authoritative registration, если destination member
  доступен только из metadata.

Source-backed final argument effective `MemberSelection` и declaration
selected constructor-а добавляются после primary в этом порядке, когда они
непосредственно определяют obligation. Additional locations отсутствуют, если
соответствующий origin задан MSBuild/library default либо metadata.

Diagnostic identity включает mapper, canonical pair, required member symbol,
structured construction origin и member-plan leaf/cause. Paths одного origin
агрегируются; независимые constructor/member-plan leaves диагностируются
отдельно. Declaration order destination members определяет порядок diagnostics
одного leaf-а.

Recovery заменяет affected structured creation leaf typed
`MappingConfigurationException` stub-ом до вычисления constructor arguments и
member values. Existing previous, runtime-result и другие structured leaves
сохраняются. Generator не подставляет `default`, не выполняет скрытый
assignment и не считает constructor parameter эквивалентом required member-а.

### 6.85. `MORPH0042`: member rule не может быть применён

`MORPH0042` публикуется для effective explicit rule, который сам по себе valid,
но требует недоступную lifecycle-фазу хотя бы на одном reachable path. `{2}`
имеет одну из двух стабильных форм:

- `init-only member cannot be assigned after a runtime result policy has returned the result`;
- `creation-time member rule depends on result before it is created`.

Первая причина применяется к value-producing explicit rule `init`-member-а на
non-null result из `ConstructUsing` / `ResolveUsing`. `ConstructUsing`
затрагивает только no-previous paths: existing previous остаётся result, и
`init` rule на нём пропускается. `ResolveUsing` является full result policy,
поэтому потенциально non-null runtime result делает rule неприменимым на всех
его достижимых paths. Callback остаётся непрозрачным: Morphant не пытается
доказать, вернул ли он previous, cache object либо новый instance.

Explicit `Ignore` не требует assignment и потому остаётся допустимым для уже
созданного result. Unwritten `MemberSelection.Auto` convention для
неприменимого `init` также просто отсутствует; он не превращает default
runtime-policy mapping в error. Явный `Auto`, expression, `Value` либо nested
rule является value-producing и после собственной проверки применимости
попадает под `MORPH0042`.

Вторая причина применяется к `init` rule и к rule любого `required` member-а,
который должен войти в structured creation initializer, когда его value либо
условие применимости прямо или транзитивно зависит от `result`. Само наличие
третьего/четвёртого параметра `Members` dependency не создаёт. Обычный setter
либо mutable-field rule может зависеть от result и выполняется
post-construction без diagnostic.

Primary location — member designator effective explicit rule-а. Для
result-dependency первая прямая source reference, образующая dependency,
является первой additional location. Для runtime-result причины identifier
effective `ConstructUsing` / `ResolveUsing` является первой additional
location. Source-backed `IncludeBase` edge добавляется последним, если rule и
lifecycle origin встретились только после import-а.

Diagnostic identity включает mapper, canonical pair, member/rule origin,
lifecycle origin и reason kind. Один rule не размножается по operations и
generic substitutions; affected paths агрегируются в `{3}`. Независимые rules
одного member-а в разных effective plan leaves остаются независимыми.

Recovery блокирует только leaf/path, где rule должен быть применён. Structured
creation leaf бросает до construction/member values; non-null runtime result
сначала получается один раз, затем member stage бросает до assignments.
Runtime `null` по-прежнему завершает mapping до member stage и stub-а.
Previous branch, где `init` rule неприменим и не вычисляется, остаётся valid.
Morphant не превращает `init` в setter, не переносит creation-time rule после
constructor-а и не вычисляет expression только ради side effect.

### 6.86. `MORPH0043`: structured member plan равен null

`MORPH0043` публикуется, когда reachable terminal leaf structured `Members`
callback-а статически является `null`, target-typed `default`,
`default(DestinationMembers)` либо тем же значением через поддерживаемые
transparent wrappers и single-value structured local alias.

Primary location — наименьшее `null` / `default`-producing expression.
Terminal alias uses того же producer-а являются additional locations в source
order. Diagnostic identity — callback и null-producing origin; повторное
достижение producer-а из нескольких paths дедуплицируется с агрегированным
списком paths, независимые producers диагностируются отдельно.

`null!`, nullable-disabled context, explicit cast и suppression C# nullable
warning не делают member plan допустимым. Compiler warning остаётся
независимой compiler diagnostic. Empty `new DestinationMembers()` и полное
отсутствие configured `Members` являются valid plans: вторая форма оставляет
только effective conventions.

`null` runtime result `ConstructUsing` / `ResolveUsing` завершает mapping до
`Members` и не является `MORPH0043`. Manual `Convert` null также не имеет
member plan. `null` вместо `DestinationConstruction` принадлежит
`MORPH0039`.

Recovery сохраняет control-flow conditions и dependencies до invalid terminal
leaf, после чего бросает `MappingConfigurationException` до вычисления member
values и assignments. Generator не заменяет plan пустым object initializer-ом,
не включает conventions как fallback и не применяет частичный соседний plan.

### 6.87. Recovery, precedence, порядок и suppression

Все четыре diagnostics сохраняют C#-legal `ITypeMapper<,>` contract,
capability-specific fluent surfaces и независимые operations/pairs. Invalid
member-plan leaf/path бросает `MappingConfigurationException`; valid leaves и
paths исполняются по исходному declarative lifecycle.

Recovery granularity:

- `MORPH0040` блокирует leaves, использующие invalid explicit rule;
- `MORPH0041` блокирует только structured creation leaves с
  неудовлетворённым required obligation;
- `MORPH0042` блокирует lifecycle path, где valid rule нужно было бы применить
  в недоступной фазе;
- `MORPH0043` блокирует terminal null/default member-plan leaf.

Если выбранный effective plan leaf содержит хотя бы один blocking member
observation, recovery бросает до любых member assignments этого leaf-а. Это не
создаёт гарантии порядка независимых valid member rules, но исключает частично
изменённый existing result только из-за diagnostic stub-а. Control-flow
conditions, runtime result policy и зависимости, необходимые для выбора leaf-а,
сохраняют обычную evaluation semantics; значения самих заблокированных rules
не вычисляются.

Если attribution rule/leaf невозможна после valid category-8 lowering, весь
structured member callback считается атомарно invalid и primary ownership
возвращается к `MORPH0030`. Category 10 не строит эвристический pair-wide
fallback и не анализирует body runtime result callback-а.

Precedence действует так:

- gates категорий 1–4, invalid local composition/settings/inheritance
  категорий 5–7 и invalid callback transfer/grammar категории 8 подавляют
  category-10 analysis соответствующей области;
- invalid construction leaf категории 9 не получает member cascade;
  successful selection с точным required/init blocker-ом, напротив, сохраняет
  ownership категории 10 и подавляет производную `MORPH0036`;
- `MORPH0043` делает contents null plan leaf-а неизвестными и подавляет
  `MORPH0040`–`MORPH0042` только внутри него;
- `MORPH0040` invalid rule подавляет производные `MORPH0041`/`MORPH0042` того
  же member/path;
- exact `MORPH0042` lifecycle failure подавляет производную `MORPH0041` того
  же required member/path;
- explicit valid `Ignore` required member-а получает `MORPH0041`, а не
  `MORPH0040` либо `MORPH0042`;
- invalid nested `Map` / `Create` / `Update` категории 11 сохраняет точный
  nested ownership и подавляет только производную required-obligation
  diagnostic того же rule/path;
- category-12 warning-анализ исключает invalid affected paths, но продолжает
  анализировать независимый effective plan;
- точная source C# binding/type error не дублируется Morphant diagnostic-ой.

Intrinsic invalid rule origin не размножается по exact same-pair inheritance
consumers: retained origin переносит recovery, local override/discard удаляет
его. Consumer-specific cross-pair hiding либо lifecycle конфликт получает
diagnostic current pair, потому что исходный base rule остаётся valid в своём
contract-е.

Publication order — по ID. Внутри ID: ordinal mapper identity, canonical pair,
member declaration order, member-plan/construction origin, primary source
location, reason и affected paths. Additional locations сохраняют порядок,
заданный разделами 6.83–6.86.

Suppression либо severity override не меняет member selection, result phase,
required obligations, recovery или artifact set. Изменение destination member
surface/hiding, source candidates, marker exact type, `MemberSelection`,
constructor/`SetsRequiredMembers`, result policy, member control flow,
dependency on `result`, settings, override либо inheritance полностью
actualizes category-10 observations и affected stubs при одном сохранённом
incremental driver-е.

### 6.88. Самостоятельная тестовая матрица категории 10

Unit-категория member diagnostics независимо фиксирует:

- exact descriptors `MORPH0040`–`MORPH0043`: ID, title, category, default
  severity, enabled/configurable flags, message formats и все parameters;
- canonical contract/member/type/path formatting, deterministic ordering и
  exact primary/additional locations;
- explicit `Auto` без source, с ambiguous/inaccessible source и без
  warning-free conversion; отсутствие `MORPH0040` у успешного `Auto` и
  неуказанного convention member-а;
- прошедшие C# binding typed `Auto` / `Ignore` / `Value<T>` mismatches через
  broader object/boxing/nullability forms, exact valid markers и отсутствие
  Morphant duplicate для compiler-rejected mismatch/ordinary expression;
- cross-pair imported rule, скрытый eligible либо ineligible derived member-ом,
  override как тот же slot, exact same-pair import и local same-name override;
- `MORPH0041` для required set/init properties и fields при
  `MemberSelection.Auto` / `Explicit`, missing convention, explicit `Ignore`,
  conditional/switch omission, inaccessible/reserved/hiding surface;
- required satisfaction explicit expression/`Value`, valid `Auto`, valid
  nested rule и unwritten convention; constructor parameter/default member
  initializer не удовлетворяет obligation без `[SetsRequiredMembers]`;
- `[SetsRequiredMembers]` matrix для implicit, explicit и `ByConvention`
  constructors, multiple construction leaves и independent required members;
- отсутствие `MORPH0041` на previous/runtime-result paths и точная ownership
  invalid explicit/lifecycle/nested rule без derivative required diagnostic;
- `MORPH0042` для explicit value-producing init rule после
  `ConstructUsing`/`ResolveUsing`, paths каждого result policy, runtime-null
  short circuit, valid `Ignore` и skipped unwritten/previous init rules;
- direct/transitive/local/condition dependency on `result` для init и required
  creation-time rules, отсутствие ошибки для result-independent rules и
  result-dependent ordinary setter/mutable-field rules;
- `MORPH0043` для `null`, `null!`, target-typed/typed `default`, transparent
  casts/wrappers, conditional/switch leaves и local aliases; producer
  deduplication и exact use additional locations;
- valid empty/no `Members` plan, runtime/manual null и category-9 construction
  null без `MORPH0043`;
- operation/previous/result-origin reachability across every `MappingMode`,
  null destination policy, structured previous/replacement branches and all
  four result-policy families;
- branch-atomic full generated recovery for every ID: legal interfaces and
  fluent surfaces, throwing affected leaf before values/assignments,
  preserved runtime-null short circuit, previous reuse, independent
  branches/operations/pairs and no hidden fallback;
- category 9/11 ownership and suppression, category-12 exclusion of invalid
  paths, compiler-owned diagnostics and no dependent cascades;
- local/exact/cross-pair inheritance origin laws, retained versus discarded
  rules, consumer-specific conflicts and independent local overrides;
- real suppression/severity override for all four IDs without changing
  recovery/artifacts;
- actualization member/source declarations, hiding/override, marker type,
  required/`SetsRequiredMembers`, result policy, callback control flow,
  `result` dependency, settings and inheritance при одном incremental driver-е.

Package-like integration-категория независимо проверяет:

- suppressed `MORPH0040` for invalid explicit `Auto`, exact-marker mismatch
  and imported hidden rule: affected plan throws without value/assignment,
  valid branch and local override execute normally;
- suppressed `MORPH0041` on Create, null-Update and existing-Update structured
  replacement: invalid creation throws before constructor/member side effects,
  previous reuse, `[SetsRequiredMembers]` and runtime-result paths remain
  executable;
- suppressed `MORPH0042` for result-dependent structured initializer,
  `ConstructUsing` init and full `ResolveUsing` init: non-null affected path
  throws, runtime null returns terminally, previous/skipped and ordinary
  setter paths execute;
- suppressed `MORPH0043` for null/default member-plan leaves while empty plan,
  non-null conditional branch and independent member plan preserve their
  control flow and side effects;
- one invalid member rule never partially mutates an existing result before
  recovery throw; branch not selecting the rule still applies all valid
  members;
- exact/inherited origin recovery, independent pair isolation and no category
  11–12 cascade from invalid member leaf;
- real `.editorconfig` / MSBuild suppression or severity override for all four
  IDs without changing generated artifact set and effective recovery.

### 6.89. Категория 11: общий contract

Категория «Корректность nested mapping» содержит ровно три diagnostics:

| ID | Title | Message format |
|---|---|---|
| `MORPH0044` | `Nested mapping pair cannot be determined` | `Nested mapping pair for marker '{0}' in contract '{1}' cannot be determined: {2}. Reachable paths: {3}.` |
| `MORPH0045` | `Nested mapping result is incompatible` | `Nested mapping result type '{0}' in contract '{1}' cannot be converted warning-free to target type '{2}'. Reachable paths: {3}.` |
| `MORPH0046` | `Nested Update destination is invalid` | `Nested Update destination for marker '{0}' in contract '{1}' is invalid: {2}. Reachable paths: {3}.` |

Для всех трёх diagnostics действует общий descriptor contract:

- category — `Morphant.NestedMapping`;
- default severity — `Error`;
- diagnostic включена по умолчанию и не имеет `NotConfigurable`;
- description и help link отсутствуют, custom tags пусты;
- анализируется только effective declarative nested marker, оставшийся после
  успешных категорий 1–10 и специализации outer result/member paths;
- manual `Convert`, `ConstructUsing` и `ResolveUsing` используют настоящий
  runtime `IMapper` либо обычный C# callback и категорией 11 не разбираются;
- `UnmappedMemberValidation` не скрывает category-11 error и не понижает её до
  warning;
- suppression либо изменение severity меняет только compiler presentation и
  не выбирает nested pair, не вставляет conversion, не меняет Create на Update
  или наоборот и не включает runtime fallback.

Outer mapping contract в `{1}` форматируется по canonical identity категории
3. Marker name в `{0}` у `MORPH0044` и `MORPH0046` является одним из `Map`,
`Create` либо `Update` без generic suffix-а. Nested source, destination и
target types используют fully-qualified nullable-aware display после mapper
generic substitutions. Target означает конечный destination member либо
constructor parameter, а для standalone read-only Update — соответствующий
get-only member proxy.

Affected paths используют общие стабильные имена категорий 9–10:

1. `Create`;
2. `Update without a previous destination`;
3. `Update with a previous destination`.

Message перечисляет paths в этом порядке через `, `. Один marker origin не
размножается по public operations, если одинаковая причина и recovery
затрагивают несколько paths. Разные terminal target uses declarative local-а
остаются разными observations, когда их pair, conversion либо current
destination различаются.

### 6.90. Nested origins, static pair и lifecycle model

Category-11 observation принадлежит одному из следующих origins:

- nested marker invocation, являющемуся producer-ом source/destination pair;
- конечному member/constructor target use этого marker-а;
- explicit destination argument nested `Update`;
- generated current destination adaptive `Map` на конкретном outer path;
- standalone destination proxy read-only nested `Update`;
- набору terminal uses одного adaptive declarative local-а, которым нужны
  разные current destinations.

После category-8 lowering и category-9/10 specialization generator отдельно
анализирует каждый reachable construction/member leaf. Conditional/switch
branches, declarative locals, `with` overlays, overridden rules и actual
result origin уже разрешены. Discarded marker, недостижимая branch и path, на
котором member rule не применяется, category-11 observation не получают.

Static nested pair определяется без application lookup:

- source type берётся из естественного статического типа explicit
  source-expression до conversion к `object?` marker surface;
- у parameterless `Map()` source выводится из unique readable source
  property/field по точному имени конечного target-а;
- destination type берётся из explicit generic argument либо из конечного
  member/constructor target-а у non-generic marker-а;
- mapper generic substitutions и canonical type normalization применяются до
  проверки pair, но runtime type source/current destination pair не меняет;
- наличие local pair, mapper type, assembly или service registration не
  участвует в выводе: dispatch остаётся application-wide.

Adaptive `Map` получает nested operation из фактического outer lifecycle.
`Create` и Update без previous вызывают nested Create. Update с previous
вызывает nested Update: writable member читает current member фактически
выбранного `result`, включая replacement, а constructor parameter использует
соответствующий readable member outer `previous`. Explicit `Create` и
`Update` сохраняют выбранную nested operation на любом outer path.

Declarative local является alias marker producer-а и получает target context
от каждого terminal use. Один producer может безопасно использоваться в
нескольких Create contexts с одной pair/conversion. На adaptive Update один и
тот же producer не может одновременно обозначать разные current destinations:
это не порядок вычисления, а неоднозначная lifecycle dependency.

Standalone `Update(source, members.Member)` является отдельным terminal
statement только для get-only proxy текущего effective `DestinationMembers`
plan. Eligible proxy представляет non-opaque reference-type destination,
читает actual `result.Member` один раз, выполняет null guard до source и
отбрасывает returned nested replacement. Writable member, другой plan local,
обычное значение либо non-terminal use proxy semantics не получают.

Category 11 не пытается доказать:

- существует ли registration вычисленной nested pair;
- одна она либо registrations несколько и вернула ли factory non-null mapper;
- разрешена ли nested Create/Update effective `MappingMode` найденного mapper-а;
- какое runtime значение хранится в широком adaptive current slot-е.

Первые три состояния сохраняют утверждённые runtime lookup/operation failures.
Последнее проверяется generated runtime guard-ом и при incompatible non-null
значении бросает `NestedDestinationTypeMismatchException`.

### 6.91. `MORPH0044`: nested pair нельзя определить

`MORPH0044` публикуется, когда успешно C#-связанный terminal marker не задаёт
одну статическую source/destination pair. Стабильный `{2}` reason имеет одну
из форм:

- `source expression does not have a statically determined type`;
- `parameterless Map does not resolve a unique readable source member named '{memberName}'`;
- `non-generic marker has no final target from which to infer the destination type`.

Первая форма охватывает untyped `null`, targetless `default` и transparent
aliases этих expressions. Conversion public marker argument-а к `object?` не
делает `object` nested source type-ом: это потеряло бы заявленную exact pair.
Явно типизированные `(Source?)null` и `default(Source)` имеют static source type
и этой причиной не затрагиваются.

Вторая форма относится только к `Map()` / `Map<TDestination>()`. Поиск source
использует effective target name и readable source surface с правилами hiding,
accessibility и exact ordinal name. Отсутствие candidate либо отсутствие
однозначного readable symbol-а не заменяется похожим именем, runtime type-ом
или поиском зарегистрированной pair. Для constructor parameter no-previous
path может использовать имя parameter-а; если existing Update требует
отсутствующую association с readable destination member-ом, первичная причина
принадлежит `MORPH0046`, а не второй форме `MORPH0044`.

Третья форма возможна только для terminal marker-а, чья placement сама по себе
допустима category 8, но target context не предоставляет destination type.
Non-terminal marker, return/capture/cast/method-group use либо marker в runtime
callback по-прежнему принадлежит `MORPH0033`. Unnameable/file-local type с уже
определённой identity является transfer failure `MORPH0030`, а malformed C#
binding с error type остаётся compiler-owned.

Primary location для source-expression reason — само untyped expression. Для
parameterless inference и отсутствующего target-а primary — identifier `Map`,
`Create` либо `Update`. Конечный member/parameter designator, declarative local
use и соответствующая source/destination declaration добавляются как
additional locations в source order, если имеют spans.

Diagnostic identity включает mapper, outer canonical pair, marker producer,
terminal target context и normalized set missing side/reason. Один marker/use
получает одну `MORPH0044`; если независимо не определены обе стороны, `{2}`
перечисляет source- и destination-reasons в этом порядке через `; `. Primary
берётся у первой причины, location второй становится additional. Producer не
повторяется для каждого outer path; разные terminal target contexts остаются
разными observations только когда их inference либо recovery различаются.

Recovery блокирует только construction/member leaf и paths, которым нужна
неопределённая pair. Marker arguments не вычисляются; leaf бросает
`MappingConfigurationException`. Paths, где marker либо его invalid terminal
use не достигается, а также independent rules/branches/pairs остаются
исполняемыми.

### 6.92. `MORPH0045`: nested result несовместим с target

`MORPH0045` публикуется, когда статическая nested pair определена, но выбранный
nested destination type не имеет warning-free implicit C# conversion к
конечному member/constructor target type. Diagnostic относится прежде всего к
generic `Map<TDestination>`, `Create<TDestination>` и
`Update<TDestination>` либо к их declarative local alias: у прямой
non-generic формы destination уже равен target type.

Warning-free law использует ту же compilation/nullable context, что и
generated call/assignment. Разрешены стандартные и user-defined implicit
conversions, включая reference/interface/variance, numeric/lifted, tuple и
boxing, если probe не создаёт warning. Narrowing/downcast/unboxing, explicit
operator и nullable-warning conversion не становятся допустимыми через cast
либо null-forgiving operator generator-а.

`MORPH0045` является post-binding diagnostic. Если C# уже отверг initializer,
marker overload либо generic type argument и Morphant не может установить
symbol/target identity, отдельная diagnostic не публикуется. Exact type
совпадение не требуется: в отличие от `Value<T>` и typed `Auto`/`Ignore`,
generic nested destination намеренно допускает warning-free conversion к
более широкому target-у.

Primary location — explicit `TDestination` type argument marker producer-а.
Если generic destination пришёл через alias и его source type argument span
недоступен, primary становится marker identifier. Конечный member/parameter
designator и declaration target type-а добавляются как additional locations.
Для consumer-specific cross-pair conversion, которая была valid в source
contract-е, primary переносится на effective `IncludeBase` edge, а исходный
type argument и current target становятся additional locations.

Diagnostic identity включает mapper, outer canonical pair, marker producer,
terminal target use, nested destination/target types и conversion reason.
Несколько outer paths одного use агрегируются; один local, использованный с
разными target types, получает отдельную `MORPH0045` только для каждого
несовместимого use.

Recovery блокирует affected leaf до nested dispatch, constructor arguments и
member assignments. Compatible uses того же local, другие control-flow leaves
и independent paths сохраняются. Generator не меняет explicit nested
destination, не выбирает non-generic overload и не вставляет conversion,
которой пользователь не написал.

### 6.93. `MORPH0046`: destination nested Update недопустим

`MORPH0046` публикуется, когда pair и final result conversion определены, но
nested Update не имеет допустимого destination input на reachable path.
Стабильный `{2}` reason имеет одну из форм:

- `explicit destination expression of type '{actualType}' does not have a warning-free implicit conversion to nested destination type '{destinationType}'`;
- `explicit null destination cannot represent non-nullable value destination type '{destinationType}'`;
- `adaptive Map has no readable current destination for target '{target}'`;
- `current destination slot of type '{currentType}' cannot contain nested destination type '{destinationType}'`;
- `adaptive Map local is associated with multiple current destinations: {targets}`;
- `standalone Update destination is not an eligible get-only DestinationMembers proxy`.

Первые две формы относятся к explicit `Update(source, destination)` и
`Update<TDestination>(source, destination)`. Marker public surface принимает
`object?`, но generated scoped call должен получить normalized nullable
destination input (`TDestination?` для reference type) по обычной warning-free
implicit conversion. Explicit `null` допустим для reference и
nullable value destination и передаётся nested null policy; для non-nullable
value destination он invalid. Target-typed `default` обозначает настоящий
`default(TDestination)` и сам по себе допустим. В отличие от generated
adaptive current slot-а, explicit user argument типа `object` не получает
скрытый runtime downcast: пользователь задаёт compatible static expression.

Третья форма относится к adaptive `Map` только на existing-previous outer
path. Writable member всегда берёт current value из фактического `result`.
Constructor parameter обязан однозначно связаться с readable destination
member outer `previous`: exact name имеет приоритет, затем разрешено одно
unique `OrdinalIgnoreCase` совпадение. Без association nested Create на
no-previous paths остаётся valid, а nested Update path получает
`MORPH0046`.

Четвёртая форма применяется к generated adaptive current slot-у и eligible
read-only proxy. Static slot должен хотя бы быть способен содержать выбранный
`TDestination`. Exact/implicit conversion не требуется: `object`, interface
либо base slot, для которого runtime-compatible value возможен, разрешён и
получает generated checked conversion. Фактически incompatible non-null
значение бросает `NestedDestinationTypeMismatchException`; `null` writable
slot передаётся nested Update, если destination type способен его представить.
Если selected destination — non-nullable value type, runtime `null` также
бросает typed mismatch до dispatch. Заведомо unrelated sealed/static shapes
получают compile-time `MORPH0046`.

Пятая форма относится к одному adaptive marker producer-у, terminal uses
которого на одном reachable Update plan требуют разных current expressions.
Разные текстовые выражения одного exact current slot-а не создают ambiguity
после semantic normalization; разные members/constructor associations
создают. Mutually exclusive uses остаются конфликтом, если producer вычислен
до выбора target-а и не имеет единственного current destination. Local нужно
разделить на отдельные marker invocations либо выбрать explicit Update.

Шестая форма относится только к terminal expression statement `Update(...)`
в structured `Members`. Destination обязан быть get-only marker current
effective `DestinationMembers` local-а. Writable marker, proxy другого local-а,
обычное destination expression либо попытка использовать proxy не как
standalone statement не получают in-place/discard semantics. Non-terminal use
marker-а по-прежнему принадлежит `MORPH0033`; отсутствие самого proxy из-за
неподдерживаемого destination member surface обычно остаётся точной C# binding
error и не дублируется.

Primary location для explicit forms — destination argument expression. Для
adaptive current reasons — identifier `Map`; target designator/current member
declaration и explicit `TDestination` добавляются как additional locations.
Для ambiguous local primary — producer `Map`, а все conflicting terminal
target designators являются additional locations в source order. Для wrong
read-only proxy primary — destination argument member access, identifier
`Update` становится первой additional location. Consumer-specific failure
valid inherited rule-а использует effective `IncludeBase` edge как primary.

Diagnostic identity включает mapper, outer canonical pair, marker producer,
destination/current origin, terminal target set и reason. Одна причина
агрегирует paths, но разные explicit destination arguments и разные
consumer-specific current slots остаются независимыми. Ambiguous local даёт
одну diagnostic на producer и normalized conflicting target set.

Recovery блокирует только leaves/paths, где nested Update destination invalid.
Adaptive no-previous nested Create того же marker-а, branch с одной valid
current destination, compatible runtime-wide slot и unrelated nested markers
сохраняются. Invalid leaf бросает `MappingConfigurationException` до чтения
current destination, вычисления explicit source/destination arguments и
nested dispatch.

### 6.94. Recovery, precedence, порядок и suppression

Все три diagnostics сохраняют C#-legal outer `ITypeMapper<,>` contract,
capability-specific fluent surfaces и application-wide runtime dispatch.
Invalid nested leaf/path бросает `MappingConfigurationException`; valid nested
calls используют исходные static pair, operation, argument order и shared
mapping scope.

Recovery granularity:

- `MORPH0044` блокирует leaves, которым нужна неопределённая source либо
  destination side pair;
- `MORPH0045` блокирует только incompatible terminal target uses;
- `MORPH0046` блокирует explicit/adaptive/read-only Update paths с invalid
  destination input.

Construction leaf с blocking nested observation бросает до вычисления
constructor arguments. Member-plan leaf бросает до любых assignments и
standalone in-place updates этого leaf-а, сохраняя branch-atomic law категории
10. Control-flow conditions и dependencies, необходимые для выбора leaf-а,
сохраняют обычную evaluation semantics; marker arguments и current members
заблокированного nested call не вычисляются.

Если marker/target attribution невозможна после valid category-8 lowering,
весь соответствующий structured fragment считается transfer-invalid и
ownership возвращается к `MORPH0030`. Category 11 не угадывает pair по
generated text, регистрации либо имени похожего member-а.

Precedence действует так:

- gates категорий 1–4, invalid composition/settings/inheritance категорий 5–7
  и invalid transfer/grammar категории 8 подавляют category-11 analysis своей
  области;
- invalid construction leaf категории 9 и invalid/null/lifecycle member leaf
  категории 10 не получают nested cascade;
- exact invalid nested marker категории 11 подавляет производную
  `MORPH0041` required-obligation diagnostic того же rule/path;
- `MORPH0044` подавляет `MORPH0045`/`MORPH0046`, которым неизвестна
  соответствующая source/destination side;
- `MORPH0045` подавляет только производную adaptive-current incompatibility
  `MORPH0046` того же target use; independently invalid explicit destination
  argument может публиковаться рядом;
- ambiguous adaptive-local analysis учитывает только otherwise valid terminal
  uses и потому не каскадирует от уже invalid target-а;
- non-terminal/runtime marker остаётся `MORPH0033`, inaccessible/file-local
  transfer — `MORPH0030`, а точная C# binding/type error не дублируется;
- missing/ambiguous/null runtime registration, disabled nested operation и
  actual mismatch широкого current slot-а не получают category-11 diagnostic;
- category-12 warning-анализ исключает invalid affected paths, но продолжает
  анализировать independent effective plan.

Intrinsic origin diagnostic не размножается по exact/same-context inherited
consumers: retained origin переносит recovery, local override/discard удаляет
его. Cross-pair substitution, новый target conversion/current slot либо
изменившаяся constructor association создаёт consumer-specific observation на
effective edge.

Publication order — по ID. Внутри ID: ordinal mapper identity, outer canonical
pair, marker producer, terminal target declaration order/source location,
reason и affected paths. Внутри combined `MORPH0044` source-side reason
предшествует destination-side reason. Additional locations сохраняют порядок
разделов 6.91–6.93.

Suppression либо severity override не меняет pair inference, nested operation,
conversion, current destination, read-only guard, runtime lookup, recovery или
artifact set. Изменение source/target/current member types и declarations,
marker overload/generic argument, terminal local uses, construction/member
control flow, outer result policy, null/operation settings, override либо
inheritance полностью actualizes category-11 observations и affected stubs при
одном сохранённом incremental driver-е.

### 6.95. Самостоятельная тестовая матрица категории 11

Unit-категория nested mapping diagnostics независимо фиксирует:

- exact descriptors `MORPH0044`–`MORPH0046`: ID, title, category, default
  severity, enabled/configurable flags, message formats и все parameters;
- canonical outer contract, marker, nullable-aware type/target/path formatting,
  deterministic ordering и exact primary/additional locations;
- все восемь marker forms в constructor parameter, writable member,
  declarative local и допустимом standalone read-only statement;
- `MORPH0044` для untyped `null`/`default` source и aliases, valid typed null/
  default, explicit source natural type до `object?` conversion;
- parameterless `Map` source inference: exact readable property/field,
  missing/ambiguous/inaccessible/hiding candidates, constructor parameter
  target association и path-specific fallback name;
- destination inference non-generic/generic/targetless forms, compiler-owned
  malformed binding, `MORPH0033` non-terminal marker и `MORPH0030` unnameable
  transfer без duplicate category-11 diagnostic;
- `MORPH0045` exact/reference/interface/variance/numeric/lifted/tuple/boxing/
  user-defined implicit positive conversions и narrowing/downcast/unboxing/
  nullable-warning negative conversions;
- generic marker locals с compatible и incompatible multiple targets,
  per-use cardinality и отсутствие exact-target restriction nested result-а;
- `MORPH0046` explicit Update destination exact/implicit/nullable forms,
  reference/nullable/non-nullable `null`, target-typed `default`, broad `object`
  argument и compiler-rejected expression;
- adaptive Create/Update specialization across no previous, existing previous,
  replacement result и constructor parameter current-member association;
- current slot matrix: exact, nullable, object, base/interface/boxing-capable,
  unrelated sealed/value shapes, runtime guard requirement и отсутствие
  compile-time diagnostic для potentially compatible wide slot;
- adaptive local reuse for same normalized current, different writable members,
  constructor associations, mutually exclusive terminal uses and separate
  producers;
- eligible get-only property/readonly field proxy, wrong writable/foreign
  proxy, generic destination compatibility, null guard, single read, skipped
  source on null and discarded replacement;
- outer reachability across every `MappingMode`, null destination policy,
  structured/runtime result origin, member overlays and conditional/switch
  branches;
- absence of category-11 diagnostics for missing/ambiguous/null registration,
  disabled nested operation and runtime mismatch wide slot; exact runtime
  exception ownership remains unchanged;
- branch-atomic full generated recovery for every ID: throwing affected leaf
  before marker arguments/constructor/member side effects, preserved adaptive
  Create, valid Update branch, independent rules/operations/pairs and no hidden
  fallback;
- category 8–10 precedence, derivative required suppression, category-12
  exclusion, compiler/runtime ownership and independently provable explicit
  destination error;
- local/exact/cross-pair inheritance origin laws, retained/discarded marker,
  consumer-specific conversion/current failure and local override;
- real suppression/severity override for all three IDs without changing
  recovery, dispatch or generated artifacts;
- actualization source/target/current declarations, marker type argument,
  local use graph, result policy, settings, control flow and inheritance при
  одном incremental driver-е.

Package-like integration-категория независимо проверяет:

- suppressed `MORPH0044` для untyped source, failed parameterless inference и
  unavailable non-generic target: affected path throws before source effects,
  typed/explicit pair и independent branch исполняются;
- suppressed `MORPH0045` для generic destination nullable/type mismatch:
  incompatible use throws, compatible wider target и другой use того же local
  выполняют реальный nested dispatch;
- suppressed `MORPH0046` для explicit destination, unavailable/impossible
  adaptive current, ambiguous local и wrong read-only proxy: invalid Update
  throws без partial mutation, no-previous adaptive Create и valid current
  path сохраняются;
- wide object/base/interface adaptive current с compatible runtime value,
  incompatible non-null value и runtime null реально даёт соответственно
  nested Update либо `NestedDestinationTypeMismatchException` по design law;
- get-only proxy non-null/null paths сохраняют single read, source skip и
  discard replacement, а invalid sibling leaf не выполняет in-place update;
- application-wide nested registration вне outer mapper/assembly, missing/
  ambiguous/null registration и disabled operation сохраняют runtime lookup/
  operation exceptions без compile-time category-11 error;
- exact/inherited origin recovery, consumer-specific cross-pair failure,
  independent pair isolation и отсутствие category 9–10/12 cascade;
- real `.editorconfig` / MSBuild suppression or severity override для всех
  трёх IDs без изменения generated artifact set и effective recovery.

### 6.96. Категория 12: общий contract

Категория «Полнота mapping-а через `UnmappedMemberValidation`» содержит ровно
две diagnostics:

| ID | Title | Message format |
|---|---|---|
| `MORPH0047` | `Source member is not used` | `Source member '{0}' in contract '{1}' does not participate in the effective mapping plan.` |
| `MORPH0048` | `Destination member is not mapped` | `Destination member '{0}' in contract '{1}' is not mapped by the effective mapping plan.` |

Для обеих diagnostics действует общий descriptor contract:

- category — `Morphant.MappingCompleteness`;
- default severity — `Warning`;
- diagnostic включена по умолчанию и не имеет `NotConfigurable`;
- description и help link отсутствуют, custom tags пусты;
- анализируется только effective non-manual pair после успешного разрешения
  категорий 1–11, supported member surfaces и reachable plan;
- `UnmappedMemberValidation.None` не публикует ни одну diagnostic, `Source`
  включает только `MORPH0047`, `Destination` — только `MORPH0048`, а `Strict`
  — обе независимо;
- manual `Convert` полностью исключён из category-12 analysis независимо от
  inherited setting; explicit map-level setting на manual pair остаётся
  category-6 applicability error;
- warning не меняет generated code, runtime behavior, convention selection,
  lifecycle, evaluation order, diagnostics recovery либо artifact set;
- suppression, понижение либо повышение severity до `Error` меняет только
  compiler presentation и не превращает incompleteness в throwing stub.

Member display `{0}` использует fully-qualified containing type с nullable-
aware generic substitutions и точное объявленное member name, например
`global::Example.Source.LegacyId`. Contract `{1}` форматируется по canonical
identity категории 3. Category 12 намеренно не перечисляет Create/Update paths
в message: participation pair-wide и один member не размножается по
operations/branches.

### 6.97. Supported member universes и pair-wide participation

Source и destination validation universes строятся независимо до поиска
неучаствующих members. Root nullable reference annotation снимается так же,
как для pair capability, но вложенная nullability member type-а сохраняется.
Member identity является symbol-based после mapper generic substitutions;
одинаковое имя разных hiding slots не объединяется.

Supported source universe содержит instance properties с доступным readable
getter-ом и instance fields нормализованного non-opaque source root-а, включая
readonly field. Применяются те же assembly accessibility, override/hiding и
interface most-derived/ambiguity laws, что к convention source surface.
Static/const, indexer, ref-return, fixed buffer, explicit interface member без
доступного receiver-а и нечитаемый member исключены. Для включения не
требуется name match либо conversion к конкретному destination: именно
отсутствие любого effective use такого readable member-а проверяет
`MORPH0047`. Scalar/opaque source root самостоятельного member universe не
получает.

Supported destination universe содержит:

- ordinary body-member surface: property с доступным setter-ом либо `init` и
  mutable field;
- readable otherwise non-writable member, однозначно ассоциированный хотя бы с
  одним parameter-ом фактически выбранного structured constructor-а на valid
  reachable branch;
- один canonical slot для override и один most-derived interface slot по
  общим member-surface laws.

Constructor association ищет readable destination member сначала по exact
parameter name, затем по единственному `OrdinalIgnoreCase`-совпадению. Member,
который нельзя занять ни effective constructor argument-ом, ни ordinary
member rule, не включается только ради неизбежного warning. Generated get-only
proxy для standalone nested Update также исключён: он не является обычным
destination state, которое Morphant способен присвоить. Static/const,
indexer, ref-return, fixed buffer, inaccessible, ambiguous и прочие
unsupported members validation universe не получают.

Conditional/switch construction даёт объединение supported destination slots
всех valid reachable constructor selections. Разные reachable constructors,
занимающие один symbol, не дублируют member. Если одна branch не выбирается ни
одной enabled operation либо доказанно недостижима после specialization, её
constructor/member surface в completeness не участвует.

Participation pair-wide: member достаточно использовать либо занять хотя бы в
одной valid reachable operation/branch effective plan-а. Поэтому:

- `init` member, заполняемый только при structured Create/replacement, уже
  участвует и не предупреждается из-за existing Update reuse;
- conditional rule занимает member, даже если runtime condition может выбрать
  другую branch;
- source read, нужный только одной enabled operation, является use всей pair;
- один member получает не более одного warning-а, без отдельных variants для
  Create, Update и conditional branches;
- простое сохранение existing destination без constructor argument/member rule
  destination participation не создаёт.

Доказанно недостижимые branches, overridden rules/result policies, discarded
inherited fragments и invalid slices не создают фиктивного participation.
Category 12 проверяет effective model, а не текстовое наличие member name в
`Configure`.

### 6.98. Semantic source use и compile-time source discard

Source member считается used, когда его runtime value семантически читается
effective plan-ом. Анализ охватывает:

- structured `Construct`, `Resolve` и `Members`: conditions, selectors,
  initialized locals, constructor/member expressions, `Value`, nested marker
  source/destination arguments и переносимые deferred values;
- convention и explicit `Auto`, фактически выбравшие этот source member;
- inline expression/block `ConstructUsing` и `ResolveUsing`, но только для
  source-use — callback result не интерпретируется как destination mappings.

Root member read учитывается и при дальнейшем chain-е (`source.Address.City`
использует root member `Address`). `nameof(source.Member)`, declaration symbol,
type test без чтения member value и другой symbol-only reference use не
создают. Text/syntax match без semantic binding к exact member также не
участвует.

Если whole source value передан непрозрачному helper/delegate/constructor,
использован receiver-ом arbitrary instance call-а, возвращён как opaque value
либо иначе покидает анализируемый value-flow, все supported source members
считаются potentially used. То же действует для natural method group,
materialized/conditional runtime delegate и другого valid runtime callback-а,
body которого generator не видит. Morphant не способен доказать, какие
members такой код прочитает, и выбирает отсутствие ложных `MORPH0047`.
Передача одного `source.Member` в opaque code использует только этот root
member, если whole source отдельно не уходит.

Отдельный explicit escape hatch — direct statement внешнего structured
callback body:

```csharp
_ = source.LegacyValue;
```

Он является compile-time source discard, а не source use. Точный contract:

- left-hand `_` должен семантически быть C# discard, а не local/parameter с
  именем `_`;
- right-hand side после снятия parentheses — direct supported property/field
  exact source parameter-а текущего structured callback-а;
- statement не находится внутри `if` / `switch`, nested block, loop,
  deferred lambda либо local function;
- receiver/member expression и getter при mapping-е не вычисляются;
- member pair-wide исключается только из `MORPH0047`; conventions,
  `MORPH0048`, generated assignments и runtime values не меняются;
- несколько members исключаются отдельными statements;
- chain, alias, tuple, indexer, conditional access, conversion, invocation,
  arbitrary expression и reference через `previous` / `result` специальной
  семантики не получают и принадлежат `MORPH0031`;
- discard внутри deferred lambda/local function является обычным runtime C#;
  в `ConstructUsing` / `ResolveUsing` тот же statement действительно читает
  getter и потому является normal semantic use;
- retained discard участвует pair-wide независимо от structured family и
  конкретной reachable operation; недостижимый, overridden либо discarded
  inherited callback slice suppression не сохраняет.

Source discard хранится как самостоятельная effective observation. Он не
подменяется скрытым `IgnoreSource`, не генерирует runtime statement и не
считается mapping rule для source/destination conventions.

### 6.99. `MORPH0047`: source member не используется

`MORPH0047` публикуется для каждого supported source member effective pair,
когда одновременно выполнены условия:

1. effective `UnmappedMemberValidation` равно `Source` либо `Strict`;
2. ни один valid reachable effective slice семантически не читает member;
3. member не покрыт whole-source potentially-used observation;
4. retained compile-time source discard exact member-а отсутствует;
5. category-8–11 error/unknown slice не делает его potential use
   недостоверным.

Отсутствие matching destination name, несовместимость convention conversion,
`MemberSelection.Explicit`, использование другого source member-а и
destination `Ignore()` сами по себе warning не снимают. Source member,
который только сохраняется как часть исходного object graph без чтения plan-ом,
также остаётся unused.

Primary location — source type argument соответствующего effective
`Map<TSource, TDestination>` registration-а. Declaration source member-а
добавляется первой additional location, если находится в текущей compilation;
для metadata member additional location отсутствует. Alias/nullable syntax
registration-а не меняет symbol identity, но primary сохраняет фактический
пользовательский type-argument span.

Diagnostic identity — mapper, canonical pair и supported source member symbol
после substitutions. Несколько registrations одной pair раньше принадлежат
категории 3; inherited consumers анализируют собственную effective pair и не
получают origin fan-out. Несколько missing uses одного member-а дают один
warning.

`MORPH0047` recovery не имеет: mapper генерируется ровно как при
`UnmappedMemberValidation.None`. После suppression getter не добавляется и
source value не snapshot-ится. Fix состоит в настоящем use, whole-source
opaque handoff, exact compile-time source discard либо изменении effective
setting, но generator не выбирает fix автоматически.

### 6.100. Destination occupancy model

Supported destination member считается mapped/occupied, если хотя бы один
valid reachable effective slice содержит:

- explicit member value, terminal `Value`/`Auto`/nested marker либо convention
  member rule для exact slot-а;
- member-level `Ignore()`, намеренно сохраняющий constructor/runtime/default
  value или current selected result;
- фактически переданный argument выбранного structured constructor-а,
  однозначно ассоциированный с member-ом по законам раздела 6.97;
- valid creation-time `init`/`required` rule для slot-а, даже если он не
  применяется к already-created result.

Occupancy означает участие Morphant plan-а, а не доказательство конкретного
runtime значения. Conditional assignment/`Ignore` достаточно при одной
reachable branch. Explicit `Members` rule сильнее constructor occupancy, но
не создаёт второй member observation.

Не занимают destination member:

- optional/`params` constructor parameter, который фактически опущен;
- constructor-parameter `Ignore()`, означающий omission, а не member-level
  decision;
- parameter без unique destination member association;
- default/parameterless construction, field/property initializer и CLR
  default сами по себе;
- `[SetsRequiredMembers]` на constructor-е: attribute снимает required
  obligation, но не утверждает, какие members mapper заполнил;
- reuse existing destination, непригодный convention candidate либо
  read-only standalone nested Update proxy;
- object, возвращённый `ConstructUsing` / `ResolveUsing`: runtime callback
  opaque для destination completeness, даже если C# body содержит object
  initializer либо assignments.

Invalid member/constructor/nested rule не становится occupancy. Вместо
производного warning-а exact target может быть suppressed по precedence
раздела 6.102, но suppressed error не превращает invalid rule в valid mapping.

### 6.101. `MORPH0048`: destination member не mapped

`MORPH0048` публикуется для каждого supported destination member effective
pair, когда одновременно выполнены условия:

1. effective `UnmappedMemberValidation` равно `Destination` либо `Strict`;
2. ни один valid reachable constructor/member slice member не занимает;
3. member-level `Ignore()` отсутствует;
4. category-8–11 error/unknown slice не делает возможную occupancy
   недостоверной.

Member может получить warning при `MemberSelection.Auto`, если matching source
отсутствует либо conversion не warning-free, и при `MemberSelection.Explicit`,
если rule не задан. Отсутствие mutation на existing Update само по себе не
создаёт второй warning, если member занят на Create/replacement branch.
`required` member, не удовлетворённый на reachable creation path, остаётся
`MORPH0041`; `MORPH0048` не дублирует эту точную ошибку для того же slot-а.

Primary location — destination type argument соответствующего effective
`Map<TSource, TDestination>` registration-а. Declaration destination member-а
добавляется первой additional location, если находится в текущей compilation;
metadata member additional location не имеет. Conditional constructors и
несколько possible member-rule origins location не меняют: warning относится
к pair/member, а не к одной branch.

Diagnostic identity — mapper, canonical pair и supported destination member
symbol после substitutions. Override учитывается один раз; hiding slots
различаются, если оба реально входят supported universe. Inherited consumer
получает собственный warning только для своей effective pair. Один member не
размножается по constructors, operations и branches.

`MORPH0048` recovery не имеет: constructor/member selection и generated
assignments остаются прежними. Suppression не вставляет `Ignore()`, convention,
default assignment либо mutation existing destination.

### 6.102. Precedence, uncertainty, порядок и suppression

Category 12 запускается последней и никогда не заменяет error более ранней
категории. Precedence действует так:

- compilation/mapper/pair gates категорий 1–3 и недостоверный builder flow
  категории 4 подавляют completeness в своей области;
- invalid local composition, effective setting либо inheritance edge
  категорий 5–7 разрешаются до validation; invalid
  `UnmappedMemberValidation` полностью отключает category 12 affected pair;
- manual `Convert` не анализируется; discarded declarative/runtime fragments
  не оставляют source use, discard либо destination occupancy;
- category-8 callback error, из-за которой body/branch невозможно разобрать,
  подавляет только warnings members, чьё use/occupancy могло находиться в
  неизвестном slice; independently provable members остального plan-а
  продолжают анализироваться;
- exact category-9 constructor error подавляет производные destination
  warnings ассоциированных slots и source warnings values неизвестного/
  invalid constructor leaf-а;
- exact category-10 member error подавляет `MORPH0048` target slot-а и
  `MORPH0047` source members, возможное use которых принадлежит invalid rule;
- exact category-11 nested error применяет ту же локальную policy к target и
  marker arguments; независимые unmapped members warnings сохраняются;
- source-visible C# error, из-за которой exact member/callback binding
  неизвестен, остаётся compiler-owned и не порождает speculative category-12
  warnings в затронутой области.

Unknown set вычисляется из structured observations, а не глобальным правилом
«любая error скрывает всю pair». Если недоступен весь structured callback,
подавляются все source/destination members, которые callback по своей family
мог использовать/занять. Если invalid один terminal member rule, подавление
ограничивается target-ом и source dependencies этого rule. Runtime
`ConstructUsing`/`ResolveUsing` никогда не создаёт unknown destination
occupancy, поскольку destination body принципиально непрозрачен и без error.

`MORPH0047` публикуется раньше `MORPH0048` по ID. Внутри ID порядок — ordinal
stable mapper identity, canonical pair и member symbol order: base-first,
затем declaration order, с deterministic interface order категории 10.
Primary type-argument location не используется как единственный sort key,
поэтому generic substitutions, inheritance fan-out, discovery order и
incremental invalidation порядок не меняют.

Suppression/severity override не меняет participation/uncertainty model и не
влияет на другие warning-и. Изменение effective setting, supported type
surface, constructor selection, callback body/class, member rule, source
discard, opaque handoff, reachability, override либо inheritance полностью
actualizes affected member observations при одном incremental driver-е.

### 6.103. Самостоятельная тестовая матрица категории 12

Unit-категория completeness diagnostics независимо фиксирует:

- exact descriptors `MORPH0047`–`MORPH0048`: ID, title, category, default
  severity, enabled/configurable flags, message formats и parameters;
- полную setting matrix `Default` inheritance, `None`, `Source`,
  `Destination`, `Strict` на pair/root/base/MSBuild levels, last-call-wins и
  manual-pair applicability без зависимости от settings tests;
- supported source universe: properties/fields, readonly, base/override/hiding,
  interface most-derived/ambiguity, assembly accessibility, generic
  substitutions и исключённые static/const/indexer/ref-return/fixed/
  unreadable/opaque cases;
- supported destination universe: setter/init/mutable field, constructor-only
  readable association, exact/unique ignore-case matching, conditional
  constructors, override/hiding/interface/accessibility и исключённые
  read-only proxy/unsupported/opaque cases;
- pair-wide participation across Create, Update without previous, Update with
  previous, replacement/reuse, conditional/switch branches, null policies и
  `MappingMode`, включая отсутствие path-duplicated warnings;
- source uses из conventions, `Auto`, explicit constructor/member expressions,
  conditions, locals, nested marker arguments, chained reads и deferred
  values; `nameof`/symbol-only и unrelated same-text members не считаются;
- inline `ConstructUsing`/`ResolveUsing` direct reads, whole-source opaque
  helper/receiver/return/capture и body-less runtime delegates; no false
  warnings после potential use и отсутствие inferred destination occupancy;
- compile-time source discard во всех structured families: exact semantic
  discard, direct property/field, multiple direct statements, retained exact/
  cross-pair inheritance и pair-wide suppression;
- отсутствие runtime evaluation receiver/getter у source discard, отсутствие
  влияния на conventions/destination warnings и обычный runtime read в
  `ConstructUsing`/`ResolveUsing`;
- control-flow/nested-block, chain/alias/indexer/conditional access/conversion/
  invocation/tuple, non-discard `_` symbol, unsupported member,
  previous/result receiver и deferred-lambda variants как `MORPH0031`, а не
  hidden suppression;
- destination occupancy каждым explicit/convention/`Auto`/`Value`/nested/
  member-level `Ignore` rule и фактически passed associated constructor
  argument;
- отсутствие occupancy у omitted optional/`params`, constructor `Ignore`,
  unassociated parameter, default construction, initializer, reuse,
  `[SetsRequiredMembers]`, get-only nested proxy и runtime result callback;
- `required`/`init` coexistence: category-10 error priority, valid Create-only
  occupancy и отсутствие ложного existing-Update warning;
- exact primary source/destination type-argument locations, member-declaration
  additional locations, metadata absence, generic alias/nullability syntax и
  stable fully-qualified message display;
- one warning per mapper/pair/member, separate hiding symbols, override dedup,
  inherited consumer ownership, source-before-destination publication и stable
  member ordering;
- full precedence/unknown-set matrix с representative categories 1–11,
  callback-wide и terminal-local suppression, compiler-owned binding errors и
  сохранение independent warnings;
- `UnmappedMemberValidation.None` и manual `Convert` как полный no-diagnostic
  boundary, inherited no-op и explicit manual setting category-6 error;
- suppression/`Warning`→`Error` override без stubs, selection либо generated
  artifact changes;
- exact complete generated outputs при каждом warning, включая unchanged
  mapper contracts и отсутствие diagnostic-driven reads/assignments;
- actualization setting, source/destination declaration surface, constructor,
  member rule, callback direct/opaque use, discard semantic shape,
  reachability, override и inheritance при одном сохранённом incremental
  driver-е.

Package-like integration-категория независимо проверяет:

- реальные `Source`, `Destination` и `Strict` warnings из C# и MSBuild
  settings с exact locations, а `None` реально оставляет compilation clean;
- suppressed `MORPH0047`/`MORPH0048`: Create/Update выполняются с теми же
  results, side effects и generated artifact set, warning-as-error не включает
  `MappingConfigurationException` recovery;
- compile-time source discard в `Construct`, `Resolve` и `Members` не вызывает
  throwing/side-effect getter, тогда как direct runtime discard в
  `ConstructUsing`/`ResolveUsing` вызывает его ровно один раз;
- pair-wide lifecycle: Create-only `init`, conditional rule, existing reuse и
  constructor replacement дают одну ожидаемую completeness модель во всех
  public operations;
- inline runtime source reads и whole-source helper реально выполняются, но
  object initializer/runtime mutation callback-а не скрывают destination
  warning;
- constructor argument, member-level/constructor-level `Ignore`, optional /
  `params`, `[SetsRequiredMembers]` и read-only nested Update proxy сохраняют
  заявленную occupancy в реальном consumer assembly;
- exact/cross-pair composition и local override удаляют uses/discards только
  вместе с discarded origin; retained consumer warning-и принадлежат current
  pair без origin duplication;
- representative suppressed category-8–11 error сохраняет свой throwing
  recovery и подавляет только derived completeness warning, independent
  warning остаётся;
- `.editorconfig`/MSBuild severity configuration обеих IDs меняет только
  compiler presentation, не runtime behavior.

## 7. Реализация и тесты

До первого vertical diagnostic slice production-model выравнивается с уже
зафиксированным нормативным contract-ом:

1. `IncludeBase` lookup действительно исключает current composition node, а
   exact same-pair импортирует полный effective plan, включая любую result
   policy либо `Convert`, а не только `Members`. Внутренний
   `CyclicIncludeBase` state удаляется: same-pair без ancestor принадлежит
   `MORPH0026`, reverse incompatible edge — `MORPH0027`, отдельной cycle
   diagnostic нет.
2. Builder discovery перестаёт обходить callback arguments всех шести
   families. Conditional/materialized delegate и `builder` capture целиком
   передаются категории 8 и не становятся `MORPH0017`/`MORPH0018`.
3. Compiler preflight различает source-owned warning и новую generated
   проблему. Первая сохраняется compiler-у с узким suppression generated
   duplicate; только вторая становится `MORPH0030`.
4. Внутренние boolean/string failure flags заменяются structured observations,
   достаточными для точного public contract: diagnostic reason, callback/
   setting/edge origin, offending symbol/node, primary/additional locations,
   source и target mapper, canonical pair и affected operations/path. В
   частности, convention/structured construction planners сохраняют strategy,
   candidate rejection, selected constructor, parameter-rule и terminal
   previous/null origins, а member planner — effective member identity,
   explicit/convention origin, required obligation, lifecycle/result
   dependency, hidden imported slot и terminal null-plan origin вместо
   неразличимых `null`/unsupported states. Nested planner дополнительно
   сохраняет marker producer/terminal target, inferred source/destination
   sides, result conversion, explicit/generated current destination,
   read-only proxy и adaptive-local use set вместо общего unsupported result.
   Completeness planner сохраняет supported source/destination universes,
   semantic/potential source uses, retained source discards, constructor/member
   occupancy и error-derived uncertainty set вместо повторного обхода
   generated syntax.
   Recovery строится из тех же observations, а не из повторного эвристического
   анализа.
5. Structured grammar распознаёт exact direct-body compile-time source discard
   `_ = source.Member;` до общего side-effect-statement rejection, переносит
   его как category-12 observation и удаляет из runtime lowering. Остальные
   discard/assignment shapes сохраняют category-8 unsupported ownership, а
   runtime callbacks остаются обычным C#.

Эти выравнивания не вводят новую diagnostic semantics и выполнены одним
предварительным coherent change с самостоятельными focused regression tests.

Статус на 11 августа 2026 года: все пять выравниваний реализованы и приняты
пользователем. Exact same-pair regression исполняет inherited
`Construct` / `Resolve` / `ConstructUsing` / `ResolveUsing` / `Members` /
`Convert` и три local-precedence формы. Отдельные consumer scenarios фиксируют
ownership callback arguments всех шести families, отсутствие runtime getter
read у structured source discard, обычный runtime read у `Using` callbacks и
то, что настоящий local symbol `_` discard-ом не считается. Focused
exact-source test сохраняет source-owned `CS0618` и подавляет только generated
duplicate. Focused model tests отдельно фиксируют полный constructor candidate
set, selected constructor, rejection reason и parameter-rule origin для
convention и explicit structured planning. Representative ранее принятые
inheritance и control-flow consumers повторно исполняются без изменения
semantics.

Этап 3 выполняется вертикальными срезами по согласованным категориям:
detection, diagnostic publication, locations, deduplication, recovery,
самостоятельные unit- и integration-тесты и соответствующая документация
входят в один coherent change.

Первый vertical slice категории 1 реализован. Runtime assembly публикует
contract revision `1`; generator проверяет effective C# version, единственность
runtime candidate, revision metadata и ordered structural manifest, публикует
точные `MORPH0001`–`MORPH0004` и fail-closed отключает все mapper pipelines
независимо от suppression либо severity override. Analyzer release tracking
фиксирует все четыре правила.

Самостоятельная unit-категория покрывает exact descriptor contract, language
aliases, missing/ambiguous/incompatible runtime, все классы manifest failure,
global gate, suppression/severity и actualization одного incremental driver-а.
Package-like integration-категория проверяет bundled package execution,
analyzer-only, mismatched/duplicate runtime, C# 8 и реальную analyzer config;
focused production-composition regression подтверждает неизменный normal
generated artifact set. После пользовательского ревью test-owned runtime
contract вынесен в отдельный fixture с именованными дефектами, а MSBuild и
package scenarios представлены обычными consumer-проектами вместо
динамической генерации project/source файлов. Срез принят; следующий —
категория 2.

Второй vertical slice категории 2 реализует configurable `MORPH0005`–
`MORPH0010` и `MORPH0034`, отдельную declaration-модель, mapper-wide
structural gates и pair-local исключение конфликтующих user-declared
contracts. Самостоятельные unit- и package-like integration-тесты фиксируют
точные diagnostics, recovery, suppression и incremental actualization. Срез
принят пользователем.

Третий vertical slice категории 3 реализует configurable `MORPH0011`–
`MORPH0014`: рекурсивную доступность mapping types, запрет root type
parameter-а, canonical duplicate registration и унификацию generated
`ITypeMapper<,>` contracts. Первая canonical registration остаётся
авторитетной; unavailable pair исключается, unsupported root сохраняет typed
exception-stub, а унифицируемые contracts не объявляются. Compiler-owned
invalid type arguments не получают дублирующую Morphant diagnostic.

Самостоятельная unit-категория фиксирует descriptors, canonical identity,
locations/additional locations, ordering, suppression/severity, incremental
actualization и полный recovery output. Обычные C# 9 consumers исполняют
suppressed unsupported, duplicate, unavailable, unification и opaque-root
scenarios; package/MSBuild fixtures проверяют file-local type и реальные
`.editorconfig` overrides. Срез реализован и ожидает пользовательского ревью;
следующий — категория 4.

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
