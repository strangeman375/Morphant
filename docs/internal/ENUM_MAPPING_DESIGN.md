# First-class enum mapping: исследование и набросок дизайна

Дата: 2026-09-23. Статус: согласованный дизайн обычных enum и flags;
реализация впереди. Редакция 23: пользователь утвердил ByName только для
целых именованных значений в ByMask, строгую допустимость целой маски как
OR объявленных destination-значений, правила zero/null, применимость
FlagsMappingMode, порядок битов и предупреждение о доказанно недостижимых
composite cases. Все ранее вынесенные вопросы обычных enum и flags закрыты.
ConstructUsing и ResolveUsing можно сочетать с Members; сохраняются его
существующие формы с result и context. Construct и Resolve не генерируются.
Границы согласованного объёма и проверки реализации перечислены ниже.
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
`[Flags]` распознаётся при генерации. Согласованная `FlagsMappingMode`
выбирает обработку отдельных битов через `Members` либо полной маски;
library default — `ByBit`. Отдельный callback `Flags` для этого не нужен.

Требование к этой редакции: каждая новая настройка должна отвечать на вопрос,
на который не отвечают существующие настройки. Наличие отдельной внутренней
ветки алгоритма само по себе не обосновывает новый публичный переключатель.

Зафиксированы решения: library default ByName, сопоставление имён в два
этапа, конвенция перед завершающей веткой, её привязка к source текущей пары,
числовое строковое представление, строгий ByValue и ByValueAllowUndefined,
сохранение числа, выбор реакции на переполнение через Members, same-type,
применимость числовых режимов и default для integer-to-enum, композиция
правил через IncludeBase, контракт coverage, доступность `result`,
применимость settings с Using, семантика обоих flags-режимов и
границы обработки compiler warnings. Их канонические описания находятся
ниже; раздел 10 перечисляет закрытые решения и границы объёма.

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

Для обычных enum после этой проверки нужна `EnumMappingStrategy`.
Её вопрос: что считать автоматическим соответствием — совпадение имени,
объявленное числовое значение или любое представимое число? Имя и число
также задают разные представления enum в строке.
`MemberSelection` определяет, применять ли неявную конвенцию;
`UnmappedMemberValidation` только диагностирует покрытие. Ни одна из них
не выбирает критерий и множество успешных соответствий. Полное перечисление
случаев при `Explicit` либо ручной cast не заменяют выбор числовой конвенции.

Для flags пользователь утвердил дополнительную `FlagsMappingMode`:
она отвечает на другой вопрос — обрабатывать отдельный бит или полную маску.
Ни стратегия соответствия, ни selection, ни coverage не определяют эту
единицу. Значения `Default`, `ByBit`, `ByMask` и их поведение описаны в
разделе 7. Она применима только к flags enum → flags enum. Обе настройки
независимы и используют обычные уровни precedence.

Для `EnumMappingStrategy` утверждены значения `Default`, `ByName`,
`ByValue`, `ByValueAllowUndefined`.
`ByValue` — строгий числовой режим; `ByValueAllowUndefined` сохраняет и
неназванное число в пределах диапазона destination.
`Default` продолжает обычный поиск настройки. Library default для
enum-to-enum, включая одинаковые типы, и enum-to-string — `ByName`.
Для integer-to-enum library default — строгий `ByValue`.
Числовой режим выбирается для пары, mapper или через MSBuild по общим
правилам precedence.
[Исследование default](ENUM_MAPPING_DEFAULT_RESEARCH.md) содержит обоснование
решения. Для самой стратегии `ByName`
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
Не добавлять сюда `Explicit`, `Flags`, fallback или режимы проверки покрытия:
эти вопросы уже имеют свои правила и не выбирают успешные соответствия.

Стратегия применяется к enum-to-enum, enum-to-string и integer-to-enum.
В последнем направлении форма пары выбирает число, а стратегия — строгость.
Обратный enum-to-integer сохраняет числовой путь без этой настройки:
у integer destination нет набора объявленных значений. String-to-enum пока
сохраняет конвенцию имён; расширение на числовой parsing — отдельное
предложение, не следствие поддержки numeric output. Same-type enum
подчиняется той же стратегии (раздел 8). Для enum-to-string оба числовых
режима форматируют число без проверки source-имени (раздел 7).

Для integer-to-enum применимы `ByValue` и `ByValueAllowUndefined`, включая
наследование с общих уровней. У source нет имени, поэтому `ByName`, явно
заданный на такой паре, даёт diagnostic о неприменимой настройке по общему
контракту `MORPH0023`. Выигравший `ByName` с общего уровня mapper, base mapper
или MSBuild не мешает числовой паре: для неё используется строгий `ByValue`.

Порядок разрешения настройки не меняется: сначала общий resolver выбирает
значение, затем определяется его применимость. Если выиграл общий `ByName`,
поиск не продолжается по нижним уровням ради другой числовой стратегии.
Например, mapper-level `ByName` поверх MSBuild `ByValueAllowUndefined`
оставляет integer-to-enum строгим. `Default` по-прежнему продолжает поиск;
чтобы переопределить унаследованный `ByValueAllowUndefined` строгим режимом,
пара задаёт `ByValue`, а не `Default`.

Используются обычные pair/mapper/MSBuild уровни и общий resolver, без
нового уровня named arguments на `Auto()`:

```csharp
builder.Map<WireCode, StoredCode>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValue);
```

Enum strategy defaults на mapper/assembly могут сосуществовать с object,
enum-to-integer, string-to-enum и `Convert`; на этих mappings они не используются.
Явную настройку на паре, к которой она принципиально неприменима, диагностировать
по общему контракту `MORPH0023`. Для enum-to-enum, enum-to-string и integer-to-enum
наличие Using не меняет применимость стратегии и не приравнивает mapping к `Convert`.
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
различаются. Пользователь выбрал управление допустимостью через два
числовых значения одной `EnumMappingStrategy`, без отдельного setting
проверки числовой допустимости.
Стратегия определяет успех конвенции, `Members` — явные исключения и
действие при отсутствии соответствия. Прежнее предложение оставлять
сохранение неназванных чисел только ручному cast снято.
Пользователь выбрал управление реакцией на переполнение через существующий
`Members`; правила приведены в разделе 7.

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
не задаёт порядок runtime-стадий. Семантика flags приведена в разделе 7.

### Входы `Members`

Сохраняются все четыре существующие формы:

- `source => rules`: non-null source после null policy.
- `(source, previous) => rules`: тот же source и `Option` исходного non-null
  destination; для Create и Update с null destination — `None`.
- `(source, previous, result) => rules`: дополнительно начальное non-null
  destination-значение, выбранное до выполнения правил.
- `(source, previous, result, context) => rules`: дополнительно существующий
  DSL context с `Operation`, без полного runtime `MappingContext`.

Для flags `source` означает текущий бит при `ByBit` и полную source-маску
при `ByMask`. `previous` остаётся исходной destination-маской. Смысл `result`
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
выражения перед switch и не создают другую mapping pair. В
flags-to-flags mapping этим source остаётся текущий бит при `ByBit`;
при `ByMask` используется полная source-маска по контракту выше.

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

Согласованный порядок композиции local и inherited rules через `IncludeBase`,
включая вычисления между уровнями, описан в разделе 8.

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
flags-to-flags mapping единицу выбирает `FlagsMappingMode`: текущий бит при
`ByBit`, полная маска при `ByMask`. Полный ручной алгоритм без дополнения
конвенцией задаётся обычным callback из раздела 5.

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
порядок; композиция через `IncludeBase` определена в разделе 8. Режим выбора
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
Для `ByMask` анализируются правила целых объявленных значений с конвенцией
раздела 7. Ноль учитывается как отдельный вход правил; его обработка не
доказывает покрытие ненулевых значений. Не перечислять все комбинации маски
ради анализа покрытия. Для composite declarations и динамических результатов
сохраняется различие доказанного покрытия и границы статического анализа.

## 7. Flags, aliases, строки и числа

Пользователь согласовал два режима обработки flags и их семантику.
Исследование других инструментов и сравнение рассмотренных альтернатив находятся в
[исследовании flags](ENUM_MAPPING_FLAGS_RESEARCH.md). Этот раздел —
канонический контракт выбранных режимов.

### FlagsMappingMode: единица обработки

Генератор определяет flags по `System.FlagsAttribute`, включая типы из
metadata и nullable underlying enum. Не угадывать flags по числам `1, 2, 4`
без атрибута. Анализ выполняется при генерации, без runtime reflection.
Отдельный callback `.Flags(...)` не нужен: оба режима используют `Members`.
Настройка применима только к flags enum → flags enum; nullable формы
проверяются по underlying enum. Остальные направления описаны ниже.

Утверждены `FlagsMappingMode.Default`, `ByBit`, `ByMask`.
Library default — `ByBit`: переименование отдельного флага через Members
работает во всех его комбинациях без дополнительной настройки.
`ByMask` явно выбирает обработку целого значения.

| Поведение | ByBit | ByMask |
|---|---|---|
| Source правил | Текущий установленный бит того же source enum | Полная исходная маска |
| Members | Правила применяются к каждому биту | Правила применяются один раз к маске |
| Неявная конвенция и Auto() | Для текущего бита | Для полной маски |
| Завершающий fallback | Результат непереведённого бита | Итог всей операции |
| Объединение | OR результатов битов | Выбранный результат возвращается целиком |
| Composite case непосредственно над source | Не совпадает с отдельным битом | Проверяет точное значение маски |

Настройка использует общий precedence pair/included pair/mapper/base mapper/
MSBuild/library default. `Default` продолжает поиск, не сбрасывая наследование
на `ByBit`. Для явного переопределения унаследованного `ByMask` задаётся `ByBit`.

`FlagsMappingMode` и `EnumMappingStrategy` независимы: первая выбирает единицу,
вторая — соответствие для этой единицы. Смена `ByName` на `ByValue` или
`ByValueAllowUndefined` не меняет вход и количество применений Members.
Атрибут flags не переключает стратегию. MemberSelection.Explicit отключает
неявную конвенцию в обоих режимах; Auto() по-прежнему работает явно.

Один effective режим применяется ко всей итоговой паре, включая Members из
IncludeBase. Порядок для выбранной единицы остаётся общим: локальные
специальные ветки → базовые от ближайшей → effective конвенция → ближайший
fallback. Не выполнять локальные правила над полной маской, а базовые над
битами, или наоборот. Изменение режима пары меняет единицу и для inherited
правил; составные правила не получают отдельного приоритета перед уровнями.

### ByBit: правила отдельных битов

Пример предлагаемого API, ещё не реализованный в production:

```csharp
[Flags]
enum SourceAccess
{
    None = 0, Read = 1, Write = 2, Delete = 4, Audit = 8,
    ReadWrite = Read | Write
}

[Flags]
enum TargetAccess
{
    None = 0, View = 16, Edit = 32, Audit = 64, Unknown = 128
}

// В Configure: ByBit — library default.
builder.Map<SourceAccess, TargetAccess>()
    .Members(flag => flag switch
    {
        SourceAccess.Read => TargetAccess.View,
        SourceAccess.Write => TargetAccess.Edit,
        SourceAccess.Delete => TargetAccess.None
    });
```

Вход раскладывается на установленные биты; каждый получает результат по
правилам Members. Результаты объединяются через OR. Один бит может дать
ноль, один или несколько destination-битов. Совпадающие вклады объединяются
без ошибки. Aliases одного source-бита не означают повторной обработки.

| Вход примера | Результат при ByName и MemberSelection.Auto |
|---|---|
| Read | View по явному правилу |
| Read \| Write | View \| Edit |
| ReadWrite | Тот же View \| Edit; имя комбинации не меняет разбиение |
| Read \| Delete | View; удаление Delete задано явно |
| Read \| Audit | View \| Audit; Audit сопоставлен по имени |

ByName сопоставляет имя текущего бита по общему exact-first/ignore-case
правилу. Составное объявление не даёт имён отдельным битам: если объявлен
только Pair = 3, ByName не получает из него имена для 1 и 2.

При Explicit разбиение и OR сохраняются, но отсутствует неявная конвенция.
Auto() запрашивает её для текущего бита даже при Explicit; неудача бросает
исключение без перехода к fallback. Явные правила действуют при любой
EnumMappingStrategy и не обходятся числовым cast.

### ByMask: правила полного значения

```csharp
builder.Map<SourceAccess, TargetAccess>()
    .FlagsMappingMode(FlagsMappingMode.ByMask)
    .Members(mask => mask switch
    {
        SourceAccess.ReadWrite => TargetAccess.View | TargetAccess.Edit,
        _ => Auto()
    });
```

Для ReadWrite специальная ветка выбирает итог всей операции. Правило для
Read само по себе не обрабатывает наличие этого бита внутри ReadWrite.
Составные patterns, guards над маской и выражения с result используют
обычную семантику явного switch с согласованным дополнением конвенцией.
Нет предварительной проверки whole-mask cases с последующим повторным
запуском Members на битах. Прежнее предложение смешанного прохода снято.

Конвенция и Auto() получают всю исходную маску, независимо от выражения
перед switch. ByValue/ByValueAllowUndefined рассматривают её математическое
число целиком.

ByName сопоставляет только имена целого объявленного source-значения с
целыми destination-значениями по общим exact-first/ignore-case правилам.
Для aliases действуют общие правила неоднозначности ниже. Совпадение имени
ReadWrite может связать разные числа и разные составы битов. Конвенция не
разбирает маску на биты и не собирает результат из их имён.

Например, source с Read = 1, Write = 2 и destination с Read = 8, Write = 16
дают для входа 3 результат 24 в ByBit. В ByMask у неназванного 3 нет
соответствия, хотя отдельные биты имеют совпадающие имена. Если оба enum
объявляют ReadWrite, ByMask сопоставляет целые значения по этому имени.
При отсутствии соответствия действует fallback/throw. Таким образом,
whole-mask overrides не дополняются автоматической сборкой остальных
комбинаций по именам битов. Нулевая маска — отдельный случай конвенции ниже.

### Фабрика, result и вычисления

Using callbacks сохраняют прежний lifecycle при обоих режимах: получают
полную source-маску и вызываются один раз, когда lifecycle требует вызова.
Они не получают DSL-конвенцию и не запускаются внутри обхода битов.
Без Members выбранное фабрикой значение окончательно. Null фабрики завершает
операцию до Members, без повторного запуска null policies.

Result остаётся начальным destination, выбранным фабрикой либо взятым из
имеющегося destination при пропуске ConstructUsing на Update. Previous
сохраняет исходную destination-маску. При ByBit все правила видят один и тот
же полный result; он не становится накопителем и не добавляется к OR
автоматически. Доступность result сохраняет общий контракт раздела 5;
на Create без выбранного начального значения не подставлять фиктивный ноль.

Locals, guards и выражение перед switch вычисляются на своём месте для
достигнутого уровня правил: при ByBit — для текущего бита, при ByMask — для
целой маски. Не повторять вычисления при переходе к fallback. Сохраняются
ленивый вход в базовые правила и области видимости по контракту IncludeBase.
Чтение result либо наличие фабрики само по себе не меняет режим.

В ByBit установленные биты обрабатываются от младшей позиции к старшей
в точной ширине source underlying type. Signed high bit обрабатывается
последним. Порядок объявлений и aliases не меняют порядок; каждый физический
бит обрабатывается один раз. Не расширять отрицательную маску знаковыми
битами за пределы исходной ширины.

### Нулевая маска и nullable результат

В обоих режимах нулевой source проходит правила один раз с source = 0.
Поэтому явное `None => Unknown` может изменить результат. Если специальные
правила не выбрали результат, неявная конвенция возвращает ноль даже без
объявленного нулевого значения и независимо от имён. Это пустая маска;
правило действует и при ByMask + ByName, не требуя совпадения имени None.

MemberSelection.Explicit отключает и эту неявную конвенцию: нужны явное
правило, Auto() или fallback; иначе исключение. Явный Auto() запрашивает
нулевую конвенцию и при Explicit. Нулевое правило не добавляется к обработке
ненулевой маски и не выполняется для каждого её бита.

При nullable destination возврат null из любого битового правила в ByBit
сразу завершает всю операцию с null. Следующие биты не вычисляются; уже
выполненные эффекты не откатываются. Null не означает пропуск бита или ноль:
чтобы удалить только текущий бит, правило возвращает нулевую маску.
ByMask сохраняет обычный контракт scalar nullable результата. Null source
и terminal null фабрики проходят прежний lifecycle и не становятся zero.

### Fallback, неизвестные биты и числа

Fallback относится к выбранной единице обработки. При ByBit, если добавить
к примеру выше `_ => TargetAccess.Unknown`, вход Read|(SourceAccess)16
получит View|Unknown. `_ => TargetAccess.None` намеренно удаляет только
непереведённые биты. При ByMask завершающая ветка возвращает итог всей
операции; это не дополнение уже частично собранной маски.

Неизвестный source-бит в ByBit также проходит правила. Явный case
`(SourceAccess)16 => ...` может обработать его. ByName без имени не находит
соответствия; далее действует fallback/throw. Не отбрасывать бит до правил.
Без fallback непокрытый бит бросает исключение всей операции; частичный
результат не возвращается. Уже выполненные пользовательские вычисления не
откатываются. Пользовательские исключения не считаются неуспехом конвенции.

Числовая конвенция проверяет выбранную единицу, а не произвольный общий
результат OR. Явные результаты не получают скрытой проверки объявленности.
При destination с единственным Pair = 3:

| Режим со строгим ByValue | Вход 3 |
|---|---|
| ByMask | Целое число 3 имеет объявленное соответствие Pair |
| ByBit | Отдельные 1 и 2 соответствий не имеют; для них действует fallback/throw |

Это согласованное различие режимов. Перенос целого числа -1 из int в sbyte
относится к ByMask: число помещается в диапазон. При ByBit среди отдельных
битов int-маски есть 256, которое в sbyte не помещается; AllowUndefined
не снимает эту проверку. Не объявлять побитовый cast эквивалентом whole-mask
cast. Signed high bit имеет своё математическое значение в исходном типе,
не unsigned значение произвольного промежуточного типа.

Оба числовых режима сохраняют число выбранной единицы в пределах диапазона.
Неуспех неявной конвенции передаётся fallback этой единицы; явный Auto()
бросает. Поведение явно написанных checked/unchecked выражений сохраняется.

### Строгая допустимость целой числовой маски

В ByMask и integer → flags enum строгий ByValue допускает маску, которую
можно получить через OR целых объявленных destination-значений, включая
пустую комбинацию 0. Число сначала должно помещаться в underlying type
destination; объявленность source не требуется.

| Объявления destination | Допустимо | Недопустимо |
|---|---|---|
| Read = 1, Write = 2 | 0, 1, 2, 3 | 4 |
| Только Pair = 3 | 0, 3 | 1, 2 |
| Pair = 3, Audit = 4 | 0, 3, 4, 7 | 1, 2, 5, 6 |
| Только All = -1 | 0, -1 | 1 |

Составное объявление можно включить целиком, но оно не объявляет каждый
свой бит самостоятельным значением. All = -1 не разрешает произвольное
число. Это шире exact declared lookup и строже проверки «все биты хотя бы
где-то объявлены». ByValueAllowUndefined сохраняет любое представимое число.

Критерий не требует перебора подмножеств: в фиксированной ширине destination
можно объединить все объявленные c, целиком содержащиеся в проверяемой маске
x, и сравнить OR с x. Ноль удовлетворяет критерию как пустая комбинация.
Конкретную форму generated code выбирает реализация в рамках этого контракта.

Этот критерий относится к конвенции целой маски. В ByBit строгое числовое
соответствие проверяется отдельно для каждого бита: Pair = 3 не разрешает
1 и 2. Итоговый OR результатов и явные результаты Members не получают
скрытой проверки. При неуспехе конвенции сохраняется fallback/throw;
явный Auto() бросает без перехода к fallback.

### Применимость FlagsMappingMode

| Направление | Единица обработки |
|---|---|
| Flags enum → flags enum, включая nullable формы | Бит или маска по выбранному режиму |
| Enum → enum с FlagsAttribute только с одной стороны | Целое значение по обычным правилам пары; OR результатов не вводится |
| Integer → flags enum | Целое число; строгая допустимость маски описана выше |
| Flags enum → integer | Число всей маски с проверкой диапазона |
| Flags enum → string с числовой стратегией | Invariant decimal всей маски |

На остальных видах пар FlagsMappingMode не вводит побитовый алгоритм.
Явная настройка на неприменимой паре диагностируется по общему контракту
MORPH0023; настройка с общего уровня mapper/base mapper/MSBuild игнорируется
для такой пары. Обычные правила применимости и происхождения settings
сохраняются. Наличие Using у применимой пары не меняет её применимость;
без Members готовое значение фабрики остаётся окончательным.

Автоматический формат списка flags-имён и обратный parsing вне
согласованного объёма; молчаливый ToString/Parse не заменяет их контракт.

### Диагностика недостижимых composite cases

В ByBit выдаётся предупреждение, если доказано, что составной case
недостижим для входа правила. Например, неизменённый текущий бит не равен
Read | Write. Это warning, а не безусловная ошибка: правило может быть
унаследовано из конфигурации, используемой также с ByMask. Severity можно
повысить обычными средствами diagnostics, без новой настройки.

Недостаточно увидеть составную константу или имя source-параметра. Нужно
учитывать выражение перед switch, locals и изменения source в вычислениях
и guards. Pattern над полным result либо Normalize(flag) может быть
достижимым; общего запрета таких patterns нет. Анализ ограничен статически
доказуемыми случаями и не исполняет пользовательский код при генерации.
Не заменять предупреждение молчаливым игнорированием доказанно недостижимого
правила. Обычные ошибки C# сохраняются.

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
| `ByValueAllowUndefined` | `"2"` |

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

Для `ByValue` и `ByValueAllowUndefined` зафиксирована десятичная запись underlying integer в
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
У строки нет набора объявленных enum-значений; оба числовых режима применимы
и дают одинаковый output, включая неназванные source-числа.
Симметричный `string -> enum` с ByValue также можно обсудить в рамках той же
настройки, но до расширения требуется выбрать грамматику parsing, overflow
и поведение неназванного destination-числа; сейчас это не принятый контракт.

### Числовая конвенция

Сравнение популярных мапперов и история рекомендаций для обычных enum:
[неизвестные числа, identity и диапазон](ENUM_MAPPING_NUMERIC_RESEARCH.md).
После исследования пользователь выбрал поддержку двух числовых режимов.
Решения по flags обоснованы отдельно в
[исследовании flags](ENUM_MAPPING_FLAGS_RESEARCH.md).

Поддерживаемые целые типы: `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`,
`long`, `ulong`. Для enum-to-enum числовой путь выбирается через строгий
`ByValue` или `ByValueAllowUndefined`. Integer-to-enum использует те же
числовые режимы со строгим default; enum-to-integer переносит число без
выбора стратегии. Этот раздел описывает числовые преобразования обычных enum
с enum/integer destination; форматирование в string рассмотрено выше.
Для flags enum → flags enum единица числовой обработки определяется режимом
из раздела FlagsMappingMode. Зафиксировано сохранение математического числа
с учётом диапазона destination underlying type независимо от checked options consumer.
Не усекать автоматически старшие биты и не менять знак через промежуточное
приведение: `ulong` не сужается до `long`, а `256` не превращается в byte `0`.

Для enum-to-enum по числу и integer-to-enum приняты два режима:

- `ByValue` — строгое соответствие объявленному destination-значению с тем же числом.
- `ByValueAllowUndefined` — сохранение числа, даже если для него нет
  объявленной destination-константы.

Оба режима соблюдают диапазон destination и сохраняют математическое число.
Ослабленный режим не означает unchecked cast и не разрешает усечение или смену знака.
Строгость относится к конвенции; явно написанные результаты `Members`
не получают скрытой проверки объявленности.

Строгий режим проверяет объявленность в destination, без отдельного
требования к source. Неназванное source-число, уже объявленное в destination,
имеет соответствие. Enum-to-integer сохраняет и неназванные значения в
пределах диапазона: у integer нет перечня объявленных вариантов.

Для integer-to-enum это даёт тот же результат без проверки source-имени.
Например, destination основан на `byte` и содержит только `Unknown = 0`
и `Active = 1`; в декларативном switch задано `_ => Unknown`:

| Числовой source | `ByValue` (default) | `ByValueAllowUndefined` |
|---|---|---|
| `1` | `Active` | `Active` |
| `42` | `Unknown` через fallback | Неназванное destination-значение `42` |
| `300` или `-1` | `Unknown` через fallback | `Unknown` через fallback |

Без завершающей ветки на месте fallback будет исключение. Явные правила,
`MemberSelection.Explicit`, `Auto()` и null handling сохраняют общие контракты.

Непредставимое число означает отсутствие конвенционного соответствия.
На неявном пути действие выбирают существующие правила `Members`:

| Намерение | Выражение в декларативном switch |
|---|---|
| Fallback для любого отсутствующего соответствия, включая overflow | Завершающее `_ => Unknown` |
| Исключение при любом отсутствии соответствия | Нет завершающей ветки, `_ => Auto()` или `_ => throw ...` |
| Overflow бросает, неизвестное число в диапазоне получает fallback | Явная ветка с проверкой диапазона и throw, затем завершающее `_ => Unknown` |

Например, для source underlying `ushort` и destination underlying `byte`
ветка `_ when (ushort)status > byte.MaxValue => throw new OverflowException()`
выполнится до конвенции. Неназванное число в диапазоне на строгом пути
сможет перейти к fallback. Это обычный пользовательский guard, не новый
DSL marker. Такой способ выбора реакции на переполнение утверждён;
отдельный setting `EnumOverflowHandling` не вводится. Единая политика на
множество пар без повторения guard потребовала бы отдельного обоснованного
сценария; сейчас она не входит в принятый API.

Явный `Auto()` при неудаче, как и раньше, бросает mapping exception без
перехода к fallback. Пользовательские исключения не перехватываются.
Выражения сохраняют свои явные `checked`/`unchecked`.
Сохранение неизвестного числа остаётся доступно также через явный C#:

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
выражении сохраняется. При flags enum → flags enum для числового переноса
полной маски выбирается ByMask; обычный Using callback также остаётся доступен.
Побитовый и целый числовые пути различаются по согласованному контракту выше.

Критерий допустимости неназванных целых flags-комбинаций при строгом ByValue
зафиксирован выше: OR целых объявленных destination-значений.
[Enum.IsDefined](https://learn.microsoft.com/en-us/dotnet/api/system.enum.isdefined?view=net-10.0)
не эквивалентен проверке произвольной допустимой flags-комбинации.

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

У flags вход правил определяется выбранным ByBit/ByMask из раздела 7;
исходная маска проходит null guards один раз.

`Create`/`Update` вычисляют scalar result после обычных guards. `previous`
содержит исходный non-null destination. При Using начальное значение
выбирается до `Members` по lifecycle раздела 5. Для обычного enum и
enum-to-string правило возвращает итог операции. У flags-to-flags ByBit
объединяет результаты битов, а ByMask возвращает результат целиком.
Nullable результат отдельного бита `null` завершает всю операцию с null
без выполнения следующих битов, по контракту раздела 7.
Контекст операции и формы callback описаны в
разделе 5; null destination не превращает Update в Create.

Для обычных enum в `E -> E` применяется выбранная стратегия так же,
как для разных enum:
`ByName` и строгий `ByValue` не сохраняют неназванное число автоматически;
`ByValueAllowUndefined` сохраняет. Прежняя рекомендация identity для
неизвестных значений при любой стратегии снята: она обходила бы явно
выбранную строгость. Explicit overrides выполняются
первыми, `Explicit` отключает неявную конвенцию, `Auto()` запрашивает её явно.
Присваивание вместо преобразования допустимо лишь там, где оно сохраняет
выбранные правила и проверки; это не самостоятельная политика identity.
У flags одинаковые типы также не обходят выбранную стратегию и режим:
в ByBit неназванная комбинация может успешно собраться из переводимых битов,
даже если целиком не является объявленной константой.

### `IncludeBase`

Для обычных enum согласовано объединение декларативных правил одной и той
же пары типов из базового mapper. База подключается через `base.Configure`,
а её пара — через `IncludeBase` по действующим правилам
[наследования конфигурации](../configuration-inheritance.md).
Совместимость типов и наследование настроек не меняются.

Порядок поиска результата:

1. Локальные специальные ветки в написанном порядке.
2. Специальные ветки подключённых баз, от ближайшей к дальней; внутри
   каждого уровня сохраняется написанный порядок.
3. Неявная конвенция с итоговыми настройками текущей пары.
4. Ближайшая заданная завершающая ветка; при её отсутствии — mapping exception.

Новый `Members` не стирает весь набор inherited overrides.
`MemberSelection.Explicit` пропускает только шаг 3: базовые явные правила
сохраняются, явно написанный `Auto()` продолжает запрашивать конвенцию.
Конвенция и `Auto()` используют source текущей пары по разделу 5;
отдельная конвенция базового mapper не запускается.

Ложный guard продолжает поиск, в том числе в базе для того же значения.
Само упоминание константы в локальном pattern не удаляет базовую ветку.
Приоритет определяется уровнем и написанным порядком, без сравнения
«специфичности» patterns: локальный `_ when condition` при истинном условии
предшествует базовой ветке для конкретной константы. Выбранный результат
завершает mapping; пользовательские исключения прекращают поиск.

Локальная завершающая ветка заменяет только базовый fallback, сохраняя
базовые специальные случаи. Если локальной завершающей ветки нет,
наследуется ближайшая базовая. Это относится и к `_ => throw ...`, и к
`_ => Auto()`: они выполняются после всех специальных веток. В отличие от
них, `Cancelled => Auto()` явно перекрывает базовую обработку `Cancelled`.
Неудача такого запроса бросает исключение без возврата к базовому правилу
или fallback. Завершающий bare `Auto()` не дублирует неявную конвенцию
по уже принятому правилу раздела 5.

Например, база задаёт `Cancelled => Deleted`, `Suspended => Disabled`
и `_ => Unknown`; локально заданы `Cancelled => Archived` и
`_ => Unrecognized`. При `ByName` и `MemberSelection.Auto`, если `Active`
есть на обеих сторонах, а `Legacy` — только в source:

| Source | Результат | Выбранное правило |
|---|---|---|
| `Cancelled` | `Archived` | Локальное специальное |
| `Suspended` | `Disabled` | Базовое специальное |
| `Active` | `Active` | Конвенция |
| `Legacy` | `Unrecognized` | Локальная завершающая ветка |

Если локальный `Cancelled` дополнен `when ShouldArchive(status)`, false
даёт базовый `Deleted`; true — локальный `Archived`.

Вычисления внутри block lambda сохраняют области видимости и порядок.
Подготовка локальных значений выполняется при входе в соответствующий
уровень один раз, до его switch. Базовые вычисления запускаются только
при достижении базовых правил; сработавшая локальная специальная ветка
до них не доходит. Возврат к выбранному fallback не запускает тело `Members`
повторно: он использует уже полученные locals своего уровня. Само выражение
завершающей ветки вычисляется только при выборе fallback.
Guards и результаты не вычисляются заранее; исключения
пользователя не перехватываются как отсутствие соответствия.

Например, локальный `var policy = GetPolicy(status)` перед switch
выполняется один раз. Если локальные и базовые специальные ветки и
конвенция не дали результата, `_ => policy.UnknownStatus` использует тот
же `policy`. Locals базового уровня остаются в своей области видимости.
Произвольное прямое выражение с результатом для всех входов остаётся полным
explicit правилом и может закрыть дальнейший поиск по разделу 5.

Для самого IncludeBase новые настройки и `Inherited()` не добавляются.
Единый режим обработки flags-пары после композиции описан в разделе 7.
Не превращать inherited `Convert`/factory в набор enum rules. Using callbacks
могут наследоваться
для точной пары по действующему контракту и сочетаться с `Members`.
Общие guards и выбор начального результата выполняются по lifecycle
раздела 5; локальные и базовые правила используют тот же начальный `result`.
Сработавшая ветка возвращает окончательное значение, не передавая его
следующему уровню для обработки. После композиции проверяются доступность
`result`, ограничения на несколько destination methods и `Convert`;
отдельного конфликта Using + Members нет. Сохраняются текущие ограничения
доступности helper-методов и cross-assembly inheritance.

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

## 10. Закрытые решения и границы объёма

Принятое направление: C#-подобные декларации через существующий `Members`;
конвенция по умолчанию и отключение неявного подбора через существующий
`MemberSelection.Explicit`. Для поддержанных scalar enum-пар не генерировать
`Construct` и `Resolve`. `ConstructUsing`/`ResolveUsing` сохраняются с общим
null handling и могут сочетаться с `Members`, включая использование
начального `result`. Используются прежние четыре формы `Members`;
специальный overload с третьим `context` снят с наброска.
Это выбранное направление, но ещё не изменение production API. Flags
распознаются по атрибуту и используют правила `Members`; единицу обработки
выбирает согласованная `FlagsMappingMode`. Отдельный callback `.Flags(...)`
не вводится.
Обоснование минимального API приведено в разделе 4.

Числовые режимы, их имена, границы строгости, same-type и способ управления
переполнением утверждены пользователем. Применимость к integer-to-enum,
строгий default и поведение общего `ByName` также согласованы.
Композиция правил обычных enum через `IncludeBase` утверждена, включая
приоритет уровней, наследование fallback, guards и однократные вычисления.

### Категория 1: решения зафиксированы

Ниже зафиксированы решения первой категории и вопросы, уже закрытые
пользователем после обсуждения. Канонические подробности остаются в
указанных разделах.

| Вопрос | Зафиксированное решение | Основание и раздел |
|---|---|---|
| Library default стратегии | `ByName` для enum-to-enum, включая одинаковые типы, и enum-to-string; строгий `ByValue` для integer-to-enum | Утверждено пользователем; разделы 4 и 8 |
| Режим flags | `FlagsMappingMode`: Default/ByBit/ByMask; library default ByBit; единица Members, Auto и fallback — бит либо маска | Утверждено пользователем; независим от EnumMappingStrategy, обычный precedence, единый effective режим IncludeBase, Using всегда над полной маской; раздел 7 |
| ByMask + ByName | Имена целых объявленных значений; без сборки по именам отдельных битов | Утверждено пользователем; общие правила aliases и сравнения имён, отдельная конвенция пустой маски; раздел 7 |
| Строгая числовая маска | В ByMask и integer-to-flags допустим OR целых объявленных destination-значений, включая 0 | Утверждено пользователем; диапазон проверяется первым, явные результаты не валидируются; раздел 7 |
| Zero и nullable flags | Нулевой вход проходит правила один раз; конвенция даёт 0, Explicit её отключает; null из битового правила сразу завершает операцию | Утверждено пользователем; null не равен zero, следующие биты не вычисляются; раздел 7 |
| Применимость и порядок flags | Режим только для flags-to-flags; биты от младшего к старшему в ширине source | Утверждено пользователем; общая диагностика неприменимых settings, aliases не меняют порядок; раздел 7 |
| Недостижимый composite case | Warning только при доказанной недостижимости, без общего запрета patterns над result или вычисленным выражением | Утверждено пользователем; обычное управление severity, без новой настройки; раздел 7 |
| Стратегия integer-to-enum | Оба числовых режима применимы; явный pair-level `ByName` диагностируется, выигравший общий `ByName` оставляет строгий default; повторного поиска по нижним уровням нет | Утверждено пользователем; обычный precedence и смысл `Default` сохраняются; раздел 4 |
| Сравнение имён `ByName` | Сначала `Ordinal`, при отсутствии точного совпадения — `OrdinalIgnoreCase`; оба этапа независимы от culture | Как подбор параметров конструктора, по уточнению пользователя; разделы 4 и 7 |
| Приоритет завершающей ветки | Явные специальные ветки, затем неявная конвенция, затем fallback; `MemberSelection.Explicit` отключает этап неявной конвенции | Выбрано пользователем; раздел 5, композиция уровней — раздел 8 |
| Композиция `IncludeBase` | Локальные специальные ветки, базовые от ближайшей, общая конвенция и ближайший fallback; false guard продолжает поиск; locals не вычисляются повторно | Утверждено пользователем; один начальный `result`, без новых настроек и маркеров; раздел 8 |
| Вход конвенции и `Auto()` | Всегда source текущей пары; выражение перед switch выбирает явную ветку, включая `result`, кортеж и `Normalize(source)` | Утверждено пользователем; раздел 5. Не подменять пару, не вычислять `result` конвенцией заранее |
| Числовая строка | `ByValue` и `ByValueAllowUndefined` дают invariant decimal с точной знаковостью/шириной, включая неназванные числа; flags форматируют всю маску | Оба режима утверждены пользователем; у строки нет набора enum-констант; раздел 7 |
| Два числовых режима | Для обычных enum `ByValue` требует объявленного destination-значения с тем же числом, без требования к source; `ByValueAllowUndefined` сохраняет и неназванные числа в диапазоне destination | Имена и граница строгости утверждены пользователем; единица числового пути flags определяется режимом; разделы 4 и 7 |
| Сохранение числа | Оба числовых режима не теряют биты, не меняют знак и не оборачивают число при переполнении; явные C# casts сохраняют свою семантику | Конвенция ищет то же математическое число; раздел 7 |
| Выбор реакции на переполнение | Отсутствие конвенционного соответствия; `Members` выбирает fallback либо throw, в том числе отдельным range guard; явный `Auto()` при неудаче бросает | Утверждено пользователем; без `EnumOverflowHandling`; раздел 7 |
| Одинаковые enum-типы | `E -> E` соблюдает выбранную стратегию, приоритет явных правил и `MemberSelection`; безусловное identity не обходит строгость | Утверждено пользователем; раздел 8 |
| Проверка покрытия | Существующий `UnmappedMemberValidation`, default `None`, warnings; доказанная неполнота отличается от невозможности доказать покрытие | Продолжает существующую настройку; без выполнения runtime callbacks при генерации; раздел 6 |
| Доступность `result` | Только реально выбранное начальное значение; диагностика недоступного чтения по пути исполнения, без ошибки за неиспользуемый параметр | Общий lifecycle и прежние четыре формы `Members`; раздел 5 |
| Settings и Using | Using не становится Convert; отсутствие `Members` не делает корректную применимую настройку пары ошибкой и не запускает конвенцию поверх готового значения | Существующая применимость settings и отсутствие нового запрета; раздел 4 |
| Неполный DSL switch | Узкая обработка предупреждений о неполноте только у дополняемой enum DSL декларации; прочие ошибки и ordinary C# diagnostics сохраняются | Уже выбранный короткий `Members` без обязательного `_ => Auto()`; раздел 9 |

Для `result`, типизации marker/delegates и suppressor остаются проверки
интеграции в generator, MSBuild и IDE. Это задачи реализации выбранного
контракта, а не новые пользовательские решения. Успешные isolated probes
не подменяют такие проверки.

### Категория 2: вопросы закрыты

Все ранее вынесенные спорные вопросы обычных enum и flags согласованы,
включая композицию switches через `IncludeBase`, единицу правил, конвенции,
zero/null, применимость режима, порядок битов и границу диагностики.
Каноническая flags-семантика находится в разделе 7; обоснование выбора — в
[исследовании flags](ENUM_MAPPING_FLAGS_RESEARCH.md).
Проверки реализации выбранного контракта остаются необходимыми.

### Границы согласованного объёма

Обратный string-to-enum ByValue не включается автоматически вслед за
числовым output. Это возможное расширение объёма, а не блокирующий вопрос
текущего наброска.

Полный охват наброска: enum-to-enum, aliases, flags, nullable,
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
| Flags modes | Attribute detection, включая nullable и metadata; ByBit default и явный ByMask; независимость от стратегии и обычное наследование режима; один режим всех уровней IncludeBase; единица source/Auto/fallback/вычислений; flags только с одной стороны; неприменимая pair setting и игнорируемая общая; отсутствие смешанного прохода |
| Flags conventions | ByMask + ByName: именованные/неназванные маски, aliases, exact-first и отсутствие сборки по именам битов; строгая OR-допустимость Read/Write, Pair-only, Pair/Audit и All = -1; различие ByBit/ByMask; integer-to-flags; отсутствие скрытой проверки explicit результатов |
| Flags evaluation | Биты от младшего к старшему, signed high bit последним, точная ширина, без повторов aliases; zero с объявленным/необъявленным None, override/Auto/Explicit/fallback; terminal null и отсутствие effects следующих битов; неизвестные биты; диапазон при signed -1 и разных ширинах; доказанно недостижимые composite cases и достижимые patterns над result/вычисленным выражением, включая изменение source в locals/guards |
| Numeric | Строгий ByValue и ослабленный режим; unnamed source при declared destination; неизвестное destination-число; explicit checked/unchecked casts; отсутствие усечения и изменения знака в обоих режимах; fallback/throw при overflow и смешанный guard; same-type с учётом стратегии |
| Integer-to-enum | Default ByValue; оба числовых режима; объявленное/неназванное число и выход из диапазона; pair-level ByName diagnostic; общий ByName и отсутствие повторного поиска, включая нижний ByValueAllowUndefined; inherited numeric mode и явный ByValue вместо Default; Members/Auto/Explicit/null guards; enum-to-integer без стратегии |
| Enum-to-string | ByName/ByValue/ByValueAllowUndefined; одинаковый числовой output двух режимов, включая неизвестные числа; Members overrides; Auto/Explicit и явный Auto; aliases; signed/ulong; invariant culture; flags whole-mask output; nullable |
| Lifecycle | Все null policies; nullable exact pairs; Update без Create; same-type mapping; обычный explicit nested Map |
| Runtime callbacks | Using null guards; ConstructUsing без/с previous, включая enum zero; ResolveUsing на обеих операциях; terminal null пропускает Members; whole-mask flags; method groups/delegates/context; без Members выбранное значение окончательно, корректная применимая strategy setting допустима |
| Composition | IncludeBase через несколько уровней; локальный fallback сохраняет базовые special cases, отсутствие локального наследует базовый; false guard продолжает поиск для той же константы; уровень важнее специфичности pattern; case Auto против завершающего Auto; общая effective конвенция и Explicit; порядок guards/locals, ленивый вход в базу и отсутствие повторного вычисления при fallback; settings origin; конфликт с Convert; допустимые Members+Using локально и после IncludeBase; один начальный result и окончательный результат выбранной ветки; эффекты неиспользованной фабрики; result не flags-накопитель |
| API surface | Единый enum Members, без Values/Flags; прежние четыре формы с result/context; диагностика недоступного result, но не неиспользуемого параметра; отсутствие Construct/Resolve; сохранение Using; enum/string/integer/nullable; прежняя композиция object/tuple |
| Compiler/generator | C# 9; точечные suppressions и warnings-as-errors; nested switch; wrong types; obsolete; edit settings/enum/callback; cancellation/recovery |

Изменён только внутренний дизайн. Production API, generator и постоянные
тесты enum feature не реализованы. Общие contracts и публичные settings
документы пока описывают только действующее поведение.
