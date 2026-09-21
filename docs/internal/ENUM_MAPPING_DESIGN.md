# First-class enum mapping: исследование и набросок дизайна

Дата: 2026-09-21. Статус: дизайн для обсуждения, не обещание реализации.
Редакция 3: enum DSL согласуется с существующей системой настроек.
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

Предыдущая редакция ошибочно делала наличие `Auto()` переключателем
автоматического mapping и объявляла switch без него полностью ручным.
Отсюда появились лишние `Auto(fallback: ...)`, отдельная настройка покрытия
и собственные defaults. Они удалены из предлагаемого API.

При этом фраза «`_ => Unknown` обрабатывает остаток» требует точности:
остаток после **явных правил** и остаток после **явных правил и конвенции**
различаются. Раздел 5 предлагает второй смысл. Это уточнение для обсуждения,
а не уже полученное согласие пользователя на изменение приоритета catch-all.

## 4. Существующие настройки — основа enum mapping

Проверены не только названия settings, но и
[resolver](../../src/Morphant.Generator/Settings/MappingSettings.cs),
[setting diagnostics](../../src/Morphant.Generator/Settings/MappingSettingsDiagnosticPipeline.cs),
[member-selection scenarios](../../src/tests/Morphant.Generator.IntegrationTests/TypeMapperMemberTests/MemberSelectionTests.cs)
и [inheritance scenarios](../../src/tests/Morphant.Generator.IntegrationTests/TypeMapperInheritanceTests/SettingsCompositionTests.cs).
Enum-поведение в правом столбце — предлагаемое расширение; feature ещё нет.

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
`Values`/`Flags` нельзя смешивать с локальным `Convert` или factory algorithm.
Нельзя приписать обычному `Convert` новую семантику неявных enum-веток.

### Какие новые настройки действительно нужны

Предлагаются только политики, для которых нет действующего аналога:

- `EnumMappingStrategy`: `Default`, `ByName`, `ByValue`. Предлагаемый default
  для разных enum — `ByName`, exact ordinal; для enum/integer — числовая
  конвенция, для enum/string — имена. Применимость значения проверяется по
  форме пары: `ByName` не превращает enum/integer в строковое преобразование.
- `EnumValueValidation`: `Default`, `Defined`, `None`; относится к числовой
  конвенции с enum destination. Предлагаемый default — `Defined`.
  Это runtime-допустимость числа, а не compile-time-покрытие.

Обе используют обычные pair/mapper/MSBuild уровни и общий resolver, без
нового уровня named arguments на `Auto()`:

```csharp
builder.Map<WireCode, StoredCode>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValue)
    .EnumValueValidation(EnumValueValidation.None);
```

Новые enum defaults на mapper/assembly могут сосуществовать с object и
`Convert` mappings. Они там не используются; явную настройку на паре,
к которой она принципиально неприменима, следует диагностировать по общему
контракту `MORPH0023`. Отсутствие достижимого автоматического пути само по
себе не делает корректно заданную стратегию ошибкой.

Отдельных `UnmappedEnumValueValidation`, `UnknownEnumValueHandling`,
`FallbackValue`, нового enum-режима `Explicit` и `Auto(fallback: ...)` нет.

## 5. Switch задаёт правила, Morphant дополняет их конвенцией

Рабочее имя callback — `Values`. Он декларативный, как `Members`; его
содержимое не вызывается как обычный runtime delegate из `Configure`.

Минимальный сценарий не требует `Auto()` или завершающей ветки:

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Values(status => status switch
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

### Предлагаемый приоритет завершающей ветки

1. Явные специальные ветки в написанном порядке.
2. Оставшиеся inherited explicit rules, если подключён `IncludeBase`.
3. Неявная конвенция, если effective selection — `Auto`.
4. Завершающая ветка; при её отсутствии — mapping exception.

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Values(status => status switch
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

Именно результат для `Active` отличает предложение от прошлой редакции.
`_ => Unknown` становится обычным способом выразить fallback после
конвенции. Вычисляемое `_ => ResolveUnknown(status)` выполняется только
для этого остатка, один раз; дополнительный lazy API не требуется.

Альтернатива — сохранить буквальный C# catch-all: тогда `Active` сразу
получит `Unknown`, а конвенция сможет дополнить только switch **без**
catch-all. Эта модель тоже согласуется с автоматическим дополнением
пропущенных случаев, но для «конвенция, затем fallback» снова понадобится
дополнительное выражение/API. Поэтому рекомендуется первая модель.
Выбор должен быть подтверждён до реализации.

### `Auto()` остаётся явным запросом

Существующий смысл `Auto()` сохраняется: явно применить конвенцию для
выбранного случая, даже при `MemberSelection.Explicit`.

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .MemberSelection(MemberSelection.Explicit)
    .Values(status => status switch
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

Дополняется только декларативный mapping switch над входным значением
`Values`/`Flags`. Вложенный switch справа от `=>`, чужой метод и callback
`Convert` сохраняют обычную C# семантику и не получают конвенционные ветки.
`Values(_ => Auto())` — явная конвенция для всего входа; прямое выражение
`Values(source => Compute(source))` — явный результат для всего входа.

Финальный `_ => expression` или `var remaining => expression` без guard
предлагается трактовать как завершающее правило. Ветка с `when`, в том
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
Governing value вычисляется один раз: если guard меняет переменную source,
синтезированные ветки используют уже выбранный вход switch. `Auto()` в
такой ветке относится к этому входу, а внутри `Flags` — к текущему биту.
Обычные выражения по-прежнему видят пользовательские изменения переменных.

Block lambda с подготовкой локальных значений и возвращаемым mapping switch
может следовать существующим declarative statement boundaries. Не
дополнять все встреченные switch механически и не анализировать тела
пользовательских методов. Method group и полный imperative algorithm
остаются у `Convert`.

## 6. Проверка покрытия через `UnmappedMemberValidation`

Проверяется итоговая композиция явных правил, наследования, конвенции и
завершающей ветки. Режим выбора правил и режим диагностики независимы:

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .MemberSelection(MemberSelection.Auto)
    .UnmappedMemberValidation(UnmappedMemberValidation.Source)
    .Values(status => status switch
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

Guard не доказывает покрытие целого значения: нужен также путь с false.
Для динамического результата известен факт source-обработки, но часто
неизвестен набор destination-значений. Если запрошенную проверку нельзя
доказать, предупреждение должно говорить об этой границе анализа, а не
ошибочно утверждать, что конкретный результат невозможен. Не превращать
анализ в интерпретатор C# и не исполнять методы при генерации.

Для string/integer проверяется только конечная enum-сторона. Неназванные
enum-числа относятся к runtime mapping, а не к объявленному source coverage.
Для flags проверка опирается на атомарные правила и явно объявленные
композиты; не перечислять все комбинации маски.

## 7. Flags, aliases, строки и числа

### Flags: отдельно целое значение и каждый установленный бит

`Values` обрабатывает целый вход. `Flags` — предварительное имя декларации
перевода single-bit значений с объединением результатов через OR:

```csharp
builder.Map<SourceAccess, TargetAccess>()
    .Values(value => value switch
    {
        SourceAccess.All => TargetAccess.All,
        _ => TargetAccess.Unknown
    })
    .Flags(flag => flag switch
    {
        SourceAccess.Read => TargetAccess.View,
        SourceAccess.Write => TargetAccess.Edit,
        SourceAccess.Delete => TargetAccess.None
    });
```

При default `Auto` сначала действует whole-value override. Остальная маска
переводится по битам: explicit per-bit overrides, затем одноимённые биты по
конвенции. Если вся маска не имеет результата, выполняется whole-value
fallback. Отдельный fallback в `Flags` относится к одному биту, а не к маске.
`Read | Write` становится `View | Edit`; whole-value `Read => ...` сам по
себе не срабатывает на часть `Read | Write`.

`MemberSelection.Explicit` отключает неявное сопоставление и целых значений,
и отдельных битов. Явно заданный `Flags` всё ещё задаёт per-bit algorithm;
он работает для комбинаций его explicit rules и не добавляет отсутствующие
соответствия по имени. Внутренний `Auto()` явно разрешает конвенцию своему
биту. Whole-value `Auto()` явно разрешает автоматический перевод текущей
маски, включая конвенцию для неописанных битов при `Explicit`, с учётом
per-bit overrides. Это расширение областей действия требует
отдельных сценариев перед реализацией; нельзя свести `Explicit` к проверке
наличия `Values`.

- Каждый установленный объявленный single-bit обрабатывается один раз в
  порядке возрастания номера бита, signed high bit — последним.
- Нулевой вход при активном per-bit algorithm даёт ноль без вызова callback;
  `Values` может задать другой результат. Если не выбрана ни конвенция,
  ни explicit per-bit algorithm, zero не получает особой поблажки.
- Неизвестные биты проверяются до per-bit callbacks. Не терять их молча.
  Неполный implicit per-bit mapping означает отсутствие результата всей
  маски; whole-value fallback может обработать его. Явные `Auto()` с
  неудачным соответствием и пользовательские `throw` — исключения,
  а не сигнал попробовать fallback.
- Уже выполненные вычисления для прежних битов не откатываются при неудаче
  более позднего implicit mapping. Не применять catch-all вокруг callback.
- Composite names не являются атомарными битами и не переопределяют per-bit
  правила. Особые composites описываются в `Values`. `All = -1` не делает
  все неизвестные биты автоматически допустимыми.
- При `ByValue` сохраняется число без перестановки битов. `Flags` как
  per-bit декларация к этому алгоритму неприменим; не игнорировать её молча.
- При `[Flags]` только с одной стороны `ByName` не угадывает преобразование.
  Нужны явные правила, осознанный `ByValue` или `Convert`.

Окончательная форма `Flags` ещё не согласована. Разделение двух уровней
нужно сохранить даже при выборе другого синтаксиса.

### Aliases и строки

```csharp
enum SourceState { Ready = 1, Active = 1 }
enum TargetState { Ready = 10, Active = 20 }
```

Source aliases runtime-неразличимы. На автоматическом пути этот пример
неоднозначен; нужна диагностика. Явная ветка для числа `1` решает конфликт
для обоих имён. Не обходить обычные C# ошибки повторных/недостижимых веток.

Для enum-to-string canonical output задаётся явно при нескольких именах
одного числа; порядок объявления не выражает намерение.
[Enum.GetName](https://learn.microsoft.com/en-us/dotnet/api/system.enum.getname?view=net-10.0)
не гарантирует выбор конкретного alias.

```csharp
builder.Map<ApiStatus, string>()
    .Values(status => status switch
    {
        ApiStatus.Deleted => "removed"
    });

builder.Map<string, ApiStatus>()
    .Values(text => text switch
    {
        "removed" or "deleted" => ApiStatus.Deleted,
        "pending" or "queued" => ApiStatus.Pending,
        _ => ApiStatus.Unknown
    });
```

Остальные CLR-имена сопоставляются автоматически при `Auto`; направления
независимы. Ignore-case, wire attributes и naming policies не вводятся в
первую версию. Можно использовать обычные guards/expressions. Пустая,
числовая строка и пробелы не получают особого смысла. Автоматический
flags/string parser/formatter пока за границей; ручной код остаётся возможен.

### Числовая конвенция

Поддерживаемые целые типы: `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`,
`long`, `ulong`. `ByValue` сохраняет математическое число с проверкой
диапазона destination underlying type независимо от checked options
consumer. Overflow не считается отсутствием соответствия для fallback.

`EnumValueValidation.Defined` проверяет destination, `None` допускает
неназванное значение; в обоих случаях запрещено усечение. Source может
быть неназванным, если число допустимо в destination. Enum-to-integer
сохраняет и неназванные значения в пределах диапазона.

Для flags допустимы zero, точно объявленное значение и комбинации
объявленных single-bit значений. Одно `Pair = 3` не разрешает `1` и `2`.
[Enum.IsDefined](https://learn.microsoft.com/en-us/dotnet/api/system.enum.isdefined?view=net-10.0)
не эквивалентен проверке произвольной допустимой flags-комбинации.
Явные C# results/casts не получают скрытой enum validation.

## 8. Lifecycle, inheritance и вложенное использование

`Values` получает non-null source после общей null policy. Nullable
registration остаётся точной: не искать автоматически underlying pair.
`ReturnNull` при non-nullable enum destination по текущему контракту даёт
`default`, даже если ноль не объявлен. Это путь null policy, не конвенции.
Возврат null из правила допустим по nullable destination contract.

```csharp
builder.Map<DomainStatus?, ApiStatus?>()
    .Values(status => status switch
    {
        DomainStatus.Missing => null,
        DomainStatus.Cancelled => ApiStatus.Deleted
    });
```

`Create`/`Update` вычисляют scalar result после обычных guards. Возможны
перегрузки с `previous` и `context` по существующей форме declarative
callbacks; нельзя повторно менять operation при null destination.

Для `E -> E` identity предлагается как автоматический путь, а не обход
настроек: explicit overrides выполняются первыми, `Explicit` отключает
неявный identity. Допустимость неназванных чисел на same-type automatic
пути и связь с `EnumValueValidation` ещё требуют выбора; прежнее обещание
безусловного identity до настроек снято.

### `IncludeBase`

Настройки наследуются уже описанным способом. Для правил рекомендуется
развить существующий принцип: local explicit cases приоритетнее included,
непереопределённые included cases сохраняются. `Explicit` не отменяет
наследованные explicit rules. Сам факт нового `Values` не должен стирать
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
Не превращать inherited `Convert`/factory в набор enum rules. Сохраняются
текущие ограничения доступности helper-методов и cross-assembly inheritance.

### Nested mapping

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Values(status => status switch
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
mapping. `Members.Auto()` требует implicit C# conversion и не запускает
вложенную пару. Same-enum property может копироваться как прежде; разные
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

При реализации suppressor должен узнавать настоящий DSL symbol и только
switch, который planner действительно дополняет. Не подавлять warnings
глобально, в `Convert`, `Construct`, `Members` или в произвольном nested
switch. Существующие Morphant switches сохраняют текущую диагностику и
поведение непокрытого входа. Нужны проверки diagnostic family для guards,
отключённых анализаторов и поддерживаемых IDE. В полностью сгенерированном
switch покрытие должно быть явным. Проверка объявленных enum-значений
остаётся обязанностью `UnmappedMemberValidation`.

### Типизация

Предыдущая isolated probe SDK 10.0.100/C# 9 подтвердила binding mixed
switch results через generic compile-time marker с conversions от destination,
`AutoMarker` и `AutoMarker<T>`: десять arms, `or`, guards, throw, вызовы справа,
строки/числа и отдельные `Values`/`Flags`. Это не реализованный generator.

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

Enum shape содержит underlying type, constants/aliases, single-bit mask и
locations. Не приводить `ulong` к `long`. Не создавать construction/member
surfaces, runtime reflection, parsing через `Enum.Parse` или enum boxing.
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
Сложные guards/наследование могут требовать вложенной формы продолжения;
она должна сохранять original governing value и условность вычислений.
Не менять явные независимые evaluations и не вводить local на каждый case.
Внутреннее отсутствие конвенционного результата отличать от пользовательского
exception; fallback не реализуется через `catch` вокруг пользовательского кода.
Сохранить общие failure stubs, settings diagnostics, compatibility manifest,
incrementality, cancellation/recovery и ограничения generated surface.

## 10. Что остаётся согласовать перед реализацией

Принятое направление: C#-подобные декларации; конвенция по умолчанию;
отключение неявного подбора через существующий `MemberSelection.Explicit`.

Следующие предложения ещё не утверждены:

1. Приоритет конвенции перед завершающим `_`/`var` и точная граница mapping
   switch. Контрольный пример — результат `Active` в таблице раздела 5.
2. Имя `Values`, форма per-bit декларации `Flags` и композиция обоих уровней
   при `Explicit` и явном `Auto()`.
3. Расширение `UnmappedMemberValidation` на enum с сохранением `None` и warning;
   conservative coverage для guards, dynamic results и flags.
4. Частичное наследование enum rules через `IncludeBase`, без нового marker.
5. Defaults новых strategy/value-validation, same-type identity и полный
   перечень применимости этих двух настроек.
6. Узкая обработка compiler exhaustiveness warnings как часть DSL и её
   поведение в поддерживаемых toolchains.

Объём feature сохраняется: enum-to-enum, aliases, flags, nullable,
enum/integer, ordinary enum/string, неизвестные значения, запреты,
вычисляемые результаты, coverage, inheritance и Create/Update. Автоматический
reverse, flags text format, wire attributes, naming policies, коллекции и
проекции не входят в этот набросок.

Будущие проверки должны защищать поведение, а не только форму API:

| Группа | Существенные сценарии |
|---|---|
| Selection | Bare registration; partial switch без Auto; Auto/Explicit; явный Auto при Explicit; missing result на Create и Update |
| Precedence | Каждый уровень; included pair выше mapper; Default продолжает поиск; последняя запись; независимость порядка Configure |
| Fallback | Именованный override; одноимённый автоматический case; неизвестное значение; computed fallback; пользовательский throw; failed explicit Auto; отсутствие повторного Auto |
| Coverage | None/Source/Destination/Strict; warning severity; covered catch-all; guards; динамический result; aliases; finite/infinite sides |
| Flags | Whole/per-bit; Explicit на обоих уровнях; zero; удаление бита; composites; неизвестные биты; high bit; порядок effects; неудача позднего бита |
| Lifecycle | Все null policies; nullable exact pairs; Update без Create; same-type mapping; обычный explicit nested Map |
| Composition | IncludeBase special cases/fallback; guards и locals из base; settings origin; conflicts с Convert/factories |
| Compiler/generator | C# 9; точечные suppressions и warnings-as-errors; nested switch; wrong types; obsolete; edit settings/enum/callback; cancellation/recovery |

Изменён только внутренний дизайн. Production API, generator и постоянные
тесты enum feature не реализованы. Общие contracts и публичные settings
документы пока описывают только действующее поведение.
