# План compile-time diagnostics Morphant v0

Дата составления: 7 августа 2026 года.

Статус: этап 1, таксономия diagnostics, ожидает ревью.

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
| 1 | Полная таксономия категорий и общие границы diagnostics | Ожидает ревью |
| 2 | Полный каталог и точный контракт каждой diagnostic по одной категории за раз | Заблокирован этапом 1 |
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

## 6. Контракт каталога diagnostics

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
