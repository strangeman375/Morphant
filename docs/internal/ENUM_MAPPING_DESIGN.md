# Enum mapping: согласованный дизайн

2026-09-23. Основной контракт согласован; **feature ещё не реализована**.
Уточнения после review и оставшиеся вопросы перечислены в конце документа.
Примеры ниже описывают будущий DSL. Этот документ — канонический контракт;
обоснования и источники: [стратегия по умолчанию](ENUM_MAPPING_DEFAULT_RESEARCH.md),
[числа](ENUM_MAPPING_NUMERIC_RESEARCH.md), [flags](ENUM_MAPPING_FLAGS_RESEARCH.md).

## Объём и API

Поддерживаются enum ↔ enum, enum ↔ integer, enum ↔ string, flags, aliases,
nullable, Create/Update и IncludeBase. Целые типы: `sbyte`, `byte`, `short`,
`ushort`, `int`, `uint`, `long`, `ulong`.

Вне объёма: автоматический reverse, числовой string-to-enum parsing,
wire attributes, naming policies, коллекции и проекции. Строковые списки flags
возвращены на обсуждение; их контракт пока не принят.

| API scalar enum-пары | Контракт |
|---|---|
| `Members` | Типизированные декларативные правила значений через C# expressions |
| `ConstructUsing`, `ResolveUsing` | Обычный C# после null handling; можно сочетать с Members и читать начальный `result` |
| `Construct`, `Resolve` | Не генерируются: структурного создания scalar-значения нет |
| [Convert](../api/convert.md) | Обычный callback, владеющий всем алгоритмом, включая null handling |
| `Map`, settings, `IncludeBase` | Существующие контракты с уточнениями ниже |

Без особых правил достаточно регистрации `Map<TSource, TDestination>()`.
Граница определяется видом mapping: enum-to-DTO не теряет object construction
из-за enum source. Отдельных `Values`, `Flags`, `MapValue`, `Inherited` не нужно.

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status => status switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted,
        DomainStatus.Corrupt => throw new InvalidOperationException(),
        _ => ApiStatus.Unknown
    });
```

Специальные ветки имеют приоритет. Остальные значения получают конвенцию;
`Unknown` возвращается только при её неуспехе. Завершающая ветка необязательна:
без неё отсутствие результата приводит к mapping exception на Create и Update.

## Настройки

Нужны только два новых независимых выбора:

| Настройка | Вопрос | Значения и library default |
|---|---|---|
| `EnumMappingStrategy` | Что считать соответствием? | `Default`, **`ByName`**, `ByValue`, `ByValueAllowUndefined`; для integer-to-enum default — строгий `ByValue` |
| `FlagsMappingMode` | Обрабатывать бит или полную маску? | `Default`, **`ByBit`**, `ByMask`; только flags-to-flags |

```csharp
builder.Map<WireCode, StoredCode>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValue);
```

`ByName` — default enum-to-enum, включая E → E, и enum-to-string.
`ByValue` строгий; `ByValueAllowUndefined` сохраняет любое представимое число.
Критерий, единица обработки, отключение конвенции и проверка покрытия — разные
вопросы. Ручной cast не заменяет настройку общей числовой конвенции.

| Существующая настройка | Применение к enum |
|---|---|
| `MemberSelection.Auto` (default) / `Explicit` | Включает / отключает неявную конвенцию; явный `Auto()` работает в обоих случаях |
| `UnmappedMemberValidation` | Compile-time coverage; default `None`, предупреждения; runtime mapping не меняет |
| `MappingMode` | Default `CreateAndUpdate`; отключённая операция немедленно бросает исключение |
| `NullSourceHandling`, `NullDestinationHandling` | Общие null policies; defaults `ReturnNull` и `Create`; ноль не является null |
| `ConstructorSelection` | Для scalar destination общий default игнорируется; явная pair-настройка, включая `Default`, даёт `MORPH0023` |
| `Flattening` | Не преобразует enum-имена; корректное значение не влияет на них, неверное effective value сохраняет обычную диагностику |
| `UnknownDerivedTypeHandling` | Не относится к неизвестному enum-числу; scalar-пары не получают derived dispatch |

Дополнительные настройки fallback, overflow, runtime validation, ignore-case
и enum coverage не вводятся; нет `Auto(fallback: ...)`. Числовую допустимость
выбирает стратегия, реакцию на отсутствие соответствия — `Members`.

### Наследование и применимость

Обычный [precedence](../settings/README.md#precedence): текущая пара → included
пары от ближайшей → текущий mapper → подключённые base mappers от ближайшего
→ MSBuild → library default. Каждая настройка разрешается независимо.

- `Default` продолжает поиск, а не сбрасывает наследование. Для сброса
  унаследованного ослабленного режима нужен `ByValue`, для `ByMask` — `ByBit`.
- Последняя запись одного уровня побеждает; положение mapper-level вызова
  относительно `Map` не влияет. База участвует через `base.Configure` и `IncludeBase`.
- Included pair выше mapper: mapper-level `Explicit` не перебивает её `Auto`;
  pair-level `Explicit` перебивает, последующий `Default` возвращает наследование.

| Направление | `EnumMappingStrategy` | `FlagsMappingMode` |
|---|---|---|
| Enum → enum | Все стратегии; при `[Flags]` только с одной стороны — правила целого значения | Только когда оба enum имеют `[Flags]`, включая nullable underlying types |
| Enum → string | Все стратегии; числовые режимы дают одинаковое представление | Неприменима; обрабатывается целое значение |
| Integer → enum | `ByValue` / `ByValueAllowUndefined`; default `ByValue` | Неприменима; целое число, для flags destination — допустимость маски |
| Enum → integer | Неприменима; перенос числа с проверкой диапазона | Неприменима |
| String → enum | Неприменима; конвенция CLR-имён | Неприменима |

Явную неприменимую pair-настройку диагностировать по общему контракту
`MORPH0023`; общий default на неприменимой паре игнорируется. Для integer-to-enum
явный `ByName` ошибочен; выигравший общий `ByName` оставляет строгий default.
Resolver **не ищет заново** нижнюю числовую настройку: mapper-level `ByName`
поверх MSBuild `ByValueAllowUndefined` всё равно даёт строгий `ByValue`.

Using не превращает пару в Convert: применимая настройка допустима даже без
Members или достижимого автоматического пути и может участвовать в IncludeBase.
Без Members она не преобразует готовое значение фабрики. Остальные declarative
settings сохраняют обычные проверки значений и применимости.

`Convert` сохраняет полное владение алгоритмом: inherited declarative settings
игнорируются, локальные несовместимые настройки диагностируются. `MappingMode`
и `UnknownDerivedTypeHandling` остаются применимыми. Members с локальным Convert
несовместим; обычный Convert не получает enum-конвенцию.

## Members: правила и конвенция

Members — декларативный DSL, не runtime delegate из Configure. Конвенцией
дополняется только switch, непосредственно задающий enum-правила. Вложенные
switch справа от `=>`, пользовательские методы и Using/Convert остаются C#.

### Входы и приоритет

Сохраняются четыре формы callback:

| Форма | Доступные значения |
|---|---|
| `source => rules` | Non-null source после null policy |
| `(source, previous) => rules` | Дополнительно `Option` исходного non-null destination; `None` на Create и Update с null destination |
| `(source, previous, result) => rules` | Дополнительно реально выбранный начальный non-null destination |
| `(source, previous, result, context) => rules` | Дополнительно существующий DSL context с `Operation`, без полного runtime `MappingContext` |

В flags-to-flags единица source — бит при ByBit и маска при ByMask.
`previous` и `result` остаются полными destination-значениями.

Для каждой единицы правила выполняются в порядке:

1. Локальные специальные ветки в написанном порядке.
2. Специальные ветки IncludeBase, от ближайшего уровня к дальнему.
3. Одна конвенция с effective settings, если selection — Auto.
4. Ближайшая завершающая ветка; при отсутствии — mapping exception.

`Explicit` пропускает только шаг 3. Конвенция всегда берёт **исходный source
этой единицы**, независимо от выражения перед switch:

| Switch | Явные patterns проверяют | Конвенция и Auto() получают |
|---|---|---|
| `source` | Значение source | Исходный source |
| `result` | Начальный destination | Тот же source, без destination-to-destination mapping |
| `(source, result)` / `(result, source)` | Кортеж в указанном порядке | Тот же source, без выбора элемента кортежа |
| `Normalize(source)` | Вычисленное значение | Тот же source, без нормализации |

Например, source = Active, фабрика вернула Pending, ByName даёт Active:
если специальные ветки не сработали, `_ => Auto()` возвращает Active.
`_ => result` при Auto тоже даёт Active, при Explicit — Pending.
Конвенция не создаёт начальный result и не вычисляется заранее ради него.

### Ветки, Auto и вычисления

| Декларация | Поведение |
|---|---|
| `Cancelled => Deleted` | Явное соответствие до конвенции |
| `Corrupt => throw ...` | Явный запрет; исключение не считается неуспехом конвенции |
| `Active => Auto()` | Явный запрос конвенции, включая Explicit; неуспех сразу бросает |
| `_ => Unknown` | Fallback после конвенции; при Explicit — после явных правил |
| `_ => Auto()` | Конвенция для остатка; без соответствия — исключение |
| `_ when condition => ...` | Специальная ветка до конвенции; false продолжает поиск |

Завершающий `_ => expression` или `var remaining => expression` без guard —
fallback. `or` объединяет случаи; patterns и guards не переупорядочиваются.
Даже после false guard для Active конвенция может сопоставить Active.
Bare завершающий Auto и неявное дополнение образуют **один** автоматический путь:
не повторять конвенцию после неудачи и не возвращаться к другим веткам после
неуспешного явного Auto.

`Members(_ => Auto())` явно запрашивает конвенцию. Прямое
`Members(source => Compute(source))` задаёт результат для всей текущей единицы
и может завершить поиск до inherited rules и конвенции. Поддерживаются основные
декларативные управляющие конструкции остальных типов: блоки, locals и их aliases,
if/else, switch statements, условные expressions, логические условия и guards.
Сохраняются их scopes, short-circuit и действующие statement boundaries.
Перенос возвращаемого mapping switch в неизменяемый local alias не отключает
конвенцию. Вложенный switch внутри выбранного результата остаётся обычным C#.
Требования C# к завершённости return-путей сохраняются; method group и полный
imperative algorithm задаются Using/Convert.

Выражение перед switch вычисляется один раз на своём месте. Сохраняются scopes,
locals, комментарии, независимые вычисления, условность guards и результатов.
Если пользователь меняет переменную source, явные выражения видят изменение,
но конвенция и Auto используют исходный source этих правил. Exceptions
пользователя не перехватываются для fallback. Дополнение перед `_` — осознанная
DSL-семантика: [обычный C# switch](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/switch-expression)
сам конвенцию не добавляет.

### IncludeBase

Объединяются правила **той же пары типов** по обычным
[правилам наследования](../configuration-inheritance.md). Новый Members не
стирает базовые overrides. Локальный fallback заменяет только базовый fallback;
без локального наследуется ближайший. Это относится и к `throw`, и к `Auto()`.

Уровень важнее специфичности pattern: локальный `_ when condition` при true
предшествует базовому именованному case. False guard позволяет проверить
тот же case в базе. `Cancelled => Auto()` перекрывает базовый Cancelled;
завершающий `_ => Auto()` остаётся после **всех** специальных веток.

Пример при ByName: база задаёт Cancelled → Deleted, Suspended → Disabled,
fallback Unknown; локально Cancelled → Archived и fallback Unrecognized.
Результаты: Cancelled → Archived, Suspended → Disabled, общее Active → Active,
неизвестное Legacy → Unrecognized. False у локального guard Cancelled даёт Deleted.

Locals уровня вычисляются один раз при входе, до его switch; база достигается
лениво. Возврат к локальному fallback использует уже полученные locals:
`var policy = GetPolicy(source)` не вызывается повторно ради `_ => policy.Unknown`.
Результат выбранной ветки не передаётся следующему уровню как новый вход.
Все уровни используют одну конвенцию и один начальный result.

Using наследуется для точной пары и сочетается с Members; inherited Convert
или фабрика не превращаются в enum rules. После композиции сохраняются проверки
доступности result, нескольких destination methods, конфликтов с Convert,
helper accessibility и cross-assembly inheritance. Нового маркера Inherited нет.

## Lifecycle и Using

Порядок: общие guards → начальный destination → проверка terminal null →
Members → результат. Сохраняются [null policies](../settings/null-handling.md):
source проверяется первым, null destination не превращает Update в Create.
Nullable registration точная: автоматически искать underlying pair нельзя.
`ReturnNull` для non-nullable enum destination даёт `default`, даже без
объявленного нуля; это результат null policy, не конвенции.

| Метод | Create | Update с destination | Update с null destination и `NullDestinationHandling.Create` |
|---|---|---|---|
| ConstructUsing | Callback | Callback пропущен; result = previous | Callback; операция остаётся Update |
| ResolveUsing | Callback с previous = None | Callback с исходным previous | Callback с previous = None |

При NullDestinationHandling.Throw callback не вызывается. Ноль — имеющийся
destination, а не отсутствие previous. Using принимает inline lambda, method
group или delegate; контекстная форма получает полный MappingContext с Mapper.

Без Members выбранное значение окончательно: нет конвенции и проверки
объявленности поверх него; coverage не анализирует тело фабрики. Null фабрики
завершает операцию до Members, без повторных null policies. Вызов сохраняется,
даже если Members не читает result или всегда возвращает другое значение:
важны эффекты, исключения и terminal null.

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

`status` здесь non-null DomainStatus. Default null source возвращает null,
не вызывая LegacyConvert. Явный null из правила также допустим для nullable
destination. На Update previous остаётся исходным destination, result —
выбранным фабрикой. Без Explicit завершающее `_ => result` сохраняет фабричное
значение только после неуспеха конвенции. Прямое `Members(... => Normalize(result))`
уже является полным явным правилом. Порядок fluent-вызовов не меняет lifecycle.

Замена результата через Members ограничена scalar mapping.
Result доступен только по пути, где начальное значение реально выбрано:
фабрикой либо из имеющегося destination при Update. На Create без фабрики
читать его нельзя; не подставлять ноль и не запускать конвенцию заранее.
Диагностируется недоступное **чтение**, не неиспользуемый параметр:
`(_, _, _, context) => ...` допустимо для доступа к Operation.

## Имена и строки

ByName сравнивает имена сначала через `Ordinal`, затем, только без точного
совпадения, через `OrdinalIgnoreCase`, независимо от culture — как при
[подборе параметров конструктора](../api/members.md#constructor-parameters). Найденное exact
совпадение не становится неоднозначным из-за других вариантов регистра.
Это правило enum-конвенции, не изменение object/tuple matching или C# patterns.

| Имена destination | Source | Результат |
|---|---|---|
| Ready = 10, READY = 20 | Ready / READY | 10 / 20 по точному имени |
| Те же | ready | Неоднозначность второго этапа |
| Ready = 10, READY = 10 | ready | Однозначное число 10 |

Source aliases одного числа неразличимы в runtime. Достаточно одного найденного
соответствия, если остальные найденные соответствия дают то же число; отсутствие
имени для другого alias не отменяет успех. Ready = Active = 1 в source против Ready = 10, Active = 20
в destination неоднозначны. Нужна диагностика автоматического пути либо явная
ветка для числа 1. Порядок объявлений не разрешает конфликт; обычные C# ошибки
дублирующих/недостижимых веток сохраняются.

Для string-входа неоднозначность ignore-case этапа — неуспех конвенции для
этой строки: используется fallback либо исключение, включая немедленный throw
явного Auto. Точные Ready и READY по-прежнему допустимы; первый найденный элемент
не выбирается. Статически известные конфликты дополнительно диагностируются.

| Направление | Конвенция |
|---|---|
| Enum → string, ByName | CLR-имя с исходным регистром; aliases требуют явного canonical output, даже если различаются только регистром |
| Enum → string, обе числовые стратегии | Underlying integer в десятичной записи с InvariantCulture без группировки; включая неназванное число, с точной шириной и знаком |
| String → enum | CLR-имя с exact-first/ignore-case; явные строковые patterns сохраняют обычную C# семантику |

Active = 2 даёт `"Active"` по имени, `"2"` по числу; неизвестное 123 — `"123"`
в обеих числовых стратегиях. Signed -1 даёт `"-1"`, ulong не сужается до long.
При ByName неназванный enum не имеет соответствия; скрытого numeric fallback
через ToString нет. [Enum.GetName](https://learn.microsoft.com/en-us/dotnet/api/system.enum.getname?view=net-10.0)
не гарантирует canonical alias.

```csharp
builder.Map<ApiStatus, string>()
    .Members(status => status switch { ApiStatus.Deleted => "removed" });

builder.Map<string, ApiStatus>()
    .Members(text => text switch
    {
        "removed" or "deleted" => ApiStatus.Deleted,
        "pending" or "queued" => ApiStatus.Pending,
        _ => ApiStatus.Unknown
    });
```

Направления независимы. Пустая строка, числовой текст и пробелы не получают
особой трактовки; расширение на numeric parsing потребует отдельного контракта.
Формат/culture можно задать выражением Members или обычным callback.
Числовой flags-to-string форматирует **всю** маску: Read = 1, Write = 2 дают
`"3"`, неизвестные биты сохраняются; Members вызывается для маски, OR строк нет.

## Числовая конвенция

Для обычного enum destination `ByValue` требует объявленного значения с тем
же числом; объявленность source не нужна. `ByValueAllowUndefined` допускает
любое представимое число. Enum-to-integer тоже сохраняет неназванное число:
у integer нет списка объявлений. Строгость flags описана ниже.

**Оба режима сохраняют математическое число** независимо от checked options
consumer: без усечения, смены знака и переполнения промежуточного типа.
Например, 256 не становится byte 0, ulong не сужается до long.
Явные результаты Members не получают скрытой validation; пользовательские
checked/unchecked expressions сохраняют свою семантику.

Пример integer-to-enum с byte destination `{ Unknown = 0, Active = 1 }`
и завершающим `_ => Unknown`:

| Вход | ByValue (default) | ByValueAllowUndefined |
|---|---|---|
| 1 | Active | Active |
| 42 | Unknown через fallback | Неназванное 42 |
| 300 или -1 | Unknown через fallback | Unknown через fallback |

Выход из диапазона означает отсутствие конвенционного соответствия.
Реакция задаётся существующими ветками:

| Требование | Members |
|---|---|
| Fallback при любом неуспехе, включая overflow | Завершающее `_ => Unknown` |
| Исключение при любом неуспехе | Без fallback, `_ => Auto()` либо `_ => throw ...` |
| Overflow бросает, неизвестное число в диапазоне получает fallback | Явный range guard с throw до завершающего fallback |

Для ushort → byte guard может быть
`_ when (ushort)status > byte.MaxValue => throw new OverflowException()`.
Он выполняется до конвенции. Общая overflow policy для множества пар без
повторения guard в принятый API не входит.

Завершающий `_ => checked((StoredCode)code)` также допустим: конвенция
обработает известные соответствия, пользовательский cast — остаток.
Explicit Auto при неудаче всегда бросает, без перехода к fallback.

E → E соблюдает стратегию, Members и Explicit. У обычного enum ByName и строгий
ByValue не сохраняют неназванное число автоматически, ослабленный режим сохраняет.
Identity допустимо только как реализация тех же правил, без обхода проверок.

## Flags

`System.FlagsAttribute` распознаётся при генерации, включая metadata и nullable
underlying enum; числа 1, 2, 4 без атрибута не делают enum flags. Режим только
для flags-to-flags; другие направления обрабатывают целое значение по таблице
[применимости](#наследование-и-применимость). Runtime reflection не нужен.

### ByBit и ByMask

| Свойство | ByBit (default) | ByMask |
|---|---|---|
| Source правил, конвенции и Auto | Текущий установленный бит source enum | Полная исходная маска |
| Members, locals и guards | Для каждого бита и достигнутого уровня IncludeBase | Один проход уровней над маской |
| Fallback | Результат непереведённого бита | Итог всей операции |
| Результат | OR результатов битов | Выбранное значение целиком |
| Composite case над неизменённым source | Не совпадает с отдельным битом | Проверяет точную маску |

EnumMappingStrategy не меняет единицу или число проходов Members. Один effective
FlagsMappingMode действует на все локальные и inherited rules; смешанного
прохода «сначала маска, затем те же Members на битах» нет.

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

builder.Map<SourceAccess, TargetAccess>()
    .Members(flag => flag switch
    {
        SourceAccess.Read => TargetAccess.View,
        SourceAccess.Write => TargetAccess.Edit,
        SourceAccess.Delete => TargetAccess.None,
        _ => TargetAccess.Unknown
    });
```

| Вход при default ByBit + ByName | Результат |
|---|---|
| Read | View по явному правилу |
| Read \| Write или ReadWrite | View \| Edit; имя composite не меняет разбиение |
| Read \| Delete | View; явный ноль удаляет Delete |
| Read \| Audit | View \| Audit; Audit сопоставлен по имени |
| Read \| (SourceAccess)16 | View \| Unknown; неизвестный бит дошёл до fallback |
| (SourceAccess)16 | Unknown; совпадение числа с View не заменяет имя |

Один бит может дать ноль, один или несколько destination-битов. Совпадающие
вклады объединяются без ошибки; aliases не вызывают повторной обработки.
Неизвестные биты тоже проходят правила, включая явный `(SourceAccess)16 => ...`.
Без fallback непокрытый бит бросает исключение всей операции; частичный
результат не возвращается, выполненные эффекты не откатываются.

ByBit посещает физические биты от младшего к старшему в точной ширине source;
signed high bit — последним. Порядок объявлений не влияет; знаковые биты не
расширяются за исходную ширину. Только Pair = 3 не даёт ByName имён для 1 и 2.
Explicit отключает неявную конвенцию, сохраняя разбиение и OR; Auto запрашивает
конвенцию текущего бита. Числовая стратегия не обходит явные правила cast-ом.

```csharp
builder.Map<SourceAccess, TargetAccess>()
    .FlagsMappingMode(FlagsMappingMode.ByMask)
    .Members(mask => mask switch
    {
        SourceAccess.ReadWrite => TargetAccess.View | TargetAccess.Edit,
        _ => Auto()
    });
```

В ByMask правило Read не обрабатывает Read внутри ReadWrite. ByName сопоставляет
только **имена целых объявленных значений**, по общим правилам регистра и aliases.
Одинаковое имя ReadWrite может связать разные числа и составы битов; сборки по
именам отдельных битов нет. Например, source Read = 1, Write = 2 и destination
Read = 8, Write = 16 дают для неназванного 3 результат 24 в ByBit, но отсутствие
соответствия в ByMask. Whole-mask overrides не дополняются автоматическим
переводом остальных неназванных комбинаций. Исключение — пустая маска ниже.

### Ноль, null и начальное значение

Нулевой вход в **обоих** режимах проходит правила один раз с source = 0:
явное None → Unknown может его изменить. Затем неявная конвенция возвращает 0
даже без объявления None, независимо от имён и стратегии, включая ByMask + ByName.
При Explicit нужны явное правило, Auto или fallback; иначе исключение.
Нулевое правило не добавляется к ненулевой маске и не вызывается для её битов.

При nullable destination null из любого правила бита немедленно завершает всю
операцию с null: следующие биты не вычисляются, прошлые эффекты не откатываются.
Null не означает удаление бита; для этого нужен ноль. ByMask возвращает обычный
scalar nullable результат. Null source и terminal null фабрики следуют общему lifecycle.

Using всегда получает полную source-маску и вызывается один раз, когда этого
требует lifecycle. Все биты/уровни видят один начальный полный result и исходный
previous. Result не является OR-накопителем и не добавляется к нему автоматически.
Фабрика без Members окончательна; отсутствие начального result не заменяется нулём.

### Строгая числовая маска

При обработке целого значения строгая допустимость определяется destination:
обычный enum требует точного объявления, flags enum — **OR целых объявленных
destination-значений**, включая пустую комбинацию 0. Это действует для ByMask,
integer → flags и обычного enum → flags; источник не меняет критерий. Сначала
проверяется диапазон underlying type; объявленность source не требуется.

| Destination | Допустимо | Недопустимо |
|---|---|---|
| Read = 1, Write = 2 | 0, 1, 2, 3 | 4 |
| Только Pair = 3 | 0, 3 | 1, 2 |
| Pair = 3, Audit = 4 | 0, 3, 4, 7 | 1, 2, 5, 6 |
| Только All = -1 | 0, -1 | 1 |

Pair можно включить целиком, но нельзя извлечь его необъявленный одиночный бит.
All = -1 не разрешает любое число. Это отличается и от exact declared lookup,
и от проверки «каждый бит где-то встречается»; Enum.IsDefined не заменяет контракт.
ByValueAllowUndefined допускает любое представимое число.

Критерий проверяется без перебора подмножеств: в ширине destination объединить
все объявленные c, целиком содержащиеся в маске x, и сравнить OR с x.
Форму generated code выбирает реализация.

В ByBit числовая конвенция проверяет каждый бит отдельно: Pair = 3 не разрешает
1 и 2. Итоговый OR и явные результаты Members дополнительной проверки не получают.
Whole-mask -1 из int помещается в sbyte; при ByBit среди int-битов есть 256,
которое в sbyte не помещается даже при AllowUndefined. Signed high bit имеет
математическое значение в source type, не unsigned значение промежуточного типа.
Неуспех неявной конвенции передаётся fallback выбранной единицы; явный Auto бросает.
E → E также соблюдает режим: ByBit может собрать неназванную комбинацию из переводимых битов.

### Недостижимые composite cases

В ByBit — warning только при **доказанной** недостижимости составного case.
Это позволяет обнаружить ошибку режима и сохранить правило, наследуемое также
для ByMask; severity повышается обычными средствами, без новой настройки.

Нельзя судить только по составной константе или имени source-параметра:
учитываются switch input, locals, изменения source и guards. Pattern над полным
result или Normalize(flag) может быть достижимым. Анализ не выполняет методы
при генерации; общего запрета таких patterns нет. Доказанную недостижимость
не заменять молчаливым игнорированием. Обычные ошибки C# сохраняются.

## Проверка покрытия

Используется UnmappedMemberValidation с default None и warnings; severity
можно повышать обычными средствами. Coverage проверяет итоговую композицию
правил, конвенции и fallback, не меняя runtime-допустимость и порядок. Проверяются
связи между объявлениями и явными правилами, не множество всех возможных
результатов исполнения. Для enum → enum неназванный source не закрывает
destination coverage: Source { Active = 1 } → Target { Active = 1, Archived = 2 }
под ByValue предупреждает об Archived, хотя runtime-вход (Source)2 допустим.

| Режим / случай | Проверка |
|---|---|
| Source | Объявленные физические source-значения обработаны результатом, явным запретом или успешной конвенцией; aliases одного числа — одна группа |
| Destination | Объявленные destination-значения участвуют в соответствиях объявлений или явных правилах; many-to-one допустим |
| Strict / None | Обе стороны / без coverage; проверки некорректной конфигурации и диапазона сохраняются |
| Завершающее значение или throw | Закрывает source coverage; может скрыть добавление новых enum values |
| Auto | Не доказывает покрытие значения без соответствия |
| Guard | Неизвестный статически результат требует учитывать путь false |
| Динамический результат Members | Обработку source можно доказать, множество destination часто неизвестно; если запрошенное coverage нельзя проверить — warning о границе анализа, не о недостижимости значения |

Тело Using не анализируется; наличие фабрики не доказывает полноту Members.
`_ => result` явно обрабатывает остаток source, но не перечисляет destination.
Для string/integer проверяется конечная enum-сторона; неназванные числа —
runtime-вопрос. При None непокрытый runtime-вход всё равно бросает без fallback.

В ByBit coverage опирается на атомарные правила; composites не требуют отдельной
ветки, если выводятся из битов. В ByMask анализируются целые объявленные значения.
Zero — отдельный вход, не доказательство покрытия ненулевых. Не перечислять все
маски и не превращать анализ в интерпретатор C#; отличать доказанную неполноту
от невозможности завершить запрошенную проверку.

Намеренное исключение отдельного значения задаётся существующей формой discard:
`_ = TargetEnum.SomeMember;` в теле Members. Аналогично можно указать объявление
source enum. Это compile-time acknowledgement для coverage, не runtime-правило:
оно не отключает конвенцию, не удаляет бит и не меняет результат. Сторона
определяется типом константы; aliases относятся к одной физической группе.
Как у обычных members, нужен настоящий discard отдельным statement верхнего
уровня тела lambda, а не присваивание переменной по имени `_`. Для E → E,
где тип не различает стороны, точный охват acknowledgement ещё обсуждается.

## Вложенное использование

```csharp
builder.Map<Order, OrderDto>()
    .Members((source, _) => new() { Status = Map(source.Status) });
```

[Nested mapping](../nested-mapping.md) остаётся явным: разные enum требуют
Map/Create/Update. Auto внутри object/tuple Members требует implicit C# conversion
и не запускает зарегистрированную пару. Same-enum property может копироваться
как прежде; IMapper, DI и get-only value members не получают обходных путей.
Update возвращает scalar result, не изменяет переданный value type по месту.

## Реализация и проверенные предпосылки

Исходный анализ: Morphant 0.5.0,
[main b77d654](https://github.com/strangeman375/Morphant/commit/b77d654d8d255bb89c04f9b9c69cbb1a56c7d7c5).
Enum сейчас — opaque destination; допустимость пары не означает first-class
algorithm. Уже существующий Convert позволяет ручное преобразование.

| Точка интеграции | Необходимое изменение / ограничение |
|---|---|
| [DestinationCapabilityPolicy](../../src/Morphant.Generator/MappingPair/DestinationCapabilityPolicy.cs), [TypeMapperModelBuilder](../../src/Morphant.Generator/TypeMapperGeneration/TypeMapperModelBuilder.cs) | Scalar algorithm вместо object construction/member plan |
| [MemberConfigurationEmitter](../../src/Morphant.Generator/MemberSurface/PairConfiguration/MemberConfigurationEmitter.cs) | Четыре существующих delegates параметризованы результатом; нужен scalar rule kind и typed surface, без фиктивных writable enum fields |
| [PairConfigurationEmitter](../../src/Morphant.Generator/ConstructionSurface/PairConfiguration/PairConfigurationEmitter.cs) | Using уже генерируется; расширить композицию с scalar Members, сохранив [ConstructUsing](../api/construct-using.md) / [ResolveUsing](../api/resolve-using.md) lifecycle |
| [MappingSettings](../../src/Morphant.Generator/Settings/MappingSettings.cs), [diagnostic pipeline](../../src/Morphant.Generator/Settings/MappingSettingsDiagnosticPipeline.cs) | Общий resolver и применимость; исходные проверки: [selection](../../src/tests/Morphant.Generator.IntegrationTests/TypeMapperMemberTests/MemberSelectionTests.cs), [inheritance](../../src/tests/Morphant.Generator.IntegrationTests/TypeMapperInheritanceTests/SettingsCompositionTests.cs) |
| [MemberTypeCompatibility](../../src/Morphant.Generator/TypeMapperGeneration/MemberTypeCompatibility.cs) | Implicit conversion; между разными enum её нет. Контракт nested Auto не менять |

Enum shape хранит underlying type, constants/aliases, single-bit mask и locations;
ulong не приводится к long. Runtime reflection, Enum.Parse и boxing не нужны.
Open `T : Enum` без известных полей требует ручного алгоритма либо diagnostic.
IMapper/ITypeMapper и MappingContext подходят без нового runtime API.

### Компилятор и типизация

Неполный switch предупреждает уже **в Configure**: полный generated switch этого
не устраняет, а обязательное `_ => Auto()` противоречит краткому Members.

Исторические isolated probes (не проверка интеграции feature):

| Probe | Подтверждено | Не подтверждено / ограничение |
|---|---|---|
| Roslyn 4.4.0, C# 9, nullable, warnings-as-errors | Неполнота даёт CS8509/CS8524, guard — CS8846; узкий DiagnosticSuppressor подавляет их и после повышения severity; ordinary/nested switch warnings, CS8510 и wrong result types остаются | Интеграция с реальным DSL, MSBuild и IDE; harness потребовал исключения CS1701 для старого Roslyn с .NET 10 references, сами входные switch-проверки — нет |
| SDK 10.0.100, C# 9, generic marker с conversions от destination, `AutoMarker` и `AutoMarker<T>` | Binding смешанных результатов, 10 arms, or, guards, throw, вызовы, строки/числа, ранние пробные callback forms | Не реальный Members overload resolution; имя старых Values/Flags не было условием типизации |

Nullable reference marker поддерживает natural null/default; генератор проверяет
их по настоящему destination type. Struct marker ломает natural null. Промежуточному
`var x = ... switch` со смесью enum и bare Auto может не хватить target type;
помогает существующий `Auto<T>()`. Не использовать object/dynamic как обход типизации.

[Suppressor](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.diagnostics.diagnosticsuppressor?view=roslyn-dotnet-4.4.0)
должен распознавать настоящий DSL symbol, enum rule kind и только switch, который
planner дополняет. Одного имени Members недостаточно. Не подавлять warnings
глобально, в object/tuple Members, Construct, Using, Convert или nested switch;
сохранить wrong-type, unreachable, obsolete и прочую диагностику. В generated
switch покрытие должно быть явным; enum coverage остаётся отдельной проверкой.

### Generated code и критерии готовности

Простой случай должен оставаться простым switch: explicit arms → синтезированные
соответствия → fallback. Не добавлять недостижимые arms после явных patterns.
Guards, IncludeBase и другой switch input могут требовать вложенного продолжения:
сохранить единичные вычисления, исходный source конвенции и условность результатов.
Нет local на каждый case, ранней конвенции ради result или catch вокруг user code.
Соблюдать [generator contracts](GENERATOR_CONTRACTS.md): читаемость, scopes,
форматирование, failure stubs, settings diagnostics, compatibility manifest,
incrementality, cancellation/recovery; ordinary object/tuple path не менять.

Реализация проверяет все контракты выше по [testing guidelines](TESTING_GUIDELINES.md).
Особенно важны сочетания, которые isolated probes не покрывают:

| Область | Проверки |
|---|---|
| API и lifecycle | Bare registration; четыре Members формы; enum/string/integer/nullable; Create/Update, включая Update без Create; все null policies; доступное/недоступное чтение result и неиспользуемый параметр; прежний object/tuple API |
| Правила и эффекты | Partial switch; Auto/Explicit; source/result/оба порядка tuple/Normalize; mutation source; false guards; computed fallback/throw; один автоматический путь; явные checked/unchecked; method group/delegate/context Using, пропуск ConstructUsing, terminal null и эффекты неиспользованной фабрики |
| Композиция | Все уровни и порядок settings, Default и последняя запись; несколько IncludeBase, local/inherited fallback, приоритет уровня; ленивые locals/scopes; один initial result; Convert/destination-method conflicts; фабрика после наследования |
| Значения | Перестановка кодов, mixed-case/culture/aliases и canonical string; signed/ulong/range; declared/unnamed source и destination; E → E; integer default, явный ByName diagnostic, общий ByName поверх нижнего numeric setting; coverage всех режимов, guards, dynamic results и finite/infinite sides |
| Flags | Metadata/nullable attributes; ByBit/ByMask и единый inherited mode; именованные/неназванные whole-mask ByName; Pair-only, Pair/Audit, All = -1; signed high bit и разные ширины; zero/Explicit/Auto/fallback, null short-circuit, неизвестные биты, aliases, OR вкладов; предупреждение с учётом mutation/result/computed input; неприменимая pair setting и общий default |
| Toolchain | Реальная nullable marker/delegate типизация и доступность result; C# 9, minimum Roslyn, MSBuild и IDE; suppressor diagnostic families, guards, отключённые анализаторы и warnings-as-errors; nested switch/wrong types/obsolete; edit settings/enum/callback, recovery и cancellation |

## Оставшиеся вопросы после review

- Строковые flags: применимость ByBit/ByMask к string-парам, форматы и разделители,
  единица Members/Auto/fallback, ноль, composite names и неизвестный токен/бит.
- Coverage: acknowledgement при E → E и точный декларативный критерий для
  destination composites; без анализа всех возможных runtime-результатов.
- Scalar-применимость существующих Value/Map/Create/Update/Ignore; не изобретать
  значение для Ignore при отсутствии выбранного result.
- Интеграционный прототип настоящих delegate/marker форм, generated extensions
  и suppressor; отдельная проверка IDE. Исторические isolated probes не заменяют её.
