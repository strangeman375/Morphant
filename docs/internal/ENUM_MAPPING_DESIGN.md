# First-class enum mapping: исследование и набросок дизайна

Дата: 2026-09-21. Статус: предложение для обсуждения, не принятый контракт и
не обещание реализации. Названия нового API предварительные. Исходная точка:
Morphant 0.5.0, remote `main` `b77d654d8d255bb89c04f9b9c69cbb1a56c7d7c5`.

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

## 3. Предлагаемая семантика

Четыре решения должны быть независимы: как сопоставлять значения, какие
объявленные случаи обязаны иметь соответствие, какие числа допустимы во
время выполнения и что делать при отсутствии результата.

| Вопрос | Предлагаемый default для разных обычных enum |
|---|---|
| Matching | Точное имя, ordinal и с учётом регистра |
| Явные соответствия | Приоритет над конвенцией; many-to-one допустим |
| Полнота | Все различные объявленные значения source должны быть покрыты; отсутствие — error |
| Лишние значения destination | Допустимы; проверка обеих сторон включается отдельно |
| Неизвестное runtime-значение | Типизированное исключение Morphant; явный fallback может заменить его |
| Ноль | Обычное значение, не неявный fallback и не null |
| Update | Вычислить и вернуть новое скалярное значение, как Create после общих null guards |
| Обратное направление | Отдельная регистрация; автоматический reverse не добавляется |

Почему имя: `Active = 1` и `Active = 10` часто означают одно состояние в
разных моделях. Совпадение чисел при разных именах не даёт такой гарантии.
Это выбор для Morphant, а не универсальное преимущество над числовым mapping.
Для контрактов со стабильными числовыми кодами нужен явный `ByValue`.

### Три режима

- `ByName`: таблица по именам плюс overrides. Никакого числового fallback.
- `ByValue`: сохраняет математическое целочисленное значение; по умолчанию
  проверяет допустимость destination. Неизвестное в source число может
  пройти, если такое число допустимо в destination. Отдельная настройка
  разрешает сохранять и неизвестные destination-значения.
- `Explicit`: только записанные соответствия; конвенция отключена.

Переполнение при числовом преобразовании — исключение, без усечения битов и
без зависимости от `CheckForOverflowUnderflow` проекта. Оно не является
обычным отсутствием enum-соответствия и не поглощается fallback. Для
намеренного unchecked cast остаётся `Convert`. Matching по именам вообще
не требует приведения числового значения source к типу destination.

Ненастроенную пару `E -> E` предлагается считать identity и сохранять любое
значение, как при обычном присваивании такого member. Явные enum rules или
enum-настройки, в том числе унаследованные, включают обычный enum algorithm.
Для запроса валидации достаточно явно выбрать стратегию. Это отдельное
решение для обсуждения: альтернатива — валидировать даже identity mapping.

## 4. Набросок DSL

Все новые имена ниже предварительные; примеры не компилируются текущим
Morphant. Используется существующая форма `Configure(MapperBuilder builder)`.

```csharp
public enum DomainStatus
{
    Pending = 1,
    Active = 2,
    Cancelled = 3
}

public enum ApiStatus
{
    Unknown = 0,
    Pending = 10,
    Active = 20,
    Deleted = 30
}

public partial class StatusMapper : TypeMapper<StatusMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<DomainStatus, ApiStatus>()
            .MapValue(DomainStatus.Cancelled, ApiStatus.Deleted)
            .FallbackValue(ApiStatus.Unknown);
    }
}
```

`Pending` и `Active` сопоставляются автоматически. `(DomainStatus)42`
возвращает `Unknown`. Добавление `DomainStatus.Paused` без соответствия
приводит к compile-time error, несмотря на fallback.

Минимальная общая поверхность:

| Элемент | Назначение |
|---|---|
| `EnumMappingStrategy(Default / ByName / ByValue / Explicit)` | Выбор алгоритма; effective default — `ByName` для разных enum |
| `MapValue(source, destination)` | Типизированное соответствие compile-time constants |
| `FallbackValue(destination)` | Результат для неразрешённого non-null входа; отсутствие означает throw |
| `RejectValue(source)` | Явно запрещённый вход: throw даже при fallback; считается обработанным для source coverage |
| `UnmappedEnumValueValidation(Default / None / Source / Destination / Strict)` | Compile-time coverage; default — `Source` для enum-to-enum |
| `EnumValueValidation(Default / Defined / None)` | Проверка destination у числового преобразования; default — `Defined` |

Формально в первой строке и двух последних строках записаны методы настройки
и значения одноимённых enum, как в существующем API. Во всех настройках
`Default` продолжает обычную цепочку precedence.

Отдельная `UnmappedEnumValueValidation` позволяет сделать source coverage
строгим, сохранив текущий default `None` у object-member validation.
Расширять смысл существующей настройки незаметно для object mappings не нужно.

```csharp
// Числовой контракт с проверкой destination.
builder.Map<WireStatus, StorageStatus>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValue);

// Открытый числовой контракт: неизвестные значения сохраняются,
// пока помещаются в underlying type destination.
builder.Map<WireCode, StoredCode>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValue)
    .EnumValueValidation(EnumValueValidation.None);

// Намеренно неполная таблица с общим fallback.
builder.Map<LegacyStatus, ApiStatus>()
    .EnumMappingStrategy(EnumMappingStrategy.Explicit)
    .UnmappedEnumValueValidation(UnmappedEnumValueValidation.None)
    .MapValue(LegacyStatus.Ready, ApiStatus.Active)
    .RejectValue(LegacyStatus.Corrupt)
    .FallbackValue(ApiStatus.Unknown);
```

Не нужен отдельный `UnknownEnumValueHandling` с теми же комбинациями.
`IgnoreValue` тоже не нужен: скалярный mapping должен вернуть значение или
бросить исключение. Сохранение предыдущего destination относится к
пользовательскому алгоритму через `Convert`, а не к enum-конвенции.

Альтернатива — новый `.Values(source => source switch { ... _ => Auto() })`.
Она привычнее при большом количестве cases, но потребует отдельного
declarative callback и правил разбора его ветвлений. Для константной таблицы
предпочтителен `MapValue`: меньше новой семантики и generated API. Обычный
switch уже доступен через `Convert`; добавлять туда `Auto()` нельзя без
изменения существующего контракта ordinary C# callback.

### Композиция и конфликты

- Rules принимают константы подходящих типов; произвольные вычисления
  остаются в `Convert`. Enum-значения и строки получают обычный IntelliSense.
  Constant casts позволяют явно задать неназванное source/destination
  значение. Explicit результаты и fallback авторитетны: `Defined`
  проверяет автоматическое числовое преобразование, а не переписывает
  намеренно заданный результат. Null source-key диагностируется, поскольку
  null обрабатывается раньше таблицы существующей null policy.
- Совпадающие повторные rules допустимы. Два разных результата либо
  `MapValue` и `RejectValue` для одного ключа на одном уровне — diagnostic,
  без зависимости от порядка регистрации. Source-ключ enum — число,
  поэтому это распространяется и на aliases.
- При same-pair inheritance локальный rule заменяет inherited rule с тем же
  ключом; локальный fallback заменяет inherited fallback. Правила разных
  enum-пар не переносятся между ними по сходству имён.
- Общие настройки имеют существующую precedence. `MapValue`, `RejectValue`
  и `FallbackValue` принадлежат конкретной паре.
- Настройки enum не должны менять алгоритм уже настроенного `Convert`,
  `ConstructUsing` или `ResolveUsing`. Их существующая семантика сохраняется.
  Явное смешивание альтернативных algorithms на одной паре диагностируется;
  неприменимые inherited defaults игнорируются по общим правилам.
- `EnumValueValidation.None` применим к числовому алгоритму; он не создаёт
  числовой fallback у `ByName` и не снимает конфликты таблицы. Явное
  неприменимое сочетание следует диагностировать.

## 5. Полнота и неизвестные значения

Compile-time coverage относится к конечному набору объявленных значений
enum, runtime-проверка — к реально пришедшему значению underlying type.
Это разные множества. Отключение coverage не отключает runtime-проверку.

Fallback намеренно не закрывает source coverage: иначе новый член внешнего
enum незаметно превратится в `Unknown`. Для намеренного catch-all достаточно
`UnmappedEnumValueValidation.None`. `RejectValue` позволяет осознанно
запретить конкретный объявленный вариант без отключения всей проверки.

Source coverage учитывает overrides, конвенцию и явные rejects; aliases
считаются одним значением. Destination coverage проверяет достижимость
различных значений; явно заданный fallback делает свой результат достижимым.
Наличие нескольких source для одного destination не является ошибкой.
В открытом `ByValue` числовое преобразование само покрывает представимые
source-значения: destination не обязан объявлять их имена.

При строковой или числовой стороне coverage проверяет только конечную
enum-сторону; бесконечное множество строк/чисел не объявляется «полностью
проверенным». Неприменимая сторона настройки не добавляет проверок.

## 6. Aliases и неоднозначность

```csharp
enum SourceState { Ready = 1, Active = 1 }
enum TargetState { Ready = 10, Active = 20 }
```

`SourceState.Ready` и `SourceState.Active` неразличимы во время выполнения.
Поэтому `ByName` здесь должен дать diagnostic, а не выбрать первое поле или
породить повторные switch arms. Один `MapValue` выбирает результат для всей
группы source aliases. Если оба имени destination имеют одно число,
неоднозначности enum-to-enum нет. Отсутствующее соответствие одному alias
также не ошибка, если другой alias однозначно определил результат группы.

Для `enum -> string` несколько имён одного числа требуют явного выходного
имени через `MapValue`. Для `string -> enum` все такие входные имена могут
быть допустимы. Порядок объявления не выбирает каноническое имя. Даже
[Enum.GetName](https://learn.microsoft.com/en-us/dotnet/api/system.enum.getname?view=net-10.0)
не гарантирует конкретное имя для дублирующегося значения.

При добавлении case-insensitive matching нужны ordinal comparison и
compile-time collision checks. `Read` и `READ`, ведущие к разным результатам,
нельзя разрешать порядком. В первом этапе достаточно точного регистра.

## 7. Flags

Для двух `[Flags]` enum в `ByName` нужна композиция атомарных флагов:

```csharp
[Flags]
enum SourceAccess { None = 0, Read = 1, Write = 2 }

[Flags]
enum TargetAccess { None = 0, Read = 8, Write = 16 }
```

`Read | Write` должен дать `8 | 16`, хотя отдельная константа со значением
`3` или `24` не объявлена. Простого cast или switch по объявленным полям
недостаточно. [Enum.IsDefined](https://learn.microsoft.com/en-us/dotnet/api/system.enum.isdefined?view=net-10.0)
также не проверяет произвольную допустимую комбинацию флагов.

Предлагаемые правила:

1. Явное соответствие/запрет всего входного значения проверяется первым.
   Это позволяет задать исключение для composite или sentinel, включая
   `All = -1`. Такой rule не применяется к совпавшей части большей маски.
2. Иначе каждый установленный одноразрядный source-флаг переводится по
   имени либо своему explicit rule; результаты объединяются через OR.
   Явный single-bit rule участвует и в составе комбинации. Reject такого
   бита запрещает любую композицию с ним, если нет explicit whole-value rule.
3. Явное направление бита в `0` означает намеренное удаление этого флага;
   направление в несколько destination-битов допустимо. Неизвестные или
   непереводимые биты не отбрасываются автоматически: весь вход даёт
   fallback/throw, без частичного успешного результата.
4. Ноль переводится в ноль, даже без имени `None`; explicit whole-value rule
   для нуля может это изменить. Он не добавляется к ненулевым комбинациям.
5. Объявленный composite, который раскладывается на известные биты,
   покрывается их композицией. Если одноимённый destination composite
   противоречит результату, нужна диагностика и явное решение. Если source
   composite не разложим, нужен explicit whole-value rule либо reject.
6. Не составлять разрешённую маску через OR всех объявленных констант:
   `All = -1` иначе разрешит любые неизвестные биты. Атомарные биты
   определяются в точной разрядности underlying type, включая старший бит
   signed enum. Совпадение с явно объявленным sentinel — отдельный случай.

Для `ByValue` сохраняется число, включая расположение битов: пользователь
сам выбирает такой контракт. При `Defined` destination допускает ноль,
точно объявленное значение или комбинацию объявленных одноразрядных битов.
Объявление только `Pair = 3` само по себе не разрешает значения `1` и `2`.

При `[Flags]` только с одной стороны `ByName` не должен угадывать смысл:
diagnostic с предложением `Explicit`, осознанного `ByValue` или `Convert`.
В `Explicit` flags рассматриваются как целые значения без автоматической
декомпозиции. Many-to-one mappings битов допустимы, но не обратимы.

Это наиболее сложная часть предложения. Альтернатива для меньшего первого
этапа — временно поддержать flags только в `ByValue`/`Explicit`; она не даёт
полноценного переноса flags между моделями с разными расположениями битов.

## 8. Строки, числа и nullable

| Пара | Предлагаемое поведение |
|---|---|
| Обычный `enum -> string` | CLR-имя или explicit строка; неизвестное значение — fallback/throw |
| `string ->` обычный enum | Точное CLR-имя или explicit входной alias; неизвестная строка — fallback/throw |
| `enum ->` целое число | Сохранить число, включая неназванное; проверить диапазон destination |
| Целое число `-> enum` | Проверить диапазон, затем допустимость enum; можно явно отключить последнюю проверку |
| Nullable варианты | Сначала существующие null policies, затем тот же алгоритм для non-null значения |

Естественная стратегия определяется формой пары: для enum/string это
`ByName`, для enum/число — `ByValue`. `Explicit` доступен во всех этих
направлениях. Явно неподходящая стратегия диагностируется; inherited
defaults учитываются только там, где они применимы.

Для целых чисел в первой версии достаточно `sbyte`, `byte`, `short`,
`ushort`, `int`, `uint`, `long`, `ulong`. Float, decimal, char, bool,
native-sized integers и числовые строки не включаются автоматически.

Строки по умолчанию не trim-ятся; `"1"`, пустая строка и пробелы не
превращаются в число или `default`. Их можно обработать explicit rule.
`null` идёт через `NullSourceHandling`, не через `FallbackValue`.
Fallback и explicit null-результат разрешены только при nullable destination.
Настройка enum-validation не пересматривает результат null policy:
`ReturnNull` для non-nullable enum по-прежнему возвращает ноль.

Пары `E -> F`, `E? -> F` и `E? -> F?` сохраняют существующую точную
идентичность регистрации. Не добавлять неявный поиск mapper для underlying
типов или автоматическую регистрацию nullable-пар. Внутри генератора
алгоритм можно строить по non-null enum shape.

Направления независимы, например:

```csharp
builder.Map<ApiStatus, string>()
    .MapValue(ApiStatus.Deleted, "removed");

builder.Map<string, ApiStatus>()
    .MapValue("removed", ApiStatus.Deleted)
    .MapValue("deleted", ApiStatus.Deleted);
```

Входные aliases добавляются к обычным именам; `Explicit` позволяет запретить
обычные имена. Автоматический `ReverseMap` здесь потерял бы выбор одного
выходного представления.

Flags/string требует дополнительного контракта: разделитель, порядок,
composite names, aliases, пустое множество, неизвестные биты. Предлагается
отложить автоматический parse/format комбинаций; для такой пары доступен
`Explicit` по целым значениям или `Convert`. Не выдавать обычный enum/string
algorithm за полную поддержку flags/string.

`EnumMember`, `JsonStringEnumMemberName`, `Description`, naming policies и
prefix/suffix transformations — последующее расширение после ядра.
По умолчанию сторонние атрибуты не меняют mapping: CLR-имя и wire name
являются разными контрактами. `Description` может быть вообще текстом UI.

## 9. Встраивание в Morphant

Вложенный вызов остаётся явным:

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .MapValue(DomainStatus.Cancelled, ApiStatus.Deleted);

builder.Map<Order, OrderDto>()
    .Members((source, _) => new()
    {
        Status = Map(source.Status)
    });
```

`Map()` без аргумента также может вывести source по имени member. Это тот
же registered mapping и тот же механизм DI/context. Автоматические
enum conversions в `Auto()` были бы отдельным изменением текущего контракта;
они не предлагаются как скрытая часть feature. Для get-only value member
скалярный результат по-прежнему нельзя присвоить обратно.

В реализации потребуются следующие локальные изменения:

- Описание enum из Roslyn: underlying type, flags, поля/числа, группы aliases
  и locations. Не сводить все значения к `int` или signed `long`.
- Отдельный scalar enum plan рядом с существующими manual/result/object
  путями. Использовать существующие guards, contracts, operation gating и
  exception stubs. Старые explicit callback paths сохраняют приоритет.
- Типизированные pair extensions для rules по действующей схеме генерации.
  Не создавать `EnumConstruction`/`EnumMembers` и не менять `IMapper`.
  Open type parameter `T : Enum` не даёт конечного набора полей: для него
  нужен `Convert` или diagnostic, без runtime reflection.
- Зависимости incremental-моделей должны включать оба enum, значения
  констант, aliases, `[Flags]`, rules и settings. Проверить source и metadata
  types, не считать имеющийся анализ default constants достаточным.
- Добавить enum diagnostics в существующую изоляцию ошибок и compatibility
  manifest для нового runtime API. Не резервировать номера диагностик до
  реализации. Конфликт правил — configuration error; неизвестный вход —
  отдельная runtime-ошибка со значением и типами.

Ожидаемая форма обычного алгоритма из раздела 4, без interface/null обвязки:

```csharp
return source switch
{
    DomainStatus.Pending => ApiStatus.Pending,
    DomainStatus.Active => ApiStatus.Active,
    DomainStatus.Cancelled => ApiStatus.Deleted,
    _ => ApiStatus.Unknown
};
```

Flags требуют необходимых проверок маски и аккумулятора. На обычном enum
не нужны dictionary, reflection, `Enum.Parse`/`Enum.ToString`, boxing или
создание локальной переменной для каждого case. Общий алгоритм Create/Update
можно разделить через короткий typed helper, если это убирает дублирование.
Переиспользование не должно менять порядок пользовательских вычислений.

## 10. Предлагаемая граница и проверка

После согласования удобно реализовать три проверяемых этапа:

1. Обычные enum-to-enum: стратегии, constants, aliases, coverage, fallback,
   rejects, nullable, Create/Update и explicit nested calls.
2. Flags-to-flags с описанной семантикой; все восемь underlying types,
   signed/unsigned и overflow. Числовые enum conversions используют те же
   правила диапазонов и допустимых destination-значений.
3. Обычные enum/string conversions с явной канонизацией aliases.

Эти этапы составляют предлагаемую первую версию feature. Ignore-case,
автоматический flags text format, wire attributes и name transformations
оставляются за её пределами. Не расширять в этой работе collections,
projection, reverse mapping, runtime reflection или result-based `TryMap`.

До реализации нужно согласовать defaults, итоговые имена API и сложность
flags. В частности, спорны `Source` error вместо warning, независимость
fallback от coverage, identity-пара и сохранение только явных nested calls.

Будущее покрытие должно проверять поведение и полный generated source:

| Группа | Существенные сценарии |
|---|---|
| Matching | Одинаковые имена/разные числа; одинаковые числа/разные имена; overrides; Explicit; many-to-one |
| Эволюция | Добавление source/target member; fallback не скрывает source gap; выключение coverage не выключает runtime guard |
| Aliases | Однозначная группа; разные target-числа; повторный explicit rule; canonical string; изменение порядка деклараций |
| Flags | Перестановка битов; неназванная комбинация; zero; composite override; partial unknown; reject внутри маски; `All = -1`; signed high bit |
| Числа | Границы восьми underlying types; отрицательное в unsigned; `ulong` выше `long.MaxValue`; overflow при checked и unchecked consumer |
| Строки | CLR-имена, несколько aliases, пустая/числовая строка, пробелы, null, неоднозначный enum-to-string |
| Жизненный цикл | Create/Update; null policies; nullable-пары; вложенные constructor/member/tuple paths; get-only scalar |
| Композиция | Inherited rules/settings; Convert/Using; разные mapper scopes; независимая пара при ошибке другой |
| Генератор | C# 9; metadata enums; incrementality при изменении имён/чисел/Flags; cancellation, failure/recovery; obsolete diagnostics |

Сейчас изменён только этот внутренний документ. Реализация, публичная
документация и тестовые ожидания не меняются до обсуждения предложения.
