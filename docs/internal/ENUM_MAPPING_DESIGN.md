# First-class enum mapping: исследование и набросок дизайна

Дата: 2026-09-22. Статус: дизайн для обсуждения, не обещание реализации.
Редакция 15: пользователь утвердил привязку конвенции и Auto() к source
текущей пары независимо от выражения перед mapping switch. Switch над
result или кортежем не меняет вход конвенции. Вопрос перенесён в закрытые
решения; оставшиеся спорные вопросы — в разделе 10.
ConstructUsing и ResolveUsing можно сочетать с Members; сохраняются его
существующие формы с result и context. Construct и Resolve не генерируются.
Принятые замечания пользователя отделены от новых предложений ниже.
Названия нового API предварительные.
Исходная точка: Morphant 0.5.0, remote `main`
`b77d654d8d255bb89c04f9b9c69cbb1a56c7d7c5`.

## 1. Что дают другие инструменты

Проверены первичные источники; ниже описаны документированные подходы,
а не результаты сравнительного запуска библиотек. Последний столбец —
вывод для Morphant, а не утверждение об устройстве чужого инструмента.

| Инструмент | Подход | Полезная идея |
|---|---|---|
| [AutoMapper.Extensions.EnumMapping](https://docs.automapper.io/en/stable/Enum-Mapping.html) | Отдельное расширение: по числу по умолчанию, по имени опционально; `MapValue`, валидация и специальные правила обратного отображения. | Краткие типизированные overrides. Many-to-one mapping делает автоматический reverse неоднозначным. |
| [Mapster](https://github.com/MapsterMapper/Mapster/wiki/Data-types) | Enum-to-enum по числу по умолчанию; можно выбрать имя. Есть enum/string/numeric conversions и flags. | Полезна единая поддержка скалярных преобразований, но строковый parsing и перенос числа требуют собственных контрактов. |
| [Mapperly](https://mapperly.riok.app/docs/configuration/enum/) | Source generator: `ByValue` по умолчанию, `ByName`, `ByValueCheckDefined`; overrides, fallback, проверка покрытия source/target, строковые naming policies. | Семантика сопоставления, runtime-проверка и compile-time-полнота — разные решения. |
| [MapStruct, Java](https://mapstruct.org/documentation/stable/reference/html/#mapping-enum-types) | По имени; отсутствующее соответствие source — ошибка компиляции. `ANY_REMAINING` сохраняет конвенцию, `ANY_UNMAPPED` отключает её для неописанных случаев; есть преобразования имён. | Проверять эволюцию enum при компиляции. Различать fallback после конвенции и полностью explicit mapping. Java enum не моделирует произвольное C# enum-число. |
| [Chimney, Scala](https://chimney.readthedocs.io/en/stable/supported-transformations/#between-sealedenums) | Сопоставляет варианты sealed/enum по имени, поддерживает явное переименование и вычисляемую обработку варианта; различает total и partial transformations. | Сначала определить полный набор поддержанных входов. Result-based ошибки — отдельный API, не обязательная часть enum feature. Scala enum также может содержать данные. |
| [Serde, Rust](https://serde.rs/variant-attrs.html) | Раздельные имена для сериализации/десериализации; несколько входных `alias` для одного варианта. | Входных текстовых имён может быть несколько, выходное представление должно быть однозначным. |
| [System.Text.Json](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/customize-properties#enums-as-strings) | Числа по умолчанию; строковый converter, naming policy, `JsonStringEnumMemberName`. [Разрешение чисел](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonstringenumconverter-1.-ctor?view=net-10.0) задаётся отдельно. | Имя CLR, имя в протоколе и допустимость числового представления не должны смешиваться. |
| [Protocol Buffers](https://protobuf.dev/programming-guides/enum/) | Open enum сохраняет неизвестное число; closed enum обрабатывает его через unknown fields. | Сохранение неизвестных значений бывает намеренным требованием совместимости, а не ошибкой. Для него нужен явный режим. |
| [Enums.NET](https://github.com/TylerBrinkley/Enums.NET) | Утилиты для enum/flags; `PrimaryEnumMember` позволяет выбрать основное имя среди дубликатов значения. | Нужна явная канонизация aliases при enum-to-string; порядок объявления не выражает пользовательское намерение. |

Не следует копировать все возможности сразу. Самые полезные общие идеи:
типизированные соответствия, проверка полноты, явная политика неизвестного
значения, отдельная семантика flags и независимые направления преобразования.

## 2. Исходные ограничения Morphant

- Enum сейчас является opaque destination без generated construction/member
  surface: [DestinationCapabilityPolicy](../../src/Morphant.Generator/MappingPair/DestinationCapabilityPolicy.cs).
  Готового enum algorithm нет; [Convert](../api/convert.md) уже позволяет
  описать преобразование целиком. Сам факт допустимости enum как типа пары
  не означает наличия first-class mapping.
- [TypeMapperModelBuilder](../../src/Morphant.Generator/TypeMapperGeneration/TypeMapperModelBuilder.cs)
  различает manual conversion, result policies и object construction.
  Enum algorithm должен формировать скалярный результат, а не конструктор
  enum или фиктивные members.
- [Nested mapping](../nested-mapping.md) всегда явный. `Auto()` копирует
  совместимое значение и не запускает зарегистрированную пару автоматически.
  [MemberTypeCompatibility](../../src/Morphant.Generator/TypeMapperGeneration/MemberTypeCompatibility.cs)
  проверяет implicit C# conversion; между разными enum её нет.
- `IMapper`/`ITypeMapper` и `MappingContext` уже подходят для enum. `Update`
  возвращает итоговое значение; изменять переданный value type по месту нельзя.
- [Null handling](../settings/null-handling.md) уже определён. Null, нулевое
  значение enum и неизвестное ненулевое значение — разные входы.
- Сохраняются [setting precedence](../settings/README.md#precedence),
  полное владение алгоритмом у `Convert` и
  [generator contracts](GENERATOR_CONTRACTS.md): без runtime reflection,
  с простым generated code, изоляцией ошибок и корректной incrementality.

## 3. Что исправляет эта редакция

Пользователь выбрал направление на естественный C# вместо цепочек
`MapValue`. Он также уточнил: конвенция включена независимо от наличия
`Auto()` в пользовательском switch; отключает неявную конвенцию существующий
`MemberSelection.Explicit`. Достаточно описать отличающиеся случаи.

Используется существующий `Members` вместо нового `Values`. Для scalar enum
mapping не генерируются `Construct` и `Resolve`: отдельного структурного
создания результата перед выполнением его правил нет. `ConstructUsing` и
`ResolveUsing` сохраняются: обычный C# callback нужен и без самостоятельной
обработки null. Последнее уточнение снимает предложенный запрет их сочетания
с `Members`: фабрика может предоставить значение, а правила — скорректировать
его через существующий параметр `result`. Сам по себе scalar destination
не делает такую композицию бесполезной.
`[Flags]` можно распознать при генерации, разложить вход на биты и применить
к ним правила `Members`. Отдельный метод `Flags` для этого не нужен.

Требование к этой редакции: каждая новая настройка должна отвечать на вопрос,
на который не отвечают существующие настройки. Наличие отдельной внутренней
ветки алгоритма само по себе не обосновывает новый публичный переключатель.

Зафиксированы решения: library default ByName, сопоставление имён в два
этапа, конвенция перед завершающей веткой, её привязка к source текущей пары,
числовое строковое представление, сохранение числа,
контракт coverage, доступность `result`, применимость settings с Using и
границы обработки compiler warnings. Их канонические описания находятся
ниже; раздел 10 отделяет закрытые решения от оставшихся спорных вопросов.

Ранее набросок ошибочно делал наличие `Auto()` переключателем
автоматического mapping и объявлял switch без него полностью ручным.
Отсюда появились лишние `Auto(fallback: ...)`, отдельная настройка покрытия
и собственные defaults. Они удалены из предлагаемого API.

Пользователь также выбрал конвенцию перед завершающей веткой: последняя
обрабатывает остаток после явных правил и конвенции (раздел 5).

## 4. Существующие настройки — основа enum mapping

Проверены не только названия settings, но и
[resolver](../../src/Morphant.Generator/Settings/MappingSettings.cs),
[setting diagnostics](../../src/Morphant.Generator/Settings/MappingSettingsDiagnosticPipeline.cs),
[member-selection scenarios](../../src/tests/Morphant.Generator.IntegrationTests/TypeMapperMemberTests/MemberSelectionTests.cs)
и [inheritance scenarios](../../src/tests/Morphant.Generator.IntegrationTests/TypeMapperInheritanceTests/SettingsCompositionTests.cs).
Enum-поведение в правом столбце — предлагаемое расширение; feature ещё нет.
Конвенция и enum coverage относятся к декларативным правилам; тела Using
callbacks остаются обычным C#. Их композиция с `Members` описана в разделе 5.

| Настройка | Действующий контракт | Применение к enum |
|---|---|---|
| `MemberSelection` | `Auto` по умолчанию; explicit rules имеют приоритет; `Explicit` отключает только неявный подбор | Неописанные значения получают конвенцию при `Auto`. Явный `Auto()` работает и при `Explicit` |
| `UnmappedMemberValidation` | `None` по умолчанию; `Source`, `Destination`, `Strict`; предупреждения, не изменение mapping | Проверять enum-значения после композиции правил. Переиспользовать настройку, её default и управление severity |
| `MappingMode` | `CreateAndUpdate`; отключённая операция немедленно бросает исключение | Enum mapping соблюдает те же границы операций, включая Update без включённого Create |
| `NullSourceHandling` | `ReturnNull`; применяется раньше destination и expressions | Nullable enum/string source проходит общий guard. Ноль и неизвестное число не являются null |
| `NullDestinationHandling` | `Create`; применяется только в Update | Для nullable destination сохраняется текущий контракт; операция остаётся Update |
| `ConstructorSelection` | Для scalar destination inherited default игнорируется; явная pair-настройка, включая `Default`, даёт `MORPH0023` | Enum не получает конструктор и не меняет этот контракт |
| `Flattening` | Управляет вложенными source paths; не является naming policy | Корректное значение не влияет на enum-имена. Не ослаблять текущую диагностику неверного effective value у declarative mappings |
| `UnknownDerivedTypeHandling` | Относится к runtime-типу и `ForDerived` | Не относится к неназванному enum-числу. Для поддержанных scalar-пар нет derived-dispatch |

### Precedence и применимость

Для каждой применимой настройки независимо:

1. Current mapping.
2. Included mappings, nearest first.
3. Current mapper.
4. Connected base mappers, nearest first.
5. MSBuild property.
6. Morphant default.

Все included pair settings стоят выше mapper-level settings. `Default`
продолжает поиск, а не сбрасывает на library default. Последняя запись на
одном уровне побеждает; положение mapper-level вызова до/после `Map` ничего
не меняет. Base configuration участвует только через `base.Configure` и
`IncludeBase`. Общий контракт описан в [settings](../settings/README.md).

Пример: mapper-level `Explicit` не перебивает `Auto` включённой пары.
Чтобы отключить эту конвенцию, текущая пара задаёт `Explicit`. Если она
затем задаст `Default`, снова вступит в силу включённое `Auto`.

`Convert` сохраняет полное владение алгоритмом: inherited declarative
settings игнорируются, локальные несовместимые settings диагностируются.
`MappingMode` и `UnknownDerivedTypeHandling` остаются применимыми.
`Members` нельзя смешивать с локальным `Convert`. Сочетание с
`ConstructUsing`/`ResolveUsing` допустимо, включая scalar enum-пары;
их callbacks сохраняют общее null handling (раздел 5).
Нельзя приписать обычному `Convert` новую семантику неявных enum-веток.

### Проверка необходимости новых настроек

Для новой настройки нужны самостоятельный пользовательский вопрос и
сценарий, который не покрыт действующими настройками. Затем следует проверить,
не выражается ли нужное исключение обычным правилом `Members`. Возможность
написать весь алгоритм через `Convert` сама по себе не отменяет полезность
конвенций; новый переключатель должен управлять именно нужным общим выбором.
Не перегружать старую настройку несвязанным смыслом ради меньшего их числа.

После этой проверки остаётся одна новая настройка — `EnumMappingStrategy`.
Её вопрос: что использовать в конвенции — имя или число enum? Это относится
и к соответствию двух enum, и к представлению enum в строке.
`MemberSelection` определяет, применять ли неявную конвенцию;
`UnmappedMemberValidation` только диагностирует покрытие. Ни одна из них
не выбирает критерий соответствия.

Значения: `Default`, `ByName`, `ByValue`. `Default` продолжает обычный поиск
настройки. Утверждённый library default для разных enum и enum-to-string —
`ByName`. `ByValue` выбирается для пары, mapper или через MSBuild по общим
правилам precedence. [Исследование default](ENUM_MAPPING_DEFAULT_RESEARCH.md)
содержит обоснование решения и сценарии обеих стратегий. Для самой стратегии `ByName`
пользователь выбрал порядок, аналогичный сопоставлению
[параметров конструктора](../api/members.md#constructor-parameters):

1. Искать точное совпадение имени через `Ordinal`.
2. Только если точного совпадения нет, искать через `OrdinalIgnoreCase`.

Оба этапа не зависят от culture. Найденное точное совпадение завершает
поиск: отличающиеся регистром кандидаты его не меняют и не делают
неоднозначным. Это правило enum-конвенции; существующее сравнение имён
object/tuple members не меняется. Отдельная настройка ignore-case не вводится.
Неоднозначные совпадения второго этапа разобраны в разделе 7. Выбор критерия
по умолчанию и способ сравнения имён — разные решения.
Не добавлять сюда `Explicit`, `Flags`, fallback или варианты validation:
это не альтернативные критерии соответствия.

Стратегия применяется к enum-to-enum и enum-to-string. Для enum/integer
форма пары уже определяет числовой путь. Обратный string-to-enum пока
сохраняет конвенцию имён; расширение на числовой parsing — отдельное
предложение, не следствие поддержки numeric output. Применимость same-type
enum обсуждается вместе с identity ниже.

Используются обычные pair/mapper/MSBuild уровни и общий resolver, без
нового уровня named arguments на `Auto()`:

```csharp
builder.Map<WireCode, StoredCode>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValue);
```

Enum strategy defaults на mapper/assembly могут сосуществовать с object,
enum/integer, string-to-enum и `Convert`; на этих mappings они не используются.
Явную настройку на паре, к которой она принципиально неприменима, диагностировать
по общему контракту `MORPH0023`. Для enum-to-enum/enum-to-string наличие Using
не меняет применимость стратегии и не приравнивает mapping к `Convert`.
С `Members` она управляет конвенцией этих правил; без `Members` выбранное
фабрикой значение окончательно и стратегия его не преобразует.

Корректная явная настройка не становится ошибкой только из-за отсутствия
`Members` или достижимого автоматического пути. Она остаётся частью
конфигурации и может участвовать в `IncludeBase`. То же отсутствие `Members`
не вводит новых запретов на существующие declarative settings; сохраняются
их обычные проверки значений и применимости. Проверка coverage не анализирует
тело Using callback. Это уточнение снимает предложение считать scalar Using
без `Members` ещё одной Convert-моделью для диагностики settings.

`EnumValueValidation` снята с предлагаемого API. Она не является синонимом
`UnmappedMemberValidation`: runtime-допустимость числа и compile-time-покрытие
различаются. Однако отдельная настраиваемая runtime-политика пока не нужна:
известные соответствия обрабатывает конвенция, поведение без соответствия
задают выражения `Members` — fallback, throw или явное приведение числа.
Предлагаемые границы числовой конвенции и пример приведены в разделе 7.
Это сохраняет сценарии обработки неизвестных значений без второго setting.
Общий переключатель для всех числовых пар потребовал бы отдельного
обоснованного сценария; такой запрос пока не установлен.

Отдельных `UnmappedEnumValueValidation`, `UnknownEnumValueHandling`,
`FallbackValue`, нового enum-режима `Explicit` и `Auto(fallback: ...)` нет.
Flags определяются по атрибуту; aliases, переименования и запреты задаются
правилами. Для наследования используется `IncludeBase`, для обычного C# —
существующие Using callbacks либо `Convert` в зависимости от владения null
handling (раздел 5). Новые методы `Values`, `Flags`, `Inherited` не нужны.

## 5. Switch задаёт правила, Morphant дополняет их конвенцией

Используется существующий `Members`: он задаёт явные правила поверх
конвенции. Для object/tuple это правила свойств, полей и элементов; для enum
это правила значений. Форма callback определяется типами пары. Его
содержимое остаётся декларативным DSL и не вызывается как обычный runtime
delegate из `Configure`. Отдельный метод `Values` не вводится.

Минимальный сценарий не требует `Auto()` или завершающей ветки:

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status => status switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted,
        DomainStatus.Archived  => ApiStatus.Hidden
    });
```

При effective `MemberSelection.Auto` остальные значения сопоставляются по
конвенции. При `Explicit` этого дополнения нет. Отсутствие результата
означает mapping exception в обеих операциях: у scalar mapping нельзя
«не присвоить member» и всё же получить результат Create. Не возвращать
неявно ноль или previous. Для previous остаётся явное выражение callback.

### Минимальный API для enum-пары

Граница этого решения — поддержанные scalar пары enum/enum, enum/integer,
enum/string в обоих направлениях, включая nullable. Наличие enum на одной
стороне само по себе не отменяет object mapping: например, для enum-to-DTO
создание объекта через constructor/factory по-прежнему может быть полезно.
Состав API выбирается по виду mapping, а не только по наличию enum source.

| Метод | Решение для scalar enum mapping |
|---|---|
| `Members` | Генерировать typed overloads для декларативных правил значений |
| `Convert` | Сохранить обычный callback, владеющий полным алгоритмом и null handling |
| `Construct`, `Resolve` | Не генерировать: для scalar значения нет структурной construction DSL |
| `ConstructUsing`, `ResolveUsing` | Сохранить обычные callbacks после null handling; можно сочетать с `Members`, предоставляя ему начальный `result` |

`Map`, применимые settings и `IncludeBase` сохраняются. Автоматическая
обработка flags использует тот же `Members` (раздел 7); отдельного метода
для регистрации побитовых правил нет.

### Обычный C# с общим null handling

Using callbacks принимают inline lambda, method group или совместимый
delegate. Их тела остаются обычным C#: без дополнения switch конвенцией,
DSL-маркеров и побитовой обработки flags. В контекстных формах доступен
полный `MappingContext`, включая `Mapper` для вложенных вызовов.

Сохраняется действующее различие методов после прохождения null guards:

| Метод | Create | Update с имеющимся destination | Update с null destination при `NullDestinationHandling.Create` |
|---|---|---|---|
| `ConstructUsing` | Вызывает callback | Callback не вызывается; начальным result служит previous | Вызывает callback, операция остаётся Update |
| `ResolveUsing` | Вызывает callback с `previous = None` | Вызывает callback с исходным previous | Вызывает callback с `previous = None`, операция остаётся Update |

При null destination и `NullDestinationHandling.Throw` callback не вызывается.
`MappingMode` сохраняет обычные границы операций. Ноль, включая неназванный,
является имеющимся enum destination: не трактовать его как отсутствие previous.
Null source обрабатывается раньше обоих callbacks по `NullSourceHandling`.

Для ручного преобразования и при Create, и при Update подходит `ResolveUsing`:

```csharp
builder.Map<DomainStatus?, ApiStatus?>()
    .ResolveUsing((status, _) => ConvertStatus(status));
```

`status` здесь non-null `DomainStatus`; `ConvertStatus` — обычный метод.
При default `NullSourceHandling.ReturnNull` null source даёт null destination,
не вызывая метод. Пользователь не пишет собственный null guard.

Без `Members` выбранное значение окончательно. Конвенция enum и проверка
объявленности к нему не применяются; проверка покрытия DSL не анализирует
произвольный runtime callback. Для flags Using callback получает исходную
маску целиком и вызывается один раз, если lifecycle требует его вызова.

При наличии `Members` non-null результат становится его начальным `result`.
Null из callback окончателен: пропустить `Members`, не запускать null policies
повторно. Это действующий общий lifecycle, а не новое исключение для enum.
Сохраняются ограничения на несколько destination methods и на их сочетание
с `Convert`; запрет Using вместе с enum `Members` не вводится.

`Convert` нужен, когда пользователь также владеет null handling. Вызов
helper из `Members` остаётся возможен, но не заменяет ordinary callback с
method group, произвольным телом и полным runtime context.

Сейчас `Construct`/`Resolve` уже отсутствуют для enum destination, но
[PairConfigurationEmitter](../../src/Morphant.Generator/ConstructionSurface/PairConfiguration/PairConfigurationEmitter.cs)
выдаёт `ConstructUsing`/`ResolveUsing` для всех допустимых пар. Этот API
сохраняется; прежний план его удаления снят. Действующие контракты описаны в
[ConstructUsing](../api/construct-using.md) и [ResolveUsing](../api/resolve-using.md).
При реализации enum Members потребуется расширить композицию фабрики и
правил на scalar result, сохранив общее поведение остальных mapping kinds.

### Фабрика и последующая коррекция через `Members`

Например, существующий converter используется как основа, а конкретная пара
переопределяет обработку отмены с учётом полученного от него значения:

```csharp
builder.Map<DomainStatus?, ApiStatus?>()
    .MemberSelection(MemberSelection.Explicit)
    .ResolveUsing((status, _) => LegacyConvert(status))
    .Members((status, _, result) => status switch
    {
        DomainStatus.Cancelled when result == ApiStatus.Active
            => ApiStatus.Deleted,
        _ => result
    });
```

`LegacyConvert` вызывается после null guards, затем правило использует его
non-null результат. `_ => result` сохраняет это значение во всех остальных
случаях. `Explicit` здесь намеренен: нужна коррекция результата фабрики без
неявного сопоставления остальных source values. Можно также вернуть прямое
выражение, например `Normalize(result)`; оно явно задаёт результат для всех
входов и само по себе не требует отключать конвенцию.

Без `Explicit` применяются обычные правила раздела 5: наличие фабрики не
отключает конвенцию в `Members`. Завершающая ветка `_ => result`
сохраняет значение фабрики только для остатка после
явных правил и конвенции. Наличие начального `result` не делает его неявным
fallback неполного switch. `Auto()` также запрашивает соответствие source
по конвенции, а не означает «вернуть результат фабрики».

Порядок исполнения: общие guards, выбор начального результата по таблице
Using, проверка terminal null, правила `Members`, возврат итогового значения.
Для enum правило может заменить scalar целиком; это расширение `Members`
на значения, а не разрешение заменять произвольный object из фабрики.
На Update с имеющимся destination `ConstructUsing` пропускается, но `Members`
выполняется и видит этот destination как начальный `result`. `ResolveUsing`
выполняется на обеих операциях; `previous` при замене остаётся исходным
destination, а `result` — новым выбранным значением.

Не удалять вызов фабрики, если `Members` не читает `result` либо всегда
возвращает другое значение: сохраняются её эффекты, исключения и terminal
null. Не повторять фабрику для отдельных правил или битов. Совместное
использование допустимо и после `IncludeBase`; порядок fluent-вызовов
не задаёт порядок runtime-стадий. Flags требуют уточнений раздела 7.

### Входы `Members`

Сохраняются все четыре существующие формы:

- `source => rules`: non-null source после null policy.
- `(source, previous) => rules`: тот же source и `Option` исходного non-null
  destination; для Create и Update с null destination — `None`.
- `(source, previous, result) => rules`: дополнительно начальное non-null
  destination-значение, выбранное до выполнения правил.
- `(source, previous, result, context) => rules`: дополнительно существующий
  DSL context с `Operation`, без полного runtime `MappingContext`.

В побитовом flags-алгоритме `source` означает текущий бит того же source
enum, а `previous` остаётся исходной destination-маской. Смысл `result`
при этой композиции разобран в разделе 7.

Наличие параметра не создаёт значение. Фиксируется существующее правило
доступности `result`: читать только на пути, где начальный destination
уже выбран. Это non-null результат Using либо имеющийся destination на
Update без его замены. На Create без фабрики начального scalar нет; нельзя
вычислять конвенцию заранее ради его получения — она может бросить исключение
до явно обрабатывающей этот случай ветки. Не подставлять фиктивный ноль.

Чтение недоступного `result` диагностируется по достижимому пути с учётом
операции и наличия destination, аналогично существующим ограничениям
доступности результата. Неиспользуемый параметр сам по себе не ошибка:
для `context.Operation` достаточно формы `(_, _, _, context) => ...`.
Поэтому отдельный overload с третьим `context` не нужен. Семантика закрыта;
интеграция проверки доступности с enum planner остаётся задачей реализации.

### Конвенция и выражение перед switch

Выражение перед mapping switch выбирает явную ветку. Неявная конвенция и
`Auto()` всегда сопоставляют `source` текущей зарегистрированной пары с её
destination по одной и той же effective стратегии. Они не берут вход из
выражения перед switch и не создают другую mapping pair. В побитовом
flags-to-flags mapping этим source остаётся текущий бит по контракту выше.

| Выражение перед switch | Что проверяют явные patterns | Вход конвенции и `Auto()` |
|---|---|---|
| `source` | Исходное значение правил | `source` текущей пары |
| `result` | Начальное destination-значение | Тот же `source`, без destination-to-destination mapping |
| `(source, result)` или `(result, source)` | Комбинацию значений в указанном порядке | Тот же `source`, без выбора элемента кортежа или tuple mapping |
| `Normalize(source)` | Результат пользовательского вычисления | Тот же `source`, без подмены нормализованным значением |

Например, source — `DomainStatus.Active`, фабрика вернула `ApiStatus.Pending`,
а конвенция `ByName` даёт `ApiStatus.Active`. Если явные специальные ветки
`result switch` не выбраны, завершающее `_ => Auto()` возвращает `Active`.
Начальный `result` остаётся `Pending`; конвенция не вычисляет его заранее
и не запускает фабрику повторно. При `_ => result` результат в режиме `Auto`
тоже будет `Active`: завершающая ветка идёт после конвенции. В режиме
`Explicit` та же ветка сохранит `Pending`, а явно написанный `Auto()`
по-прежнему запросит соответствие source.

Таким образом, перенос условия из guard над `source` в pattern над
`(source, result)` не меняет вход конвенции. Форма выражения перед switch
не требует отдельной настройки и не ограничивает `Auto()`.

### Приоритет завершающей ветки

Для enum mapping switch независимо от выражения перед ним зафиксирован порядок:

1. Явные специальные ветки в написанном порядке.
2. Неявная конвенция, если effective selection — `Auto`.
3. Завершающая ветка; при её отсутствии — mapping exception.

Слияние local и inherited rules через `IncludeBase` остаётся отдельным
вопросом раздела 8. Принятый приоритет не утверждает порядок вычислений
между этими уровнями.

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status => status switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted,
        DomainStatus.Corrupt => throw new InvalidOperationException(),
        _ => ApiStatus.Unknown
    });
```

Допустим, обе стороны содержат `Active`, а для source `Legacy` соответствия
нет. Для примера без `IncludeBase`:

| Вход | `MemberSelection.Auto` | `MemberSelection.Explicit` |
|---|---|---|
| `Cancelled` | `Deleted` | `Deleted` |
| `Corrupt` | Пользовательское исключение | Пользовательское исключение |
| `Active` | `Active` по конвенции | `Unknown` |
| `Legacy` | `Unknown` | `Unknown` |
| Неназванное число при `ByName` | `Unknown` | `Unknown` |

`_ => Unknown` задаёт fallback после конвенции. Вычисляемое
`_ => ResolveUnknown(status)` выполняется только для этого остатка,
один раз; дополнительный lazy API не требуется.

При `MemberSelection.Explicit` этап неявной конвенции пропускается:
завершающая ветка обрабатывает всё, что осталось после явных правил.
Наличие этой ветки само по себе не меняет effective selection.

### `Auto()` остаётся явным запросом

Существующий смысл `Auto()` сохраняется: явно применить конвенцию для
source выбранного случая, даже при `MemberSelection.Explicit`. Вход
определяется текущей парой, как описано выше, а не выражением перед switch.

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .MemberSelection(MemberSelection.Explicit)
    .Members(status => status switch
    {
        DomainStatus.Active => Auto(),
        DomainStatus.Cancelled => ApiStatus.Deleted,
        _ => ApiStatus.Unknown
    });
```

Только `Active` запрашивает конвенцию. Неудача этого явного запроса бросает
mapping exception и не проваливается в следующую ветку. `_ => Auto()`
явно запрашивает конвенцию для всего остатка при любом `MemberSelection`;
без соответствия — исключение. В режиме `Auto` такая завершающая ветка
обычно избыточна, но её существование не определяет режим mapping.
Не выполнять конвенцию дважды: завершающий bare `Auto()` и неявное
дополнение образуют один автоматический путь. Особенно важно не повторять
per-bit callbacks и их эффекты после уже полученной неудачи.

### Граница с обычным C#

Дополняется switch, которым `Members` непосредственно задаёт декларативные
enum-правила, независимо от выражения перед ним.
Вложенный switch справа от `=>`, чужой метод и callbacks `Convert`/Using
сохраняют обычную C# семантику и не получают конвенционные ветки.
`Members(_ => Auto())` — явная конвенция для обрабатываемых значений;
прямое выражение
`Members(source => Compute(source))` — явный результат для текущего
обрабатываемого значения. В обычном enum mapping это весь вход, в
побитовом flags-to-flags mapping — текущий бит. В этом побитовом режиме
полный ручной алгоритм над маской задаётся обычным callback из раздела 5.

Финальный `_ => expression` или `var remaining => expression` без guard
трактуется как завершающее правило. Ветка с `when`, в том
числе `_ when condition`, остаётся явным условным правилом до конвенции.
`or` объединяет случаи; `SomeValue => throw ...` явно запрещает случай.
Другие patterns сохраняют свой порядок; не переупорядочивать их по именам
и не выполнять guards/результаты заранее.

Это сознательное расширение **в точке дополнения декларации**. Нельзя
обещать одновременно такую конвенцию перед `_` и буквально неизменённую
семантику всего исходного switch. [Обычный C#](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/switch-expression)
выбирает первую совпавшую ветку и сам не добавляет соответствия.

Если guard для `Active` вернул false, поиск продолжается; достижимая
конвенция может сопоставить `Active`. Пользовательские исключения не
являются отсутствием соответствия и не перехватываются fallback.
Сохраняются scopes, locals, комментарии и независимые вычисления.
Выражение перед switch вычисляется один раз на своём месте. Его значение
для выбора веток и source для конвенции — разные входы. Если guard меняет
переменную source, конвенция и `Auto()` сохраняют исходный source этих правил,
а не читают изменённую переменную или значение выражения перед switch.
В побитовой обработке это текущий source-бит.
Обычные выражения по-прежнему видят пользовательские изменения переменных.

Block lambda с подготовкой локальных значений и возвращаемым mapping switch
может следовать существующим declarative statement boundaries. Не
дополнять все встреченные switch механически и не анализировать тела
пользовательских методов. Method group и полный imperative algorithm
задаются Using callback либо `Convert` по контракту раздела 5.

## 6. Проверка покрытия через `UnmappedMemberValidation`

Контракт проверки покрытия зафиксирован: используется существующая настройка
с её default `None` и предупреждениями, без нового enum setting. Проверяется
итоговая композиция явных правил, наследования, конвенции и завершающей ветки.
Coverage анализирует полученное поведение, а не устанавливает собственный
порядок; детали слияния через `IncludeBase` ещё обсуждаются. Режим выбора
правил и режим диагностики независимы:

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .MemberSelection(MemberSelection.Auto)
    .UnmappedMemberValidation(UnmappedMemberValidation.Source)
    .Members(status => status switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted
    });
```

Новый объявленный source без соответствия даёт warning. Без этой настройки
остаётся общий default `None`, но runtime-исключение на непокрытом входе не
исчезает. Проект может повышать severity обычными средствами diagnostics;
не вводить для enum скрытый default `Source` с error.

- `Source`: объявленные физические source-значения должны быть обработаны
  явным результатом, явным запретом или успешной конвенцией. Алиасы одного
  числа составляют одну runtime-группу.
- `Destination`: объявленные destination-значения проверяются на участие
  в результирующем mapping. Many-to-one не является конфликтом само по себе.
- `Strict`: обе стороны. `None`: без проверки покрытия, но без отключения
  диагностики некорректной конфигурации или проверки диапазона чисел.

Завершающее `_ => Unknown` намеренно обрабатывает остаток и закрывает source
coverage. `_ => throw ...` намеренно запрещает его. `_ => Auto()` обещает
найти соответствие; отсутствующее соответствие объявленного source остаётся
непокрытым. Для контроля новых enum values выбирать вариант без blanket
fallback, а не переопределять смысл `UnmappedMemberValidation`.

Guard, результат которого нельзя доказать статически, не доказывает покрытие
целого значения: нужен также путь с false.
Для динамического результата известен факт source-обработки, но часто
неизвестен набор destination-значений. Если запрошенную проверку нельзя
завершить из-за такого выражения, выдаётся предупреждение о границе анализа,
а не утверждение, что конкретный результат невозможен. Не превращать
анализ в интерпретатор C# и не исполнять методы при генерации.

При композиции с Using анализируются правила `Members`, но не тело фабрики.
Сам факт её наличия не доказывает полноту последующих правил. `_ => result`
явно обрабатывает остаток source; множество возвращаемых фабрикой destination
values обычно неизвестно. Не считать их отсутствующими только потому,
что они не записаны константами в `Members`.

Для string/integer проверяется только конечная enum-сторона. Неназванные
enum-числа относятся к runtime mapping, а не к объявленному source coverage.
Для побитового flags-to-flags mapping проверка опирается на атомарные
правила. Объявленные composites не требуют отдельной ветки, если они
выводятся из отображения своих битов.
Явные whole-value исключения требуют правил раздела 7; не перечислять все
комбинации маски ради анализа покрытия.

## 7. Flags, aliases, строки и числа

### Flags: автоматическое разбиение и единые правила Members

Отдельный `.Flags(...)` исключён из предлагаемого API. Для enum-to-enum
генератор определяет flags-семантику по `System.FlagsAttribute` у типов,
включая типы из metadata и nullable underlying enum. Это compile-time
анализ Roslyn; runtime reflection и вызов `Enum.GetValues` не нужны.
Не угадывать flags по набору чисел `1, 2, 4` без атрибута.

Основной случай — обе стороны `[Flags]`. Например:

```csharp
[Flags]
enum SourceAccess
{
    None = 0, Read = 1, Write = 2, Delete = 4, Audit = 8
}

[Flags]
enum TargetAccess
{
    None = 0, View = 16, Edit = 32, Audit = 64, Unknown = 128
}

// В Configure:
builder.Map<SourceAccess, TargetAccess>()
    .Members(flag => flag switch
    {
        SourceAccess.Read => TargetAccess.View,
        SourceAccess.Write => TargetAccess.Edit,
        SourceAccess.Delete => TargetAccess.None
    });
```

Алгоритм состоит из трёх шагов: разложить маску на установленные биты,
получить результат каждого бита по правилам `Members` и объединить
результаты через OR. Результат одного бита может быть нулём, одним битом
или destination-маской. Не требуется задавать правило для каждой комбинации.

| Вход примера | Результат при ByName/Auto |
|---|---|
| `Read` | `View` по явному правилу |
| `Read \| Write` | `View \| Edit` |
| `Read \| Delete` | `View`; удаление Delete задано явно |
| `Read \| Audit` | `View \| Audit`; Audit сопоставлен по имени |

`MemberSelection.Explicit` отключает неявный подбор соответствий, но не
разбиение и объединение маски. В этом режиме `Read | Write` по-прежнему
переводится в `View | Edit`, а непокрытый `Audit` не получает автоматического
соответствия. Явный `Auto()` запрашивает конвенцию для текущего бита даже
при `Explicit`. Наследуемые правила `IncludeBase` участвуют так же, как
остальные правила `Members`.

Смысл `EnumMappingStrategy` сохраняется: `ByName` ищет имя текущего бита,
`ByValue` задаёт числовое соответствие. Явные правила действуют в обеих
стратегиях. Прежний вариант «ByValue обходит все побитовые overrides» снят:
стратегия конвенции не должна отключать написанные `Members` rules. Для
`ByValue` остаются вопросы диапазона и границы успешного соответствия,
описанные ниже; этот раздел не объявляет их решёнными одним приведением типов.

### Композиция flags-правил с фабрикой

Разрешение Using вместе с `Members` не меняет автоматически вход побитовых
правил: разбирается исходная source-маска. Фабрика до этого получает её целиком
один раз. Для `result` предлагается сохранить общий смысл начального
destination: все правила видят одну полную маску, выбранную фабрикой либо
взятую из имеющегося destination при пропуске `ConstructUsing` на Update.
Это позволяет, например, учитывать результат фабрики в guard отдельного
битового правила. `previous` сохраняет исходную destination-маску даже
после замены через `ResolveUsing`.

`result` не должен незаметно становиться накопителем уже обработанных битов.
OR объединяет результаты правил; автоматически добавлять к ним всю маску
фабрики нельзя — тогда явное правило не смогло бы удалить её биты. Фабрика
не вызывается внутри побитового алгоритма, а её null завершает операцию до
него. Эта детализация композиции — предложение, требующее проверки вместе
с zero/composite overrides ниже.

Using вместе с `Members` не вводит отдельную стадию `Members` над полной
маской: побитовая семантика едина с mapping без фабрики. Поэтому выражение
`Normalize(result)` в таком `Members` будет правилом для каждого бита,
а не однократной обработкой всей маски. Полный алгоритм над маской можно
оставить в обычном Using callback. Не выводить другой режим из самого
наличия фабрики или чтения `result` и не добавлять для этого новую настройку.

### Вычисления, fallback и неизвестные биты

В обычном побитовом пути предлагается такой контракт:

- Правила применяются один раз к каждому установленному биту, в порядке
  возрастания позиции бита. `source` внутри `Members` — текущий бит;
  `previous` — исходная destination-маска, не накопленный результат.
  Context и null handling относятся ко всей операции и не запускаются заново.
- `when`, вычисляемые правые части и block locals выполняются в своей
  позиции для текущего бита. Не запускать callback предварительно на всей
  маске «на пробу», а затем повторно на битах. Это добавило бы непрошеные
  вызовы и побочные эффекты.
- Числовые aliases одного бита не вызывают повторного вычисления. Старший
  бит signed-типа также обрабатывается один раз; выделять его в точной
  ширине source underlying type, без sign extension до произвольного ulong.
- Неизвестный source-бит тоже должен получить правило. Явный case
  `(SourceAccess)16 => ...` может обработать его. `ByName` без имени не
  находит соответствия; дальнейшее поведение определяется fallback/throw.
  Не отбрасывать такой бит и не отказывать до проверки явного правила.
- Завершающий fallback относится к текущему биту. Если к примеру выше
  добавить `_ => TargetAccess.Unknown`, вход `Read | (SourceAccess)16`
  получит `View | Unknown`. `_ => TargetAccess.None` намеренно удаляет
  непокрытые биты. Это предлагаемая семантика единого per-bit Members,
  **не** прежний whole-mask fallback отдельного Values callback.
- Без fallback непокрытый бит приводит к исключению всей операции;
  накопленная часть не возвращается. Уже выполненные пользовательские
  вычисления не откатываются. Явные throw и исключения из guards/expressions
  не перехватываются как отсутствие конвенционного соответствия.

Fallback всего результата в `Unknown`, если хотя бы один бит не удалось
перевести, имеет другой смысл. Он не должен неявно приписываться той же
per-bit завершающей ветке. Такой алгоритм можно выразить через `ResolveUsing`
с общим null handling либо через `Convert`;
потребность в отдельном декларативном варианте пока не установлена.

### Zero и составные значения: детали для согласования

Для нуля естественный default — ноль: OR пустого набора результатов.
Явное правило `None => ...` или `(SourceAccess)0 => ...` должно позволять
задать другое значение. Ноль не является битом и не должен добавляться
к каждой ненулевой маске. Точное взаимодействие zero override, guarded
rules, fallback и `Explicit` нужно закрепить вместе с composite rules.

Обычное объявление `ReadWrite = Read | Write` не добавляет новый бит:
такое значение раскладывается как Read и Write. Его имя и наличие поля
не должны менять результат по сравнению с той же безымянной комбинацией.

Отдельный вопрос — явно написанное правило для composite:

```csharp
SourceAccess.Read | SourceAccess.Write => TargetAccess.Special
```

Для сохранения whole-value overrides предлагается применять такое правило
при **точном совпадении всей исходной маски**, перед обычной декомпозицией.
На `Read | Write | Audit` оно не срабатывает как правило для подмаски.
Не разбирать маску жадно на overlapping composites: это даёт неоднозначный
порядок и может подавить обычные правила отдельных битов.

Этот приоритет пока только предложение. Нельзя реализовать его повторным
запуском всего `Members` на маске и на битах. До реализации нужно определить
правила для `or`, смешивающего atomic/composite constants, широких patterns,
`when`, locals и вычисляемых результатов. Перестановка либо удвоение
пользовательских вычислений не является приемлемым способом разделить
случаи. Если конкретную форму нельзя корректно разделить, нужна понятная
граница DSL с обычным callback, а не молчаливое игнорирование composite arm.
Это вопрос семантики единого `Members`, а не основание вернуть метод Flags.

`All = -1` само по себе не разрешает любые неизвестные биты и не является
атомом. При декомпозиции оно содержит все биты исходной ширины; для каждого
нужно соответствие. Возможное exact whole-value правило для All относится
к обсуждаемому приоритету composite overrides.

### Границы автоматической flags-семантики

- Автоматическое OR-объединение enum-результатов предполагает flags на обеих
  сторонах. При атрибуте только с одной стороны не угадывать, что обычный
  enum тоже является маской; прежняя граница ByName остаётся: явное полное
  соответствие, осознанный числовой путь либо ручной callback. Детали mixed pair
  applicability нужно согласовать до реализации.
- Enum/integer сохраняет числовую семантику. Flags-to-string с `ByValue`
  форматирует число всей маски, как описано ниже. Автоматическое текстовое
  представление flags через список имён и его parsing остаются за границей.
  Сам атрибут source не разрешает OR строк или object results.
- Для `ByValue` нельзя считать unsigned bit pattern математическим числом
  signed-типа. Например, sbyte high bit — это значение -128. Связь per-bit
  overrides, checked range и переноса неназванных чисел должна быть явной.
- Нельзя проверять каждый промежуточный destination-бит через
  `Enum.IsDefined`: при destination с одним `Pair = 3` итог `3` объявлен,
  хотя `1` и `2` по отдельности — нет. Успех конвенции ByValue для такой
  комбинации, смеси explicit/automatic contributions и его отношение к
  fallback остаются конкретным открытым решением. Это граница алгоритма,
  а не основание вводить настраиваемую validation. Сохранить возможность
  допустимого declared composite и не вводить скрытую проверку явно
  написанных результатов.

### Aliases и строки

```csharp
enum SourceState { Ready = 1, Active = 1 }
enum TargetState { Ready = 10, Active = 20 }
```

Source aliases runtime-неразличимы. На автоматическом пути ByName этот пример
неоднозначен; нужна диагностика. Явная ветка для числа `1` решает конфликт
для обоих имён. Не обходить обычные C# ошибки повторных/недостижимых веток.

Каждое source-имя сопоставляется в порядке `Ordinal`, затем при отсутствии
точного совпадения — `OrdinalIgnoreCase`. Результаты сопоставления разных
source aliases одного числа должны согласовываться: runtime не сохраняет,
какое из этих имён использовал вызывающий код.

Если destination содержит `Ready = 10` и `READY = 20`:

| Имя source | Результат конвенции |
|---|---|
| `Ready` | `Ready` (`10`), точное совпадение |
| `READY` | `READY` (`20`), точное совпадение |
| `ready` | Нет точного совпадения; второй этап неоднозначен |

При неоднозначности нужен явный результат в `Members`; порядок объявления
не определяет выбор. Если все подходящие имена destination на втором этапе
обозначают одно число, enum-результат однозначен; aliases этого числа не
создают конфликт.

Для enum-to-string с ByName canonical output задаётся явно при нескольких
именах одного числа; порядок объявления не выражает намерение.
Строковый результат сохраняет написание выбранного CLR-имени, включая
регистр. Два этапа поиска управляют сравнением, а не форматированием:
`Active` даёт `"Active"`, не `"active"` или `"ACTIVE"`. Два source aliases
одного числа, различающиеся только регистром, всё ещё задают разные выходные
строки и требуют явного выбора.
[Enum.GetName](https://learn.microsoft.com/en-us/dotnet/api/system.enum.getname?view=net-10.0)
не гарантирует выбор конкретного alias.

```csharp
builder.Map<ApiStatus, string>()
    .Members(status => status switch
    {
        ApiStatus.Deleted => "removed"
    });

builder.Map<string, ApiStatus>()
    .Members(text => text switch
    {
        "removed" or "deleted" => ApiStatus.Deleted,
        "pending" or "queued" => ApiStatus.Pending,
        _ => ApiStatus.Unknown
    });
```

При ByName остальные CLR-имена сопоставляются автоматически при `Auto`;
направления независимы. Конвенция CLR-имён в string-to-enum также сначала
ищет точное совпадение, затем при его отсутствии — без учёта регистра.
Например, `"active"` выбирает `active`, если такое имя объявлено, и может
выбрать `Active` только на втором этапе. Применимость самой
`EnumMappingStrategy` этим не расширяется. Явные строковые patterns и guards
в `Members` сохраняют обычную C# семантику: правило двух этапов применяется
к конвенции, а не переписывает пользовательские выражения.
Wire attributes и naming policies не вводятся в первую версию.
В string-to-enum пустая, числовая строка и пробелы не получают особого смысла.
Автоматическое форматирование и parsing списка flags-имён пока за границей;
ручной код остаётся возможен.

### Enum-to-string: имя или число

Одна `EnumMappingStrategy` выбирает представление. Для `Active = 2`:

| Стратегия | Результат конвенции |
|---|---|
| `ByName` | `"Active"` |
| `ByValue` | `"2"` |

```csharp
builder.Map<ApiStatus, string>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValue)
    .Members(status => status switch
    {
        ApiStatus.Deleted => "removed"
    });
```

Явное правило сохраняет приоритет; остальные значения получают числовую
строку при `MemberSelection.Auto`. При `Explicit` неявного форматирования
нет; явный `Auto()` запрашивает выбранную конвенцию. Null проходит обычные
guards. Стратегия использует общие уровни наследования и `Default`.

Для ByValue зафиксирована десятичная запись underlying integer в
`InvariantCulture`, без разделителей групп. Сохраняются его точная ширина
и знаковость: signed `-1` даёт `"-1"`, `ulong` не сужается до `long`.
Неназванное значение также имеет числовое представление: `(ApiStatus)123`
даёт `"123"`. Алиасы одного числа дают одну строку и не требуют выбора имени.
У ByName неназванное значение остаётся без соответствия и следует обычному
fallback/throw; не подменять эту конвенцию вызовом `Enum.ToString()` с
неявным переходом к числу.

Для flags с ByValue форматируется вся маска: при `Read = 1`, `Write = 2`
вход `Read | Write` даёт `"3"`. `Members` получает полную маску один раз;
побитовой обработки и объединения строк нет. Неизвестные биты сохраняются
в её числовом представлении. Это не расширяет отложенный formatter имён.

Отдельные format/culture/unknown-value настройки не добавляются. Специальный
формат можно задать выражением `Members` или обычным callback.
Симметричный `string -> enum` с ByValue также можно обсудить в рамках той же
настройки, но до расширения требуется выбрать грамматику parsing, overflow
и поведение неназванного destination-числа; сейчас это не принятый контракт.

### Числовая конвенция

Поддерживаемые целые типы: `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`,
`long`, `ulong`. Для enum-to-enum числовой путь выбирается через `ByValue`;
для enum/integer он определяется типами пары. Этот раздел описывает числовые
преобразования с enum/integer destination; форматирование в string рассмотрено
выше. Зафиксировано сохранение математического числа с учётом диапазона
destination underlying type независимо от checked options consumer.
Не усекать автоматически старшие биты и не менять знак через промежуточное
приведение: `ulong` не сужается до `long`, а `256` не превращается в byte `0`.

Реакция на выход из диапазона остаётся спорной: немедленное исключение о
переполнении либо отсутствие конвенционного соответствия с возможностью
fallback. Прежний вариант с немедленным исключением остаётся предложением.
Он не следует автоматически из требования сохранять число. Пользовательские
выражения сохраняют свои явные `checked`/`unchecked` и не получают скрытой
проверки объявленности результата.

В предлагаемом минимальном варианте конвенция с ordinary enum destination
находит объявленное destination-значение с тем же числом. Source может быть
неназванным, если в destination соответствие есть. Без соответствия работают
обычные правила fallback/throw раздела 5; настройка для включения или
отключения проверки не вводится. Enum-to-integer сохраняет и неназванные
значения в пределах диапазона: у integer нет перечня объявленных вариантов.

Сохранение неизвестного числа выражается явным C#, например для ordinary enum:

```csharp
builder.Map<WireCode, StoredCode>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValue)
    .Members(code => code switch
    {
        WireCode.Legacy => StoredCode.Replacement,
        _ => checked((StoredCode)code)
    });
```

По приоритету раздела 5 конвенция обрабатывает оставшиеся
известные соответствия, завершающая ветка — неизвестные. Явное приведение
не получает скрытой enum validation; checked/unchecked в пользовательском
выражении сохраняется. Для полного переноса исходной flags-маски доступен
обычный callback: нельзя объявлять cast каждого бита эквивалентом whole-mask cast
при разных ширинах и знаковости underlying types.

Прежний кандидат для допустимых flags-результатов — zero, точно объявленное
значение и комбинации объявленных single-bit значений. Одно `Pair = 3` не
разрешает `1` и `2`.
[Enum.IsDefined](https://learn.microsoft.com/en-us/dotnet/api/system.enum.isdefined?view=net-10.0)
не эквивалентен проверке произвольной допустимой flags-комбинации.
Взаимодействие этого кандидата с побитовой композицией ByValue пока открыто
и разобрано выше. Удаление настройки не решает алгоритмический вопрос;
не считать per-bit validation готовым решением.

## 8. Lifecycle, inheritance и вложенное использование

`Members` получает non-null source после общей null policy. Nullable
registration остаётся точной: не искать автоматически underlying pair.
`ReturnNull` при non-nullable enum destination по текущему контракту даёт
`default`, даже если ноль не объявлен. Это путь null policy, не конвенции.
Возврат null из правила допустим по nullable destination contract.

```csharp
builder.Map<DomainStatus?, ApiStatus?>()
    .Members(status => status switch
    {
        DomainStatus.Missing => null,
        DomainStatus.Cancelled => ApiStatus.Deleted
    });
```

В побитовом enum-to-enum алгоритме вход правил определяется текущим битом,
как описано в разделе 7; исходная маска проходит null guards один раз.

`Create`/`Update` вычисляют scalar result после обычных guards. `previous`
содержит исходный non-null destination. При Using начальное значение
выбирается до `Members` по lifecycle раздела 5. Для обычного enum и
enum-to-string правило возвращает итог операции; при flags-to-flags результаты
побитовых правил объединяются.
Смысл nullable результата отдельного бита ещё нужно определить: `null`
не является нулевой маской и не может молча участвовать в OR.
Контекст операции и формы callback описаны в
разделе 5; null destination не превращает Update в Create.

Для `E -> E` identity предлагается как автоматический путь, а не обход
настроек: explicit overrides выполняются первыми, `Explicit` отключает
неявный identity. Допустимость неназванных чисел на same-type automatic
пути ещё требует выбора как часть контракта конвенции; прежнее обещание
безусловного identity до настроек снято.

### `IncludeBase`

Настройки наследуются уже описанным способом. Для правил рекомендуется
развить существующий принцип: local explicit cases приоритетнее included,
непереопределённые included cases сохраняются. `Explicit` не отменяет
наследованные explicit rules. Сам факт нового `Members` не должен стирать
весь набор inherited overrides.

Для same-pair декларативных switches предлагается цепочка: local special
rules, nearest included special rules, конвенция, nearest configured
завершающая ветка. Локальная завершающая ветка заменяет inherited fallback,
а не удаляет inherited special cases. Неудавшийся guard продолжает поиск;
совпавший result/throw/явный `Auto` завершает его.

Это пока предложение для enum-правил, а не уже существующая реализация
слияния switches. Порядок пользовательских branches внутри каждого уровня
и scopes его locals должны сохраняться. Не собирать guards в отсортированную
таблицу и не запускать вычисления included callback до необходимости.
Произвольный прямой callback с результатом для всех входов остаётся полным
explicit правилом и может закрыть дальнейший поиск.

Новый `Inherited()` из предыдущей редакции снят с предлагаемого API:
сначала нужно проверить композицию через уже существующий `IncludeBase`.
Не превращать inherited `Convert`/factory в набор enum rules. Using callbacks
могут наследоваться для точной пары по действующему контракту и сочетаться
с `Members`. После композиции проверяются доступность `result`, ограничения
на несколько destination methods и `Convert`; отдельного конфликта
Using + Members нет. Сохраняются текущие ограничения доступности
helper-методов и cross-assembly inheritance.

### Nested mapping

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status => status switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted
    });

builder.Map<Order, OrderDto>()
    .Members((source, _) => new()
    {
        Status = Map(source.Status)
    });
```

Расширение `MemberSelection` внутри enum-пары не меняет контракт object
mapping. `Auto()` внутри object/tuple `Members` требует implicit C#
conversion и не запускает вложенную пару. Same-enum property может
копироваться как прежде; разные
enum требуют `Map`/`Create`/`Update`. `IMapper`, DI и get-only value members
не получают специальных обходных путей.

## 9. Проверенная реализуемость и границы проверки

### Неполный switch и компилятор

Обычный C# диагностирует неполный switch **в самом Configure**. Добавить
полный switch только в generated mapper недостаточно. Требование всегда
писать `_ => Auto()` противоречило бы принятому сценарию короткой декларации.

Временный compiler probe выполнен с Roslyn 4.4.0 (текущий minimum проекта),
`LanguageVersion.CSharp9`, nullable и warnings-as-errors. Он подтвердил:

- Неполный enum switch даёт `CS8509`; покрытие только объявленных имён может
  дать `CS8524` из-за неназванных чисел. Проверен также `CS8846` при
  завершающей ветке с guard.
- Узкий `DiagnosticSuppressor` может подавить эти предупреждения у выбранного
  декларативного switch, в том числе когда warnings повышены до errors.
- Такое же предупреждение в обычном методе и во вложенном result-expression
  остаётся. `CS8510` для недостижимой ветки и ошибки неподходящего типа
  результата остаются.

[Roslyn API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.diagnostics.diagnosticsuppressor?view=roslyn-dotnet-4.4.0)
разрешает программное подавление подходящих compiler warnings. Проба
подтверждает механизм, но не готовую интеграцию с Morphant, MSBuild или IDE.
Промежуточный запуск harness потребовал исключить `CS1701` о соединении
старого Roslyn с .NET 10 references; входные switch-проверки такого
исключения не имели.

Граница обработки warnings зафиксирована; интеграция с toolchains остаётся
проверкой реализации. Suppressor должен узнавать настоящий enum DSL symbol,
тип правил и только switch, который planner действительно дополняет.
Одного имени метода `Members` недостаточно: не подавлять warnings глобально,
в object/tuple `Members`, `Convert`, Using callbacks, `Construct` или
произвольном nested switch. Существующие Morphant switches сохраняют текущую
диагностику и поведение непокрытого входа. Нужны проверки diagnostic family для guards,
отключённых анализаторов и поддерживаемых IDE. В полностью сгенерированном
switch покрытие должно быть явным. Проверка объявленных enum-значений
остаётся обязанностью `UnmappedMemberValidation`.

### Типизация

Предыдущая isolated probe SDK 10.0.100/C# 9 подтвердила binding mixed
switch results через generic compile-time marker с conversions от destination,
`AutoMarker` и `AutoMarker<T>`: десять arms, `or`, guards, throw, вызовы справа,
строки/числа и прежние пробные callbacks `Values`/`Flags`. Их имя не является
необходимой частью проверенной типизации. Интеграция с реальными `Members`
delegates и выбором overload ещё не реализована и не проверялась этой пробой.

Nullable reference result marker позволяет natural null/default, но
генератор должен проверять их по настоящему destination type. Struct marker
ломает natural null. Промежуточному `var result = ... switch` может не
хватить target type при смеси enum и bare `Auto()`; существующий `Auto<T>()`
помогает. Не переходить на `object`/`dynamic` ради красивого примера.

Результаты старой пробы для `Auto(fallback: ...)` больше не относятся к
предлагаемому API. После удаления этой перегрузки нет и её отдельной проблемы
nullable generic fallback. Полная nullable-типизация актуальных callback
форм всё ещё должна быть проверена перед реализацией.

### Generated code

Текущие `Members` delegates уже параметризованы типом результата правил;
все четыре формы можно использовать без отдельного имени метода или нового
context overload.
[MemberConfigurationEmitter](../../src/Morphant.Generator/MemberSurface/PairConfiguration/MemberConfigurationEmitter.cs)
сегодня связывает их с object member-plan type. Для enum нужен scalar kind
правил и соответствующая typed surface. Просто включить нынешний `Members`
capability недостаточно: это не набор writable enum fields. Сохранить
существующий runtime callback path и порядок lifecycle для Using; после
выбора non-null результата подключить scalar `Members` с правильной
доступностью `result`. Не менять ordinary object/tuple path ради enum-ветки.

Enum shape содержит underlying type, constants/aliases, single-bit mask и
locations. Не приводить `ulong` к `long`. Генерировать типизированные
`Members` extensions со scalar rule result; не создавать фиктивные
construction/member records со свойством на каждый enum-элемент. Runtime
reflection, parsing через `Enum.Parse` и enum boxing не нужны.
Open `T : Enum` без известных полей требует ручного алгоритма либо diagnostic.

Для обычного примера краткая форма после дополнения может быть такой:

```csharp
return status switch
{
    DomainStatus.Cancelled => ApiStatus.Deleted,
    DomainStatus.Corrupt => throw new InvalidOperationException(),
    DomainStatus.Active => ApiStatus.Active,
    _ => ApiStatus.Unknown
};
```

Не добавлять недостижимые синтезированные arms после явных patterns.
Сложные guards/наследование или switch над другим выражением могут требовать
вложенной формы продолжения. Она должна сохранять значение выражения перед
switch, отдельный source для конвенции и условность вычислений, без раннего
запуска конвенции ради `result`.
Не менять явные независимые evaluations и не вводить local на каждый case.
Внутреннее отсутствие конвенционного результата отличать от пользовательского
exception; fallback не реализуется через `catch` вокруг пользовательского кода.
Сохранить общие failure stubs, settings diagnostics, compatibility manifest,
incrementality, cancellation/recovery и ограничения generated surface.

## 10. Закрытые решения и спорные вопросы

Принятое направление: C#-подобные декларации через существующий `Members`;
конвенция по умолчанию и отключение неявного подбора через существующий
`MemberSelection.Explicit`. Для поддержанных scalar enum-пар не генерировать
`Construct` и `Resolve`. `ConstructUsing`/`ResolveUsing` сохраняются с общим
null handling и могут сочетаться с `Members`, включая использование
начального `result`. Используются прежние четыре формы `Members`;
специальный overload с третьим `context` снят с наброска.
Это выбранное направление, но ещё не изменение production API. Flags
распознаются по атрибуту и используют правила `Members`; отдельный
`.Flags(...)` не вводится.
Обоснование минимального API приведено в разделе 4.

Один прежний вопрос мог объединять очевидную часть и реальную развилку.
Например, сохранение числа закрыто, но выбор поведения за пределами диапазона
ещё открыт. Наличие рекомендуемого варианта само по себе не делает его
очевидным или утверждённым.

### Категория 1: решения зафиксированы

Ниже зафиксированы решения первой категории и вопросы, уже закрытые
пользователем после обсуждения. Канонические подробности остаются в
указанных разделах.

| Вопрос | Зафиксированное решение | Основание и раздел |
|---|---|---|
| Library default стратегии | `ByName` для разных enum и enum-to-string; `ByValue` выбирается через обычные уровни настройки | Утверждено пользователем после исследования; раздел 4 |
| Сравнение имён `ByName` | Сначала `Ordinal`, при отсутствии точного совпадения — `OrdinalIgnoreCase`; оба этапа независимы от culture | Как подбор параметров конструктора, по уточнению пользователя; разделы 4 и 7 |
| Приоритет завершающей ветки | Явные специальные ветки, затем неявная конвенция, затем fallback; `MemberSelection.Explicit` отключает этап неявной конвенции | Выбрано пользователем; раздел 5. Порядок слияния правил через `IncludeBase` остаётся отдельным вопросом |
| Вход конвенции и `Auto()` | Всегда source текущей пары; выражение перед switch выбирает явную ветку, включая `result`, кортеж и `Normalize(source)` | Утверждено пользователем; раздел 5. Не подменять пару, не вычислять `result` конвенцией заранее |
| Числовая строка `ByValue` | Invariant decimal; точная знаковость/ширина; неназванные числа сохраняются; flags форматируются всей маской | Строка представляет число enum, без нового format/culture setting; раздел 7 |
| Сохранение числа | Автоматическое преобразование не теряет биты, не меняет знак и не оборачивает число при переполнении; явные C# casts сохраняют свою семантику | Конвенция ищет то же математическое число; раздел 7. Исключение либо fallback за пределами диапазона ещё не выбраны |
| Проверка покрытия | Существующий `UnmappedMemberValidation`, default `None`, warnings; доказанная неполнота отличается от невозможности доказать покрытие | Продолжает существующую настройку; без выполнения runtime callbacks при генерации; раздел 6 |
| Доступность `result` | Только реально выбранное начальное значение; диагностика недоступного чтения по пути исполнения, без ошибки за неиспользуемый параметр | Общий lifecycle и прежние четыре формы `Members`; раздел 5 |
| Settings и Using | Using не становится Convert; отсутствие `Members` не делает корректную применимую настройку пары ошибкой и не запускает конвенцию поверх готового значения | Существующая применимость settings и отсутствие нового запрета; раздел 4 |
| Неполный DSL switch | Узкая обработка предупреждений о неполноте только у дополняемой enum DSL декларации; прочие ошибки и ordinary C# diagnostics сохраняются | Уже выбранный короткий `Members` без обязательного `_ => Auto()`; раздел 9 |

Для `result`, типизации marker/delegates и suppressor остаются проверки
интеграции в generator, MSBuild и IDE. Это задачи реализации выбранного
контракта, а не новые пользовательские решения. Успешные isolated probes
не подменяют такие проверки.

### Категория 2: требуется обсуждение

1. **Flags.** Zero и composite overrides, guards/locals, fallback отдельного
   бита или всей маски, nullable результат отдельного бита, сочетание полной
   маски `result` с побитовыми правилами и атрибут только с одной стороны.
   Нужно выбрать одну последовательную семантику без повторных вычислений;
   конкретные предложения и примеры остаются в разделе 7. Побитовое
   сопоставление и автоматическое определение `[Flags]` не переоткрываются.
2. **Границы числовой конвенции и identity.** Любое число или только допустимое
   destination-значение; успешность flags ByValue при объявленном `Pair = 3`
   без отдельных `1`/`2`; исключение либо fallback при выходе из диапазона;
   неназванные значения при `E -> E`. Отсутствие потери числа закрыто, но не
   выбирает политику допустимости. Новая validation setting не предлагается.
3. **Композиция switches через `IncludeBase`.** Сохраняет ли локальный fallback
   базовые специальные случаи, как продолжается поиск после false guard и
   как выполняются locals разных уровней. Предложенная цепочка раздела 8
   ещё не утверждена. Наследование settings и разрешённое Using + Members
   уже определены и заново не обсуждаются.

Обратный string-to-enum ByValue не включается автоматически вслед за
числовым output. Это возможное расширение объёма, а не блокирующий вопрос
текущего наброска.

Объём feature в этой редакции: enum-to-enum, aliases, flags, nullable,
enum/integer, ordinary enum/string и числовая строка flags, неизвестные
значения, запреты, вычисляемые результаты, coverage, inheritance и
Create/Update. Автоматический reverse, формат списка flags-имён, wire
attributes, naming policies, коллекции и проекции не входят в этот набросок.

Будущие проверки должны защищать поведение, а не только форму API:

| Группа | Существенные сценарии |
|---|---|
| Selection | Bare registration; partial switch без Auto; Auto/Explicit; явный Auto при Explicit; missing result на Create и Update |
| Switch input | `source`, `result`, оба порядка кортежа, `Normalize(source)`; один source для неявной конвенции и Auto; `_ => result` при Auto/Explicit; однократное вычисление выражения перед switch; изменение source в guard не меняет вход конвенции |
| Precedence | Каждый уровень; included pair выше mapper; Default продолжает поиск; последняя запись; независимость порядка Configure |
| Names | Library default ByName; приоритет Ordinal, OrdinalIgnoreCase только без точного совпадения; независимость от culture; mixed-case enum/enum и string/enum; точное совпадение при наличии других вариантов регистра; неоднозначность второго этапа; aliases с одинаковыми и разными destination-числами; сохранение регистра enum-to-string; обычная C# семантика явных строковых patterns |
| Fallback | Именованный override; одноимённый автоматический case; неизвестное значение; computed fallback; пользовательский throw; failed explicit Auto; отсутствие повторного Auto |
| Coverage | None/Source/Destination/Strict; warning severity; covered catch-all; guards; динамический result и граница анализа; aliases; finite/infinite sides |
| Flags | Attribute detection; единый Members; Auto/Explicit; zero/composites; неизвестные биты и explicit numeric cases; per-bit fallback; high bit; aliases; порядок effects; успешное соответствие ByValue |
| Numeric | Same-value correspondence; unnamed source; отсутствие destination-соответствия; explicit checked/unchecked casts; отсутствие усечения и изменения знака; выбранная реакция на выход из диапазона; enum/integer без strategy setting |
| Enum-to-string | ByName/ByValue; Members overrides; Auto/Explicit и явный Auto; неизвестные числа; aliases; signed/ulong; invariant culture; flags whole-mask output; nullable |
| Lifecycle | Все null policies; nullable exact pairs; Update без Create; same-type mapping; обычный explicit nested Map |
| Runtime callbacks | Using null guards; ConstructUsing без/с previous, включая enum zero; ResolveUsing на обеих операциях; terminal null пропускает Members; whole-mask flags; method groups/delegates/context; без Members выбранное значение окончательно, корректная применимая strategy setting допустима |
| Composition | IncludeBase special cases/fallback; guards и locals из base; settings origin; конфликт с Convert; допустимые Members+Using локально и после IncludeBase; чтение начального result; Auto/Explicit с фабрикой; эффекты неиспользованной фабрики; result не flags-накопитель |
| API surface | Единый enum Members, без Values/Flags; прежние четыре формы с result/context; диагностика недоступного result, но не неиспользуемого параметра; отсутствие Construct/Resolve; сохранение Using; enum/string/integer/nullable; прежняя композиция object/tuple |
| Compiler/generator | C# 9; точечные suppressions и warnings-as-errors; nested switch; wrong types; obsolete; edit settings/enum/callback; cancellation/recovery |

Изменён только внутренний дизайн. Production API, generator и постоянные
тесты enum feature не реализованы. Общие contracts и публичные settings
документы пока описывают только действующее поведение.
