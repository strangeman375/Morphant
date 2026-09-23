# Enum mapping: пользовательские сценарии

2026-09-23. Каталог для проверки **будущего поведения** до реализации feature.
Примеры не являются отчётом о прошедших runtime-тестах. Канонические правила —
в [согласованном дизайне](ENUM_MAPPING_DESIGN.md); здесь они показаны на задачах,
коде конфигурации, конкретных входах и ожидаемых результатах.

## Как читать примеры

- Каждый номер — отдельная конфигурация. Регистрации из разных примеров не
  нужно одновременно добавлять в один mapper. Изменение настройки в таблице
  обозначает отдельный вариант той же конфигурации.
- Фрагменты `builder.Map<...>()` находятся внутри
  `protected override void Configure(MapperBuilder builder)` обычного
  `[MorphantMapper] public partial class ExampleMapper : TypeMapper<ExampleMapper>`.
  Используются `using System;` и `using Morphant;`; вспомогательные методы доступны mapper.
- Без явных настроек действуют library defaults. «Создать» означает
  `mapper.Map<TSource, TDestination>(source)`, «обновить» —
  `mapper.Map<TSource, TDestination>(source, destination)`.
- Если операция не указана, таблица описывает Create и Update с non-null
  destination; существующее значение не даёт скрытого fallback.
- «Исключение маппинга» означает отсутствие результата по принятому контракту.
  Здесь не назначается имя ещё не реализованному enum-specific exception.
  Явные пользовательские исключения названы отдельно.
- «Диагностика» относится к конфигурации/компиляции, а не к возвращаемому значению.
  Coverage по умолчанию выключен; его warnings отмечены там, где он включён.
- В таблицах значения enum приводятся именами и при необходимости числами.
  Это не результат вызова `Enum.ToString()`.

Навигация:

- [Обычные enum](#обычные-enum)
- [Выражения и управление выполнением](#выражения-и-управление-выполнением)
- [Обычные enum и строки](#обычные-enum-и-строки)
- [Числа и диапазоны](#числа-и-диапазоны)
- [Nullable, Create, Update и фабрики](#nullable-create-update-и-фабрики)
- [Наследование и настройки](#наследование-и-настройки)
- [Flags между enum](#flags-между-enum)
- [Flags и числовые стратегии](#flags-и-числовые-стратегии)
- [Строка в flags](#строка-в-flags)
- [Flags в строку](#flags-в-строку)

## Обычные enum

Общие типы для примеров, в которых не указаны собственные:

```csharp
enum DomainStatus
{
    Unknown = 0, Pending = 1, Active = 2, Cancelled = 3,
    Suspended = 4, Corrupt = 5, Legacy = 6
}

enum ApiStatus
{
    Unknown = 0, Pending = 10, Active = 20, Deleted = 30,
    Disabled = 40, Archived = 50, Unrecognized = 60, InternalOnly = 70
}
```

### 01. Регистрация без специальных правил

```csharp
builder.Map<DomainStatus, ApiStatus>();
```

| Вход | Результат |
|---|---|
| `Pending = 1` | `ApiStatus.Pending = 10` |
| `Active = 2` | `ApiStatus.Active = 20` |
| `Unknown = 0` | `ApiStatus.Unknown = 0` |
| `Cancelled` | Исключение маппинга: имени нет |
| `(DomainStatus)20` | Исключение маппинга: совпадение с числом destination не заменяет имя |

### 02. Несколько исключений из конвенции

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status => status switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted,
        DomainStatus.Suspended => ApiStatus.Disabled
    });
```

| Вход | Результат |
|---|---|
| `Cancelled` | `Deleted` |
| `Suspended` | `Disabled` |
| `Active` | `Active`, хотя ветка не написана |
| `Legacy` | Исключение маппинга |

Такой короткий mapping switch не должен требовать `_ => Auto()` ради устранения
предупреждения о неполноте. Это требование к итоговой интеграции compiler/IDE.

### 03. Завершающая ветка — fallback после конвенции

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status => status switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted,
        _ => ApiStatus.Unrecognized
    });
```

| Вход | Результат |
|---|---|
| `Cancelled` | `Deleted` |
| `Active` | `Active` |
| `Unknown` | `Unknown` |
| `Legacy`, `(DomainStatus)123` | `Unrecognized` |

Это намеренная DSL-семантика. В обычном C# такой switch вернул бы
`Unrecognized` и для `Active`.

### 04. Полностью явная таблица

К примеру 03 добавить:

```csharp
.MemberSelection(MemberSelection.Explicit)
```

| Вход | Результат |
|---|---|
| `Cancelled` | `Deleted` |
| `Active`, `Unknown`, `Legacy` | `Unrecognized` |
| `Active`, если удалить fallback | Исключение маппинга |

### 05. Явный Auto внутри Explicit

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .MemberSelection(MemberSelection.Explicit)
    .Members(status => status switch
    {
        DomainStatus.Active => Auto(),
        DomainStatus.Legacy => Auto(),
        _ => ApiStatus.Unrecognized
    });
```

| Вход | Результат |
|---|---|
| `Active` | `Active` |
| `Pending` | `Unrecognized` |
| `Legacy` | Исключение маппинга; не переход к fallback после неудачи Auto |

### 06. Запрет и объединение нескольких случаев

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status => status switch
    {
        DomainStatus.Cancelled or DomainStatus.Suspended => ApiStatus.Disabled,
        DomainStatus.Corrupt => throw new InvalidOperationException("Corrupt status"),
        _ => ApiStatus.Unknown
    });
```

| Вход | Результат |
|---|---|
| `Cancelled`, `Suspended` | `Disabled` |
| `Active` | `Active` |
| `Corrupt` | Пользовательский `InvalidOperationException`, fallback не вызывается |
| `Legacy` | `Unknown` |

### 07. Guard с приоритетом перед конвенцией

`IsBlocked()` возвращает указанное в таблице значение.

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status => status switch
    {
        DomainStatus.Active when IsBlocked() => ApiStatus.Disabled,
        _ => ApiStatus.Unknown
    });
```

| Вход | IsBlocked | Результат и вызовы |
|---|---|---|
| `Active` | `true` | `Disabled`; один вызов |
| `Active` | `false` | `Active` по конвенции; один вызов |
| `Pending` | Любое | `Pending`; IsBlocked не вызывается |
| `Legacy` | Любое | `Unknown`; IsBlocked не вызывается |

### 08. Точное имя важнее совпадения без учёта регистра

```csharp
enum NameSource { Ready = 1, READY = 2, ready = 3 }
enum NameTarget { Ready = 10, READY = 20 }

builder.Map<NameSource, NameTarget>()
    .Members(value => value switch { NameSource.ready => NameTarget.Ready });
```

| Вход | Результат |
|---|---|
| `Ready` | `NameTarget.Ready = 10`, точное совпадение |
| `READY` | `NameTarget.READY = 20`, точное совпадение |
| `ready` | `NameTarget.Ready = 10`, явное разрешение конфликта |

Без последней явной ветки автоматический путь для `ready` неоднозначен и требует
диагностики. Если оба destination-имени имеют число 10, конфликт числа исчезает.
При единственном destination-имени `Ready` все три source-имени сопоставляются ему.
Смена `CurrentCulture`, например на `tr-TR`, результат не меняет.

### 09. Aliases одного source-числа

```csharp
enum AliasSource { Ready = 1, Active = 1 }
enum AliasTarget { Ready = 10 }

builder.Map<AliasSource, AliasTarget>();
```

| Объявления destination | Вход `Ready` или `Active` | Результат |
|---|---|---|
| Только `Ready = 10` | Число 1 | 10: одного найденного имени достаточно |
| `Ready = 10, Active = 10` | Число 1 | 10: найденные соответствия согласованы |
| `Ready = 10, Active = 20` | Число 1 | Диагностика неоднозначного автоматического соответствия |

В последнем варианте явная ветка `AliasSource.Ready => AliasTarget.Ready`
разрешает конфликт для **обоих** aliases: runtime-значения одинаковы.
Две отдельные ветки для Ready и Active не позволяют различить их и сохраняют
обычную диагностику C# о недостижимой ветке.

### 10. Маппинг enum в тот же enum не является безусловным копированием

```csharp
builder.Map<DomainStatus, DomainStatus>();
```

| Стратегия / правило | Вход | Результат |
|---|---|---|
| Default ByName | `Active` | `Active` |
| Default ByName | `(DomainStatus)123` | Исключение маппинга |
| `ByValue` | `(DomainStatus)123` | Исключение маппинга |
| `ByValueAllowUndefined` | `(DomainStatus)123` | Неназванное число 123 |

Явное правило также сохраняется при E → E:

```csharp
builder.Map<DomainStatus, DomainStatus>()
    .Members(status => status switch
    {
        DomainStatus.Active => DomainStatus.Pending
    });
```

Active даёт Pending; явное правило не обходится identity-оптимизацией.

## Выражения и управление выполнением

### 11. Прямое выражение задаёт готовый результат

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status => Compute(status));
```

Если `Compute` всегда возвращает `ApiStatus.Disabled`, то `Active`, `Legacy` и
неназванное число 123 дают `Disabled`. Compute вызывается один раз; исключение из
него передаётся вызывающему коду. Конвенция не переопределяет прямой результат.

### 12. Mapping switch в local и alias

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status =>
    {
        var mapped = status switch
        {
            DomainStatus.Cancelled => ApiStatus.Deleted,
            _ => Auto<ApiStatus>()
        };
        var returned = mapped;
        return returned;
    });
```

| Вход | Результат |
|---|---|
| `Cancelled` | `Deleted` |
| `Active` | `Active` |
| `Legacy` | Исключение маппинга |

Перенос возвращаемого switch в неизменяемый local не отключает декларативный
разбор. `Auto<ApiStatus>()` здесь явно задаёт тип; смешанному switch под `var`
может не хватить target type для голого Auto.

### 13. If/else, логические условия и short-circuit

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status =>
    {
        if (status == DomainStatus.Active && IsBlocked())
            return ApiStatus.Disabled;

        return status switch
        {
            DomainStatus.Cancelled => ApiStatus.Deleted,
            _ => Auto()
        };
    });
```

| Вход | IsBlocked | Результат |
|---|---|---|
| `Active` | `true` | `Disabled` |
| `Active` | `false` | `Active` |
| `Cancelled` | Не вызывается | `Deleted` |
| `Legacy` | Не вызывается | Исключение маппинга |

### 14. Switch statement с полными return-путями

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status =>
    {
        switch (status)
        {
            case DomainStatus.Cancelled:
                return ApiStatus.Deleted;
            case DomainStatus.Corrupt:
                throw new InvalidOperationException();
            default:
                return Auto();
        }
    });
```

`Cancelled → Deleted`, `Active → Active`, `Legacy → исключение маппинга`,
`Corrupt → InvalidOperationException`. Поддержка управляющей конструкции не
отменяет обычные C# требования: нельзя удалить default и оставить путь без return.

### 15. Conditional expression выбирает декларативный путь

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status => IsBlocked()
        ? ApiStatus.Disabled
        : status switch
        {
            DomainStatus.Cancelled => ApiStatus.Deleted,
            _ => Auto<ApiStatus>()
        });
```

| Вход | IsBlocked | Результат |
|---|---|---|
| `Active` | `true` | `Disabled` |
| `Active` | `false` | `Active` |
| `Cancelled` | `false` | `Deleted` |

Условие вычисляется один раз; невыбранный путь не выполняется.

### 16. Switch над вычисленным значением не меняет source конвенции

`Normalize` всегда возвращает `DomainStatus.Pending` и считает вызовы.

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status => Normalize(status) switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted,
        _ => Auto()
    });
```

| Исходный вход | Результат | Normalize |
|---|---|---|
| `Active` | `Active`, не Pending | Один вызов |
| `Legacy` | Исключение маппинга, хотя Normalize вернул Pending | Один вызов |

Если Normalize вернёт Cancelled, явная ветка даст Deleted для любого исходного
входа. Если Normalize бросит, исключение не становится fallback.

### 17. Изменённая переменная source и вложенный обычный switch

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status =>
    {
        status = DomainStatus.Pending;
        return status switch
        {
            DomainStatus.Cancelled => ApiStatus.Deleted,
            _ => Auto()
        };
    });
```

Исходный `Active` даёт `Active`: Auto использует исходный source единицы,
а явные patterns видят присвоенный Pending.

Отдельный вариант:

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status => status switch
    {
        DomainStatus.Cancelled => GetCode() switch
        {
            1 => ApiStatus.Deleted,
            _ => ApiStatus.Archived
        },
        _ => ApiStatus.Unknown
    });
```

`Cancelled` при коде 1 даёт Deleted, при коде 2 — Archived; `Active` даёт Active
без GetCode. Внутренний switch остаётся C#: удаление его `_` сохраняет обычное
предупреждение о неполноте. Конвенция туда не добавляется.

## Обычные enum и строки

### 18. Имена на выходе и независимое обратное направление

```csharp
builder.Map<DomainStatus, string>()
    .Members(status => status switch { DomainStatus.Cancelled => "removed" });

builder.Map<string, DomainStatus>()
    .Members(text => text switch
    {
        "removed" or "deleted" => DomainStatus.Cancelled
    });
```

| Направление и вход | Результат |
|---|---|
| `Active → string` | `"Active"` |
| `Cancelled → string` | `"removed"` |
| `(DomainStatus)123 → string` | Исключение маппинга |
| `"removed"`, `"deleted" → DomainStatus` | `Cancelled` |
| `"ACTIVE" → DomainStatus` | `Active` по конвенции |
| `"REMOVED" → DomainStatus` | Исключение: явный string pattern регистрозависим |

Обратная регистрация нужна отдельно; из первой она не строится автоматически.

### 19. Обычный строковый enum не получает flags-parser

```csharp
builder.Map<string, DomainStatus>()
    .Members(text => text switch { _ => DomainStatus.Unknown });
```

| Вход | Результат |
|---|---|
| `"Active"`, `"active"` | `Active` |
| `" Active "`, `""`, `"   "` | `Unknown` через fallback |
| `"2"`, `"0"`, `"0x2"` | `Unknown` через fallback |
| `"DomainStatus.Active"` | `Unknown` через fallback |
| `"Active,Pending"` | `Unknown` через fallback |

Если нужен trim для обычного enum, его задаёт пользовательский алгоритм;
одного `text.Trim() switch { _ => Auto() }` недостаточно: Auto берёт исходный text.

### 20. Неоднозначность регистра для конкретной входной строки

```csharp
enum TextStatus { Unknown = 0, Ready = 1, READY = 2 }

builder.Map<string, TextStatus>()
    .Members(text => text switch { _ => TextStatus.Unknown });
```

| Вход | Результат |
|---|---|
| `"Ready"` | Число 1 |
| `"READY"` | Число 2 |
| `"ready"`, `"rEaDy"` | Unknown: неуспех ignore-case конвенции |
| `"ready"` без fallback | Исключение маппинга |
| `"ready"` с явной веткой `"ready" => TextStatus.Ready` | Число 1 |

Статически известный конфликт дополнительно диагностируется; runtime не должен
выбирать первое объявление. Конкретный ID/severity новой диагностики здесь не задаётся.

### 21. Canonical output при aliases

```csharp
enum AliasedStatus { Ready = 1, Active = 1 }

builder.Map<AliasedStatus, string>()
    .Members(status => status switch { AliasedStatus.Ready => "ready" });
```

`Ready` и `Active` оба дают `"ready"`. Без явной ветки ByName требует диагностики:
по числу 1 нельзя восстановить, какое имя написал вызывающий код.
Явный canonical output нужен и для aliases, отличающихся только регистром.

### 22. Числовое представление enum в строке

```csharp
enum SignedCode : long { MinusOne = -1, Active = 2 }
enum WideCode : ulong { Maximum = ulong.MaxValue }

builder.Map<SignedCode, string>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValue);
builder.Map<WideCode, string>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValue);
```

| Вход | Результат |
|---|---|
| `SignedCode.Active` | `"2"` |
| `(SignedCode)123` | `"123"`, объявленность здесь не требуется |
| `SignedCode.MinusOne` | `"-1"` |
| `WideCode.Maximum` | `"18446744073709551615"` |

ByValueAllowUndefined даёт те же строки. Culture не добавляет группировку и не
меняет знак. Обратный string → enum автоматически такой numeric text не разбирает.

### 23. Пользовательский формат и явный числовой alias

```csharp
builder.Map<DomainStatus, string>()
    .Members(status => status switch
    {
        DomainStatus.Active => "STATUS_ACTIVE",
        _ => "unrecognized"
    });

builder.Map<string, DomainStatus>()
    .Members(text => text switch
    {
        "STATUS_ACTIVE" or "2" => DomainStatus.Active,
        _ => DomainStatus.Unknown
    });
```

| Направление и вход | Результат |
|---|---|
| `Active → string` | `"STATUS_ACTIVE"` |
| `Pending → string` | `"Pending"`, конвенция раньше fallback |
| `"STATUS_ACTIVE"`, `"2" → DomainStatus` | `Active` |
| `"STATUS_PENDING"`, `"02" → DomainStatus` | `Unknown` |

Массовая обработка префиксов, snake_case и wire attributes не возникает из этих
двух явных aliases. Для полностью своего форматирования подходит прямой Members
или Convert.

## Числа и диапазоны

Типы для следующих примеров:

```csharp
enum WireCode : ushort { Unknown = 0, Active = 1, Pending = 2, Large = 300 }
enum StoredCode : byte { Unknown = 0, Pending = 1, Active = 2 }
```

### 24. По имени или по числу при переставленных кодах

```csharp
builder.Map<WireCode, StoredCode>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValue);
```

| Вход | ByName | ByValue | ByValueAllowUndefined |
|---|---|---|---|
| `WireCode.Active = 1` | `StoredCode.Active = 2` | `Pending = 1` | `Pending = 1` |
| `WireCode.Pending = 2` | `Pending = 1` | `Active = 2` | `Active = 2` |
| `(WireCode)42` | Исключение | Исключение | Неназванное 42 |
| `WireCode.Large = 300` | Исключение | Исключение диапазона конвенции | То же, без усечения |

Все «исключения» здесь — исключения маппинга при отсутствии fallback,
а не обещание бросать `OverflowException` из неявной конвенции.

### 25. Integer → enum по умолчанию строгий

```csharp
builder.Map<int, StoredCode>()
    .Members(code => code switch { _ => StoredCode.Unknown });
```

| Вход | Default ByValue | С ByValueAllowUndefined |
|---|---|---|
| 1 | Pending | Pending |
| 2 | Active | Active |
| 42 | Unknown через fallback | Неназванное 42 |
| 255 | Unknown через fallback | Неназванное 255 |
| 256, 300, -1 | Unknown через fallback | Unknown через fallback |

### 26. Объявленность source для числовой конвенции не нужна

```csharp
enum SparseSource { Active = 1 }
enum ExpandedTarget { Active = 1, Archived = 2 }

builder.Map<SparseSource, ExpandedTarget>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValue);
```

| Вход | Результат |
|---|---|
| `SparseSource.Active` | `ExpandedTarget.Active` |
| `(SparseSource)2` | `ExpandedTarget.Archived` |
| `(SparseSource)3` | Исключение маппинга |

Неназванный source 2 допустим в runtime, но не закрывает destination coverage
для Archived. Пример с диагностикой приведён в разделе coverage.

### 27. Enum → integer сохраняет число и проверяет диапазон

```csharp
builder.Map<WireCode, byte>();
```

| Вход | Результат |
|---|---|
| `Active = 1` | byte 1 |
| `(WireCode)42` | byte 42 |
| `(WireCode)255` | byte 255 |
| `Large = 300` | Исключение маппинга |

У byte нет списка объявлений. EnumMappingStrategy для этой пары неприменима;
задавать её явно не нужно.

### 28. Знак и широкий ulong не теряются

```csharp
enum Signed : long { MinusOne = -1, Maximum = long.MaxValue }
enum Unsigned : ulong { Maximum = ulong.MaxValue }

builder.Map<Signed, ulong>();
builder.Map<Unsigned, long>();
```

| Пара и вход | Результат |
|---|---|
| `Signed → ulong`, MinusOne | Исключение, не ulong.MaxValue |
| `Signed → ulong`, Maximum | 9223372036854775807UL |
| `Unsigned → long`, Maximum | Исключение, не -1 |
| `Unsigned → long`, `(Unsigned)42` | 42L |

### 29. Overflow должен бросать, а неизвестный код — давать fallback

```csharp
builder.Map<WireCode, StoredCode>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValue)
    .Members(code => code switch
    {
        _ when (ushort)code > byte.MaxValue => throw new OverflowException(),
        _ => StoredCode.Unknown
    });
```

| Вход | Результат |
|---|---|
| `Active = 1` | Pending по числу |
| `(WireCode)42` | Unknown |
| `Large = 300` | Пользовательский OverflowException до конвенции |

### 30. Явные checked и unchecked сохраняют смысл

```csharp
builder.Map<WireCode, StoredCode>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValue)
    .Members(code => code switch
    {
        _ => unchecked((StoredCode)code)
    });
```

| Вход | Результат |
|---|---|
| `Active = 1` | Pending по конвенции |
| `(WireCode)42` | Неназванное 42 из явного fallback |
| `Large = 300` | Неназванное 44 из пользовательского unchecked |
| `Large = 300`, заменить на `checked` | OverflowException из пользовательского cast |

Неявная конвенция никогда сама не усекала 300 до 44. Явный результат не получает
повторной проверки объявленности.

### 31. Явный Auto не использует числовой fallback

```csharp
builder.Map<int, StoredCode>()
    .Members(code => code switch
    {
        42 => Auto(),
        _ => StoredCode.Unknown
    });
```

| Вход | Результат |
|---|---|
| 1 | Pending по неявной конвенции |
| 42 | Исключение маппинга |
| 43 | Unknown через fallback |
| 300 | Unknown через fallback диапазона |

### 32. Явный результат может быть неназванным

```csharp
builder.Map<int, StoredCode>()
    .Members(code => code switch
    {
        42 => (StoredCode)42,
        _ => StoredCode.Unknown
    });
```

42 даёт неназванное 42 даже при строгом ByValue; 43 даёт Unknown.
Строгость относится к конвенции, а не к валидации всего пользовательского кода.

## Nullable, Create, Update и фабрики

### 33. Nullable source проверяется до Members

```csharp
builder.Map<DomainStatus?, ApiStatus?>()
    .Members(status => status switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted,
        _ => Auto()
    });
```

| Вход / операция | Результат |
|---|---|
| Create null | null; Members не выполняется |
| Create Active | Active; callback видит non-null DomainStatus |
| Update null, previous = Archived | null по default ReturnNull |
| Update Cancelled, previous = null | Deleted; операция остаётся Update |

Регистрация `DomainStatus → ApiStatus` сама по себе не заменяет точную
nullable value-type пару из примера.

### 34. Все политики null source

Для `builder.Map<DomainStatus?, ApiStatus?>()` меняется только
`.NullSourceHandling(...)`.

| Политика | Create null | Update null, previous = Archived |
|---|---|---|
| ReturnNull | null | null |
| ReturnDestination | null | Archived |
| Throw | NullSourceException | NullSourceException |

В отдельной паре `DomainStatus? → ApiStatus` ReturnNull даёт `(ApiStatus)0`.
Если destination enum не объявляет ноль, всё равно возвращается default:
это null policy, а не успешная enum-конвенция.

### 35. Null destination и порядок guards

```csharp
builder.Map<DomainStatus?, ApiStatus?>()
    .NullDestinationHandling(NullDestinationHandling.Throw);
```

| Update: source / destination | Результат |
|---|---|
| Active / null | NullDestinationException |
| null / null | null: default source policy сработала первой |
| null / null, дополнительно NullSourceHandling.Throw | NullSourceException |
| Active / Unknown = 0 | Active: zero является имеющимся destination |

### 36. Previous доступен без фабрики

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members((status, previous) =>
    {
        if (previous.TryGetValue(out var old) && old == ApiStatus.Archived)
            return old;

        return Auto();
    });
```

| Операция | Результат |
|---|---|
| Create Active | Active; previous отсутствует |
| Update Active, Archived | Archived |
| Update Active, Unknown = 0 | Active; previous присутствует |
| Update Legacy, Archived | Archived без обращения к конвенции |
| Create Legacy | Исключение маппинга |

### 37. Четвёртая форма Members различает операции

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members((status, previous, result, context) =>
        context.Operation == MappingOperation.Create
            ? ApiStatus.Pending
            : ApiStatus.Archived);
```

Create Active даёт Pending; Update Active с любым non-null destination — Archived.
Параметр result не читается, поэтому его отсутствие на Create не является ошибкой.
Этот DSL context предоставляет Operation; полный `context.Mapper` доступен в Using/Convert.

### 38. ConstructUsing создаёт начальный результат только при необходимости

`CreateInitial` считает вызовы и возвращает `ApiStatus.Pending`.

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .ConstructUsing(CreateInitial)
    .MemberSelection(MemberSelection.Explicit)
    .Members((status, previous, result) => status switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted,
        _ => result
    });
```

| Операция | Результат | Вызовы фабрики |
|---|---|---|
| Create Active | Pending | 1 |
| Update Active, Archived | Archived | 0 |
| Create Cancelled | Deleted | 1, хотя Members заменяет результат |
| Update Cancelled, Archived | Deleted | 0 |

Для nullable destination Update Active с null и policy Create вызывает фабрику
один раз, даёт Pending и остаётся Update.

### 39. ResolveUsing всегда выбирает result, previous остаётся исходным

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .ResolveUsing((status, previous) => ApiStatus.Pending)
    .MemberSelection(MemberSelection.Explicit)
    .Members((status, previous, result) =>
        previous.TryGetValue(out var old) && old == ApiStatus.Archived
            ? old
            : result);
```

| Операция | Что видит Members | Результат |
|---|---|---|
| Create Active | previous отсутствует, result Pending | Pending |
| Update Active, Archived | previous Archived, result Pending | Archived |
| Update Active, Unknown | previous Unknown, result Pending | Pending |

ResolveUsing вызывается один раз во всех трёх случаях. Без чтения previous
из result нельзя восстановить старый destination.

### 40. Switch над result и кортежем всё равно использует исходный source в Auto

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .ResolveUsing((status, previous) => ApiStatus.Pending)
    .Members((status, previous, result) => result switch
    {
        ApiStatus.Disabled => ApiStatus.Archived,
        _ => Auto()
    });
```

| Исходный вход | Начальный result | Результат |
|---|---|---|
| Active | Pending | Active |
| Legacy | Pending | Исключение маппинга |

То же относится к завершающему Auto у `(status, result) switch` и
`(result, status) switch`: порядок элементов меняет только явные patterns.
Например, `(DomainStatus.Cancelled, ApiStatus.Pending) => ApiStatus.Deleted`
даёт Deleted до конвенции.

Вариант `_ => result` при Auto даст Active для исходного Active и Pending для
Legacy. При Explicit оба дадут Pending.

### 41. Ignore сохраняет начальный result

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .ConstructUsing(status => ApiStatus.Pending)
    .Members(status => status switch
    {
        DomainStatus.Cancelled => Ignore(),
        _ => Auto()
    });
```

| Операция | Результат |
|---|---|
| Create Cancelled | Pending |
| Update Cancelled, Archived | Archived |
| Create Active | Active по конвенции |

Если убрать фабрику и оставить Create включённым, Ignore для Cancelled требует
диагностики недоступного result. Он не изобретает ни ноль, ни default fallback.
Для пары только с Update и non-nullable destination существующий result доступен.

### 42. Фабрика без Members и terminal null

```csharp
builder.Map<DomainStatus, ApiStatus?>()
    .ResolveUsing((status, previous) =>
        status == DomainStatus.Corrupt ? null : (ApiStatus)123);
```

| Вход | Результат |
|---|---|
| Active | Неназванное 123; конвенция не запускается поверх фабрики |
| Corrupt | null |

Если добавить `.Members(status => ApiStatus.Active)`, Active даст Active, но
Corrupt всё равно даст null: terminal null пропускает Members. Исключение
фабрики тоже не перехватывается. Явный null из Members допустим для nullable
destination и не вызывает null policies повторно.

### 43. Update возвращает scalar, включая режим Update-only

```csharp
builder.Map<DomainStatus, ApiStatus>(MappingMode.Update)
    .Members((status, previous, result) => status switch
    {
        DomainStatus.Cancelled => Ignore(),
        _ => Auto()
    });
```

| Вызов | Результат |
|---|---|
| Update Cancelled, Archived | Archived |
| Update Active, Archived | Active |
| Create Active | MappingOperationNotSupportedException |

```csharp
var old = ApiStatus.Archived;
var mapped = mapper.Map<DomainStatus, ApiStatus>(DomainStatus.Active, old);
// old == ApiStatus.Archived; mapped == ApiStatus.Active
```

Для nullable Update-only пары с policy Create null destination также допустим,
если её правила не читают отсутствующий result либо выбирают его фабрикой;
включать MappingMode.Create для этого не требуется.

### 44. Convert владеет всем алгоритмом

```csharp
builder.Map<DomainStatus?, ApiStatus?>()
    .Convert(status => status is null ? ApiStatus.Unknown : ApiStatus.Disabled);
```

| Вход | Результат |
|---|---|
| null | Unknown, не default null policy |
| Active, Cancelled, Legacy | Disabled, без enum-конвенции |

Members и Using нельзя добавлять к локальному Convert. Его тело — обычный C#,
поэтому там нет автоматического дополнения switch и DSL Auto/Ignore.

## Наследование и настройки

### 45. Общие правила плюс локальные переопределения

```csharp
public abstract class StatusMapperBase<TMapper> : TypeMapper<TMapper>
    where TMapper : StatusMapperBase<TMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<DomainStatus, ApiStatus>()
            .Members(status => status switch
            {
                DomainStatus.Cancelled => ApiStatus.Deleted,
                DomainStatus.Suspended => ApiStatus.Disabled,
                _ => ApiStatus.Unknown
            });
}

[MorphantMapper]
public partial class ApplicationMapper : StatusMapperBase<ApplicationMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        base.Configure(builder);
        builder.Map<DomainStatus, ApiStatus>()
            .IncludeBase<DomainStatus, ApiStatus>()
            .Members(status => status switch
            {
                DomainStatus.Cancelled => ApiStatus.Archived,
                _ => ApiStatus.Unrecognized
            });
    }
}
```

| Вход | Результат |
|---|---|
| Cancelled | Archived из локального правила |
| Suspended | Disabled из базы |
| Active | Active по общей конвенции |
| Legacy | Unrecognized из локального fallback |

Удаление локального fallback вернёт Unknown для Legacy. Локальный Members не
стирает Suspended из базы. Enum-наследования типов здесь нет: включается та же
пара из базового mapper.

### 46. Guard и явный Auto перекрывают базовое правило по-разному

В примере 45 заменить локальный Members на:

```csharp
.Members(status => status switch
{
    _ when IsBlocked() => ApiStatus.Archived,
    _ => Auto()
})
```

| Вход | IsBlocked | Результат |
|---|---|---|
| Suspended | true | Archived: локальный guard раньше базового case |
| Suspended | false | Disabled из базы |
| Active | false | Active |
| Legacy | false | Исключение: локальный `_ => Auto()` заменил базовый fallback |

Если вместо guard написать `DomainStatus.Cancelled => Auto()`, Cancelled бросит
исключение: явный Auto не возвращается к базовому `Cancelled => Deleted`.

### 47. Locals базы вычисляются только при достижении базы

Для той же конструкции наследования тела Members задаются так:

```csharp
// База; GetBaseFallback возвращает Unknown и считает вызовы.
.Members(status =>
{
    var fallback = GetBaseFallback(status);
    return status switch
    {
        DomainStatus.Suspended => ApiStatus.Disabled,
        _ => fallback
    };
})

// Локальная пара; GetLocalFallback возвращает Unrecognized и считает вызовы.
.Members(status =>
{
    var fallback = GetLocalFallback(status);
    return status switch
    {
        DomainStatus.Cancelled => ApiStatus.Archived,
        _ => fallback
    };
})
```

| Вход | Результат | Local / Base вызовы |
|---|---|---|
| Cancelled | Archived | 1 / 0 |
| Suspended | Disabled | 1 / 1 |
| Active | Active | 1 / 1 |
| Legacy | Unrecognized | 1 / 1; local не вычисляется повторно ради fallback |

### 48. Настройка пары базы выше настройки текущего mapper

В базовой паре из примера 45 явно задано
`.MemberSelection(MemberSelection.Auto)`. Текущая конфигурация:

```csharp
base.Configure(builder);
builder.MemberSelection(MemberSelection.Explicit);
builder.Map<DomainStatus, ApiStatus>()
    .IncludeBase<DomainStatus, ApiStatus>();
```

| Локальное дополнение к паре | Effective selection | Active | Legacy |
|---|---|---|---|
| Нет | Auto из included pair | Active | Unknown |
| `.MemberSelection(Explicit)` | Explicit | Unknown | Unknown |
| `.MemberSelection(Explicit).MemberSelection(Default)` | Auto из included pair | Active | Unknown |

В таблице имена значений сокращены; в C# используются
`MemberSelection.Explicit` и `MemberSelection.Default`. Последняя запись уровня
побеждает, Default продолжает поиск. Положение mapper-level вызова до/после Map
не должно менять результаты.

### 49. Default не сбрасывает унаследованную числовую стратегию

```csharp
builder.EnumMappingStrategy(EnumMappingStrategy.ByValueAllowUndefined);
builder.Map<int, StoredCode>()
    .EnumMappingStrategy(EnumMappingStrategy.Default);
```

42 даёт неназванное 42. Чтобы получить строгий режим, нужно явно поставить
`EnumMappingStrategy.ByValue`; тогда 42 без fallback бросает.

Отдельная конфигурация: в MSBuild задано
`MorphantEnumMappingStrategy=ByValueAllowUndefined`, а в mapper — ByName.

```csharp
builder.EnumMappingStrategy(EnumMappingStrategy.ByName);
builder.Map<int, StoredCode>();
```

Общий ByName для integer → enum приводит к строгому default ByValue:
1 даёт Pending, 42 бросает. Нижний MSBuild AllowUndefined повторно не ищется.

### 50. Несколько уровней используют одну конвенцию и один result

Для цепочки `Base → Middle → Application` каждый уровень вызывает
`base.Configure(builder)` и включает точную пару через IncludeBase.

| Уровень | Members |
|---|---|
| Base | `Cancelled => Deleted`, `Suspended => Disabled`, `_ => Unknown` |
| Middle | `Cancelled => Archived` |
| Application | `Corrupt => Unrecognized` |

Это те же формы switch, что в примере 45, с квалифицированными именами enum.

| Вход | Результат |
|---|---|
| Cancelled | Archived: ближайший специальный case |
| Suspended | Disabled |
| Corrupt | Unrecognized |
| Active | Active: одна конвенция после всех специальных правил |
| Legacy | Unknown: ближайший имеющийся fallback |

Если Base задаёт ResolveUsing, он выбирает начальный result один раз; все уровни
видят этот result. Выбранное Deleted не передаётся в Middle как новый source.

## Flags между enum

Общие типы этого раздела:

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
```

### 51. Переименование флагов действует на любую комбинацию

```csharp
builder.Map<SourceAccess, TargetAccess>()
    .Members(flag => flag switch
    {
        SourceAccess.Read => TargetAccess.View,
        SourceAccess.Write => TargetAccess.Edit,
        SourceAccess.Delete => TargetAccess.None,
        _ => TargetAccess.Unknown
    });
```

| Вход | Результат default ByBit + ByName |
|---|---|
| Read | View = 16 |
| ReadWrite = 3 | View \| Edit = 48 |
| Read \| Audit = 9 | View \| Audit = 80 |
| Read \| Delete = 5 | View = 16 |
| Read \| Write \| Audit = 11 | View \| Edit \| Audit = 112 |
| None | None = 0 |

### 52. Один бит может дать несколько флагов или совпадающие вклады

```csharp
builder.Map<SourceAccess, TargetAccess>()
    .Members(flag => flag switch
    {
        SourceAccess.Read => TargetAccess.View | TargetAccess.Audit,
        SourceAccess.Write => TargetAccess.View,
        SourceAccess.Delete => default,
        _ => Auto()
    });
```

| Вход | Результат |
|---|---|
| Read | View \| Audit = 80 |
| Read \| Write | View \| Audit = 80: повторный View не ошибка |
| Delete | 0 |
| Delete \| Audit | Audit = 64 |

Для явного enum-нуля также подходят `TargetAccess.None` и `(TargetAccess)0`.
Голое `=> 0` — ограничение типизации scalar marker, а не другой runtime-контракт.

### 53. Неизвестный бит проходит обычные правила

Используется конфигурация 51.

| Вход | С fallback Unknown | Если удалить fallback |
|---|---|---|
| `(SourceAccess)16` | Unknown = 128 | Исключение |
| Read \| (SourceAccess)16 | View \| Unknown = 144 | Исключение всей операции |
| (SourceAccess)16 \| (SourceAccess)32 | Unknown = 128 | Исключение |

Явная ветка `(SourceAccess)16 => TargetAccess.Edit` позволяет обработать этот
бит до конвенции; тогда Read \| 16 даст View \| Edit = 48. Совпадение исходного
числа 16 с TargetAccess.View само по себе не создаёт ByName-соответствие.

### 54. Composite case требует ByMask

```csharp
builder.Map<SourceAccess, TargetAccess>()
    .Members(flag => flag switch
    {
        SourceAccess.ReadWrite => TargetAccess.View | TargetAccess.Edit,
        _ => TargetAccess.Unknown
    });
```

| Режим | Вход ReadWrite | Диагностика |
|---|---|---|
| ByBit | Unknown: Read и Write отдельно дошли до fallback | Warning о доказанно недостижимом composite case |
| `.FlagsMappingMode(FlagsMappingMode.ByMask)` | View \| Edit = 48 | Composite case достижим |

Для ByMask вход Read всё ещё даёт Unknown: правило ReadWrite не является
правилом для каждого входящего бита.

### 55. ByMask + ByName сопоставляет целое имя

```csharp
[Flags] enum NamedSource { Read = 1, Write = 2, ReadWrite = 3, Audit = 4 }
[Flags] enum NamedTarget { Read = 8, Write = 16, ReadWrite = 128, Audit = 32 }

builder.Map<NamedSource, NamedTarget>();
```

| Вход | ByBit | ByMask |
|---|---|---|
| Read | Read = 8 | Read = 8 |
| ReadWrite = 3 | Read \| Write = 24 | ReadWrite = 128 |
| Read \| Audit = 5, отдельного имени нет | Read \| Audit = 40 | Исключение |
| 0, None не объявлен | 0 | 0 |

Добавление/удаление имени ReadWrite не меняет побитовый результат 24. ByMask не
пытается собрать неназванную пятёрку по отдельным именам.

### 56. Только составное объявление без отдельных имён битов

```csharp
[Flags] enum PairSource { Pair = 3 }
[Flags] enum PairTarget { Pair = 12 }

builder.Map<PairSource, PairTarget>();
```

| Режим | Вход Pair = 3 | Вход 0 |
|---|---|---|
| ByBit + ByName | Исключение: у source-битов 1 и 2 нет имён | 0 |
| ByMask + ByName | Pair = 12 | 0 |

В ByBit можно задать явные `(PairSource)1 => (PairTarget)4` и
`(PairSource)2 => (PairTarget)8`; тогда итогом станет 12. Наличие Pair = 3 само
по себе не создаёт такие правила.

### 57. Нулевая маска проходит правила один раз

```csharp
builder.Map<SourceAccess, TargetAccess>()
    .Members(flag => flag switch
    {
        SourceAccess.None => TargetAccess.Unknown,
        SourceAccess.Read => TargetAccess.View,
        _ => Auto()
    });
```

| Вход | Результат |
|---|---|
| None = 0 | Unknown = 128 |
| Read = 1 | View = 16; правило None не добавляет Unknown |
| Read \| Audit = 9 | View \| Audit = 80 |

Без явного None ноль по конвенции даёт 0, даже если ни одна сторона не объявляет
имя нуля. Это действует в ByBit и ByMask для любой применимой enum-стратегии.

### 58. Explicit отключает конвенцию, сохраняя разбиение

```csharp
builder.Map<SourceAccess, TargetAccess>()
    .MemberSelection(MemberSelection.Explicit)
    .Members(flag => flag switch
    {
        SourceAccess.Read => TargetAccess.View,
        SourceAccess.Audit => Auto()
    });
```

| Вход | Результат |
|---|---|
| Read \| Audit | View \| Audit = 80 |
| Write | Исключение |
| 0 | Исключение: специальная конвенция нуля тоже отключена |
| 0, добавить `SourceAccess.None => Auto()` | 0 |

### 59. Эффекты выполняются по физическим битам, не по объявлениям

`Record` добавляет вход в список и возвращает TargetAccess.None.

```csharp
builder.Map<SourceAccess, TargetAccess>()
    .Members(flag => Record(flag));
```

| Вход | Список Record | Результат |
|---|---|---|
| Read \| Delete \| Audit = 13 | Read, Delete, Audit | 0 |
| ReadWrite = 3 | Read, Write | 0 |
| 0 | 0 один раз | 0 |

Alias `View = Read` в source не добавил бы вызова. Если Record бросает на Delete,
Read уже обработан, Audit не обрабатывается; частичный результат не возвращается,
а запись Read в список сохраняется.

### 60. Null из одного бита завершает nullable mapping

```csharp
builder.Map<SourceAccess, TargetAccess?>()
    .Members(flag => flag switch
    {
        SourceAccess.Read => TargetAccess.View,
        SourceAccess.Write => null,
        SourceAccess.Delete => RecordDelete(),
        _ => Auto()
    });
```

`RecordDelete` считает вызовы и возвращает TargetAccess.None.

| Вход | Результат | RecordDelete |
|---|---|---|
| Read | View | Не вызывается |
| Read \| Write \| Delete | null | Не вызывается |
| Delete | 0 | Один вызов |

Для удаления Write с сохранением остальных битов нужно вернуть 0 вместо null.

### 61. Фабрика работает один раз, result не является накопителем

```csharp
builder.Map<SourceAccess, TargetAccess>()
    .ResolveUsing((mask, previous) => TargetAccess.Audit)
    .Members((flag, previous, result) => flag switch
    {
        SourceAccess.Read => TargetAccess.View,
        SourceAccess.Write => result,
        SourceAccess.Delete => Ignore(),
        _ => Auto()
    });
```

| Вход | Результат |
|---|---|
| Read | View = 16: Audit из фабрики не добавляется автоматически |
| Read \| Write | View \| Audit = 80 |
| Read \| Delete | View \| Audit = 80: Ignore даёт полную начальную маску |
| Write \| Delete | Audit = 64 |

Фабрика получает полную source-маску и вызывается один раз. Каждый бит видит
result = Audit, даже после вклада View. Для пропуска Delete нужен None/default,
а не Ignore. Previous по-прежнему означает полную исходную destination-маску.

### 62. Composite pattern над result может быть достижим

```csharp
builder.Map<SourceAccess, TargetAccess>()
    .ResolveUsing((mask, previous) => TargetAccess.View | TargetAccess.Edit)
    .Members((flag, previous, result) => result switch
    {
        (TargetAccess.View | TargetAccess.Edit) => TargetAccess.Audit,
        _ => Auto()
    });
```

Для Read \| Write оба прохода видят полный result = 48 и возвращают Audit;
итог Audit = 64. Это достижимый case, поэтому warning «composite недостижим
в ByBit» здесь неверен. Аналогично нельзя объявлять недостижимым composite,
получаемый через пользовательский `Normalize(flag)`.

## Flags и числовые стратегии

### 63. Строгий integer → flags проверяет OR целых объявлений

```csharp
[Flags] enum PairAudit { Pair = 3, Audit = 4 }

builder.Map<int, PairAudit>();
```

| Вход | Default ByValue | ByValueAllowUndefined |
|---|---|---|
| 0 | 0, пустая комбинация | 0 |
| 3 | Pair | Pair |
| 4 | Audit | Audit |
| 7 | Pair \| Audit, хотя 7 отдельно не объявлено | То же |
| 1, 2, 5, 6 | Исключение | Соответствующее неназванное число |

В строгом режиме нельзя взять только часть Pair. FlagsMappingMode здесь не нужен:
integer → flags обрабатывает целое число.

### 64. All = -1 не разрешает любое число

```csharp
[Flags] enum AllOnly : sbyte { All = -1 }

builder.Map<int, AllOnly>();
```

| Вход | ByValue | ByValueAllowUndefined |
|---|---|---|
| -1 | All | All |
| 0 | 0 | 0 |
| 1, 127, -128 | Исключение | Соответствующее число |
| 128, -129, 255 | Исключение | Исключение диапазона; 255 не превращается в -1 |

### 65. Обычный enum → flags использует допустимость destination

```csharp
enum WireAccess { ReadWrite = 3 }
[Flags] enum Bits { Read = 1, Write = 2 }

builder.Map<WireAccess, Bits>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValue);
```

| Вход | Результат |
|---|---|
| ReadWrite = 3 | Bits.Read \| Bits.Write = 3 |
| `(WireAccess)0` | 0 |
| `(WireAccess)1` | Bits.Read |
| `(WireAccess)4` | Исключение |

Это целое значение: flags только на одной стороне. При ByName ReadWrite не
сопоставляется автоматически отдельным Read и Write. Если убрать `[Flags]`
у Bits, строгий ByValue для числа 3 тоже бросит: обычному enum нужно точное объявление.

### 66. ByBit и ByMask дают разные результаты строгой проверки

```csharp
[Flags] enum SourceBits { Read = 1, Write = 2 }
[Flags] enum DestinationPair { Pair = 3 }

builder.Map<SourceBits, DestinationPair>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValue);
```

| Вход | ByBit | ByMask |
|---|---|---|
| Read \| Write = 3 | Исключение: 1 отдельно не допустимо | Pair = 3 |
| Read = 1 | Исключение | Исключение |
| 0 | 0 | 0 |

ByValueAllowUndefined даёт 3 в обоих режимах, но число вызовов правил остаётся
разным. В ByBit явные `Read => (DestinationPair)1` и `Write => (DestinationPair)2`
также дают 3: дополнительная строгая проверка поверх явных вкладов не выполняется.

### 67. Проверка диапазона зависит от единицы обработки

```csharp
[Flags] enum WideMask : int { All = -1 }
[Flags] enum NarrowMask : sbyte { All = -1 }

builder.Map<WideMask, NarrowMask>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValueAllowUndefined);
```

| Режим / вход | Результат |
|---|---|
| ByMask, All = -1 | NarrowMask.All = -1: целое число помещается |
| ByBit, All = -1 | Исключение: уже отдельный положительный бит 128 не помещается в sbyte |
| ByBit, `(WideMask)64` | Неназванное 64 |

Побитовый обход не делает narrowing cast всей маски заранее.

### 68. Знаковый старший бит сохраняет математическое значение

```csharp
[Flags] enum SignedBits : sbyte { Low = 1, High = -128 }
[Flags] enum LargerBits : short { Low = 1, High = -128 }
[Flags] enum UnsignedBits : byte { Low = 1, High = 128 }

builder.Map<SignedBits, LargerBits>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValueAllowUndefined);
builder.Map<SignedBits, UnsignedBits>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValueAllowUndefined);
```

| Пара / вход | Результат |
|---|---|
| SignedBits → LargerBits, High | -128 |
| SignedBits → LargerBits, Low \| High | -127: вклады 1, затем -128 |
| SignedBits → UnsignedBits, High | Исключение: -128 не равно 128 |

При ByName последняя пара может дать High = 128 по имени. В sbyte обходится
ровно восемь физических битов; дополнительного расширения до 32/64 проходов нет.
Для ulong-флага `1UL << 63` сохраняется положительное 9223372036854775808,
поэтому числовое сопоставление в long не помещается.

### 69. IncludeBase не создаёт смешанного прохода маски и битов

Для точной пары SourceAccess → TargetAccess база задаёт ByMask и
`ReadWrite => View | Edit`, `_ => Unknown`. Локальная пара включает её:

```csharp
base.Configure(builder);
builder.Map<SourceAccess, TargetAccess>()
    .IncludeBase<SourceAccess, TargetAccess>()
    .FlagsMappingMode(FlagsMappingMode.ByBit)
    .Members(flag => flag switch { SourceAccess.Read => TargetAccess.View });
```

| Effective mode | Вход ReadWrite | Что происходит |
|---|---|---|
| Локальный ByBit | View \| Unknown = 144 | Read — local; Write — inherited fallback; базовый composite недостижим и предупреждается |
| Вместо ByBit указать Default | View \| Edit = 48 | Наследуется ByMask; базовый composite совпал |

Ни в одном варианте сначала не применяется whole-mask правило, а затем те же
Members ещё раз к отдельным битам.

## Строка в flags

Общий enum для обоих строковых направлений:

```csharp
[Flags]
enum Access
{
    None = 0, Read = 1, Write = 2, ReadWrite = Read | Write,
    Audit = 4, Unknown = 8
}
```

### 70. Три разделителя, их смеси и пробелы вокруг элементов

```csharp
builder.Map<string, Access>();
```

| Вход | Результат |
|---|---|
| `"Read,Write"`, `"Read, Write"` | Read \| Write = 3 |
| `"Read\|Write"`, `"Read \| Write"` | 3 |
| `"Read;Write"`, `"Read ; Write"` | 3 |
| `" Read ; Write\|Audit,Read "` | Read \| Write \| Audit = 7 |
| `"read,WRITE"` | 3 |
| `"\tRead\r\n, Write\t"` | 3: whitespace вокруг токенов обрезается |
| `"Read Write"`, `"Read\tWrite"`, `"Read\nWrite"` | Исключение: whitespace внутри не разделяет имена |
| `"Read+Write"` | Исключение: плюс не разделитель |

Строки в таблице записаны как C# literals: `\t`, `\r`, `\n` обозначают реальные
пробельные символы на входе, а `\|` — обычный символ `|`, экранированный для Markdown.

### 71. Пустая строка, нулевой текст и пустые элементы списка

Та же регистрация без Members.

| Вход | Токены ByBit после trim | Результат |
|---|---|---|
| `""`, `"   "` | Один пустой токен | 0 |
| `"0"`, `" 0 "` | `"0"` | 0 |
| `"None"` | `"None"` | 0 по объявленному имени |
| `",;\|"` | Четыре пустых токена | 0 |
| `"Read,,Write;"` | `"Read"`, `""`, `"Write"`, `""` | 3 |
| `"Read,  ,Write"` | `"Read"`, `""`, `"Write"` | 3 |
| `"Read,0,None"` | Три токена | Read = 1 |

Если удалить None из enum, `""` и `"0"` сохраняют результат 0, а `"None"`
перестаёт быть конвенционным именем. В ByMask нулевые вклады конвенции те же,
но Members получает всю строку один раз.

### 72. Пользовательские aliases применяются к каждому токену

```csharp
builder.Map<string, Access>()
    .Members(part => part switch
    {
        "view" => Access.Read,
        "edit" => Access.Write,
        "Full control" => Access.ReadWrite,
        _ => Access.Unknown
    });
```

| Вход | Результат ByBit |
|---|---|
| `"view; Write"` | Read \| Write = 3 |
| `" view \| edit "` | 3: правила получают обрезанные токены |
| `"Full control;Audit"` | Read \| Write \| Audit = 7 |
| `"view,Missing"` | Read \| Unknown = 9 |
| `"VIEW"` | Unknown: string pattern регистрозависим, CLR-имени VIEW нет |
| `"READ"` | Read по регистронезависимому этапу конвенции |

### 73. Свой регистронезависимый alias через guard

```csharp
builder.Map<string, Access>()
    .Members(part => part switch
    {
        _ when string.Equals(part, "view", StringComparison.OrdinalIgnoreCase)
            => Access.Read,
        _ => Auto()
    });
```

`"VIEW;Write" → 3`, `" view " → 1`, `"missing" → исключение`.
Ложный guard не мешает обычному `"Write" → Access.Write`.

### 74. Пользователь может запретить пустые токены

```csharp
builder.Map<string, Access>()
    .Members(part => part switch
    {
        "" => throw new FormatException("Empty flag"),
        _ => Auto()
    });
```

| Вход | Результат |
|---|---|
| `""`, `"  "`, `"Read,,Write"`, `"Read,"` | FormatException |
| `"Read,Write"` | 3 |
| `"0"` | 0: нулевой текст не является пустым токеном |

При ошибке после Read итоговая частичная маска не возвращается. Если нужно
запретить и нулевой текст, можно написать `"" or "0" => throw ...`.

### 75. Пустой токен можно заменить собственным вкладом

```csharp
builder.Map<string, Access>()
    .Members(part => part switch
    {
        "" => Access.Unknown,
        "0" => Access.Audit,
        _ => Auto()
    });
```

| Вход | Результат |
|---|---|
| `""`, `" "` | Unknown = 8 |
| `"Read,,Write;"` | Read \| Write \| Unknown = 11 |
| `"0"` | Audit = 4 |
| `"None"` | 0: объявленное имя не стало строкой `"0"` |
| `"Read,0"` | Read \| Audit = 5 |

Явные правила имеют приоритет и над специальной конвенцией пустоты/нуля.

### 76. Explicit сохраняет токенизацию, но выключает все неявные соответствия

```csharp
builder.Map<string, Access>()
    .MemberSelection(MemberSelection.Explicit)
    .Members(part => part switch
    {
        "view" => Access.Read,
        "Write" => Auto(),
        _ => Access.Unknown
    });
```

| Вход | Результат |
|---|---|
| `"view;Write"` | 3 |
| `"Read"`, `"write"` | Unknown = 8 |
| `""`, `"0"` | Unknown = 8, конвенция нуля отключена |
| `"view,,Write"` | Read \| Write \| Unknown = 11 |

Если заменить fallback на `_ => Auto()`, CLR-имена, пустота и `"0"` снова
обрабатываются по явному запросу конвенции; неизвестный токен бросает.

### 77. ByMask получает исходную строку до trim и разбиения

```csharp
builder.Map<string, Access>()
    .FlagsMappingMode(FlagsMappingMode.ByMask)
    .Members(text => text switch
    {
        "Read,Write" => Access.Audit,
        _ => Auto()
    });
```

| Вход | Результат |
|---|---|
| `"Read,Write"` | Audit = 4, точное правило всей строки |
| `"Read, Write"` | Read \| Write = 3 по конвенции |
| `" Read,Write "` | 3: целая строка не совпала с pattern |
| `"Read;Write"`, `"Read\|Write"` | 3 |
| `"Read,,Write;"` | 3 |

Напротив, ByBit всегда сначала делит строку: whole-string pattern с запятой
не является правилом для отдельного токена.

### 78. Пользовательский alias не становится частью ByMask-parser

Конфигурация 72 с `.FlagsMappingMode(FlagsMappingMode.ByMask)`.

| Вход | ByBit | ByMask |
|---|---|---|
| `"view"` | Read = 1 | Read = 1, совпала целая строка |
| `"view,Write"` | Read \| Write = 3 | Unknown = 8 |
| `"Read,Missing"` | Read \| Unknown = 9 | Unknown = 8, неуспех всей конвенции |
| `"Read,Write"` | 3 | 3 |
| `" view "` | Read = 1 | Unknown = 8: pattern всей строки не совпал, CLR-имени view нет |

Без fallback неуспех конвенции ByMask приводит к исключению. Явная ветка
`"view,Write" => Access.ReadWrite` может задать whole-string исключение.

### 79. Составное имя токена не запускает повторные правила по битам

```csharp
builder.Map<string, Access>()
    .Members(part => part switch
    {
        "Read" => Access.None,
        _ => Auto()
    });
```

| Вход | Результат ByBit |
|---|---|
| `"Read,Write"` | Write = 2: Read явно удалён |
| `"ReadWrite"` | ReadWrite = 3: одно объявленное имя |
| `"ReadWrite,Read"` | 3 |

В string → flags единица — токен, даже если он обозначает composite. Это не
рекурсивный flags → flags вызов.

### 80. Повторы и порядок токенов сохраняют эффекты

`Record` записывает строку в список и возвращает её без изменений.

```csharp
builder.Map<string, Access>()
    .Members(part =>
    {
        var recorded = Record(part);
        return recorded switch { _ => Auto() };
    });
```

| Вход | Вызовы Record по порядку | Результат |
|---|---|---|
| `"Write,Read,Read"` | `"Write"`, `"Read"`, `"Read"` | 3 |
| `"Read,,Read;"` | `"Read"`, `""`, `"Read"`, `""` | 1 |
| `"Read,Missing,Write"` | `"Read"`, `"Missing"` | Исключение; Write не достигнут |

В ByMask Record получил бы исходную строку один раз, а ошибка конвенции
относилась бы ко всему списку.

### 81. Using видит полную строку, Ignore — полную начальную маску

`InitialMask` получает вход, считает вызовы и возвращает Access.Audit.

```csharp
builder.Map<string, Access>()
    .ResolveUsing((text, previous) => InitialMask(text))
    .Members(part => part switch
    {
        "keep" => Ignore(),
        _ => Auto()
    });
```

| Вход | Результат | InitialMask |
|---|---|---|
| `"Read"` | Read = 1 | Один вызов с `"Read"`; Audit не добавляется |
| `"Read;keep"` | Read \| Audit = 5 | Один вызов с `"Read;keep"` |
| `"keep;keep"` | Audit = 4 | Один вызов; оба Ignore дают ту же маску |

Замена Ignore на Access.None превратила бы keep в пустой вклад. Если начальная
фабрика nullable destination вернула null, токенизация/правила не продолжаются.

### 82. Null токена останавливает весь строковый ввод

```csharp
builder.Map<string?, Access?>()
    .Members(part => part switch
    {
        "stop" => null,
        "boom" => throw new InvalidOperationException(),
        _ => Auto()
    });
```

| Вход | Результат |
|---|---|
| null | null по source policy, Members не выполняется |
| `""` | Non-null нулевая маска |
| `"Read;stop;boom"` | null; boom не достигается |
| `"Read;boom;stop"` | InvalidOperationException |

### 83. IncludeBase применяется отдельно для каждого токена

В базовом mapper точная пара string → Access содержит
`"view" => Access.Read`, `_ => Access.Unknown`. В текущем mapper:

```csharp
base.Configure(builder);
builder.Map<string, Access>()
    .IncludeBase<string, Access>()
    .Members(part => part switch { "edit" => Access.Write });
```

| Вход | Результат |
|---|---|
| `"view;edit;Audit"` | Read \| Write \| Audit = 7 |
| `"view;Missing"` | Read \| Unknown = 9 |
| `"edit;"` | Write = 2: пустой токен успешно дал 0 до inherited fallback |

Добавление локального `_ => Auto()` сохраняет базовый alias view, но заменяет
базовый fallback: `"view;Missing"` тогда бросает.

### 84. Числовой текст и имена с разделителем

```csharp
builder.Map<string, Access>()
    .Members(part => part switch
    {
        "3" => Access.ReadWrite,
        _ => Access.Unknown
    });
```

| Вход | Результат |
|---|---|
| `"3"`, `" 3 "` | 3 по явному правилу |
| `"0"` | 0 по специальной конвенции |
| `"03"`, `"00"`, `"+0"`, `"-0"`, `"0x3"` | Unknown: общего numeric parsing нет |
| `"Read,3"` | 3 |
| `"Access.Read"` | Unknown |

Настройка разделителя отложена. Если внешнее имя само содержит `;`, можно
обработать его целиком в ByMask:

```csharp
builder.Map<string, Access>()
    .FlagsMappingMode(FlagsMappingMode.ByMask)
    .Members(text => text switch { "Full;control" => Access.ReadWrite });
```

`"Full;control"` даст 3. В ByBit эта строка состоит из двух токенов и такой
whole-string pattern не сработает. Автоматического quote/escape-протокола нет.

## Flags в строку

### 85. Побитовый вывод и имя целой маски

```csharp
builder.Map<Access, string>();
```

| Вход | ByBit + ByName | ByMask + ByName |
|---|---|---|
| Read | `"Read"` | `"Read"` |
| ReadWrite = 3 | `"Read, Write"` | `"ReadWrite"` |
| Read \| Audit = 5 | `"Read, Audit"` | Исключение: имя всей маски не объявлено |
| ReadWrite \| Audit = 7 | `"Read, Write, Audit"` | Исключение |
| None = 0 | `"None"` | `"None"` |

ByMask не вызывает конвенцию для отдельных битов после неудачи целого имени.

### 86. Ноль с именем, без имени и с aliases

```csharp
[Flags] enum NoZeroName { Read = 1, Write = 2 }
[Flags] enum ZeroAliases { None = 0, Empty = 0, Read = 1 }

builder.Map<NoZeroName, string>();
builder.Map<ZeroAliases, string>()
    .Members(flag => flag switch { ZeroAliases.None => "none" });
```

| Тип и вход | Результат ByBit и ByMask |
|---|---|
| Access.None = 0 из регистрации 85 | `"None"`: объявленное имя приоритетно |
| `(NoZeroName)0` | `"0"` |
| ZeroAliases.None или Empty | `"none"` из явного правила |

Без явного canonical output у ZeroAliases требуется диагностика неоднозначного
имени. Пустая строка на **входе** всё равно означает ноль; это не требование
выводить любую нулевую маску как пустую строку.

### 87. Явный пустой вклад и полностью пустой результат

```csharp
builder.Map<Access, string>()
    .Members(flag => flag switch
    {
        Access.None => "",
        Access.Read => "",
        Access.Write => "",
        _ => Auto()
    });
```

| Вход | Результат |
|---|---|
| None | `""`, явное правило выше конвенционного имени |
| Read | `""` |
| ReadWrite | `""`, не `"0"` и не `", "` |
| Read \| Audit | `"Audit"`, без начального разделителя |
| Write \| Audit | `"Audit"` |

Пустой итог не нормализуется обратно в имя None или `"0"`.

### 88. Неизвестный бит при ByName не становится числом

```csharp
builder.Map<Access, string>()
    .Members(flag => flag switch { _ => "unknown" });
```

| Вход | ByBit | ByMask |
|---|---|---|
| Read | `"Read"` | `"Read"` |
| `(Access)16` | `"unknown"` | `"unknown"` |
| Read \| (Access)16 | `"Read, unknown"` | `"unknown"` |
| `(Access)16 \| (Access)32` | `"unknown, unknown"` | `"unknown"` |

Без fallback неизвестный бит/маска бросает. С явным `(Access)16 => Auto()`
число 16 бросает даже при наличии завершающего `"unknown"`.

### 89. Порядок строковых вкладов и повторы

```csharp
builder.Map<Access, string>()
    .Members(flag => flag switch
    {
        Access.Read => "access",
        Access.Write => "access",
        Access.Audit => "audit"
    });
```

ReadWrite даёт `"access, access"`, ReadWrite \| Audit —
`"access, access, audit"`. Вызовы следуют битам 1, 2, 4 независимо от порядка
объявлений и записи выражения `Audit | Write | Read`. Совпадающие строки не
удаляются; строковые вклады соединяются через `", "`.

### 90. Null строкового вклада завершает операцию

```csharp
builder.Map<Access, string?>()
    .Members(flag => flag switch
    {
        Access.Read => "read",
        Access.Write => null,
        Access.Audit => throw new InvalidOperationException(),
        _ => Auto()
    });
```

| Вход | Результат |
|---|---|
| Read | `"read"` |
| Read \| Write \| Audit | null; Audit не достигается |
| Read \| Audit | InvalidOperationException |

Null не является ещё одним пустым вкладом. Для исключения Write из строки с
продолжением обработки нужен `""`.

### 91. Числовой вывод форматирует целую маску

```csharp
builder.Map<Access, string>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValue)
    .Members(mask => mask switch { Access.ReadWrite => "rw" });
```

| Вход | Результат |
|---|---|
| Read | `"1"` |
| ReadWrite = 3 | `"rw"`, case целой маски |
| Read \| Audit = 5 | `"5"` |
| Read \| (Access)16 = 17 | `"17"` |
| None = 0 | `"0"`, стратегия числовая |

ByValueAllowUndefined даёт то же. Без Members ReadWrite даёт `"3"`.
Здесь нет строк `"1, 2"`, а pair-настройка FlagsMappingMode неприменима.

### 92. Фабричная строка не является аккумулятором

```csharp
builder.Map<Access, string>()
    .ResolveUsing((mask, previous) => "cached")
    .Members((flag, previous, result) => flag switch
    {
        Access.Read => "read",
        Access.Write => Ignore(),
        Access.Audit => result,
        _ => Auto()
    });
```

| Вход | Результат ByBit |
|---|---|
| Read | `"read"`, без автоматического добавления cached |
| ReadWrite | `"read, cached"` |
| Write \| Audit | `"cached, cached"` |

ResolveUsing получает всю маску один раз. Все result/Ignore читают одну начальную
строку cached; previous остаётся исходной строкой Update. Без Members фабрика
вернула бы один `"cached"` для любой маски.

### 93. Explicit относится и к выводу нулевой маски

```csharp
builder.Map<Access, string>()
    .MemberSelection(MemberSelection.Explicit)
    .Members(flag => flag switch
    {
        Access.Read => "r",
        Access.Write => Auto(),
        _ => "?"
    });
```

| Вход | Результат ByBit |
|---|---|
| ReadWrite | `"r, Write"` |
| Audit | `"?"` |
| None = 0 | `"?"`, конвенционное None отключено |
| None, добавить `Access.None => Auto()` | `"None"` |

Для NoZeroName такая явная нулевая ветка с Auto вернула бы `"0"`.

### 94. Чтение и запись нормализуют представление, а не сохраняют исходный текст

```csharp
builder.Map<string, Access>();
builder.Map<Access, string>();
```

| Строка → Access → строка | Промежуточное значение | Итог |
|---|---|---|
| `"Write;Read"` | 3 | `"Read, Write"` |
| `"ReadWrite"` | 3 | `"Read, Write"` |
| `"read\|Read"` | 1 | `"Read"` |
| `"Read,,Write;"` | 3 | `"Read, Write"` |
| `""`, `"0"`, `",;"` | 0 | `"None"` |

Для enum без объявления нуля последние входы дали бы `"0"`. В обратном порядке
Access → строка → Access эти обычные значения сохраняются, если обе пары
используют совместимые правила. Сохранения пробелов, разделителей и повторов
исходного текста этот контракт не обещает.

### 95. Свои строковые имена задаются в обоих направлениях

```csharp
builder.Map<Access, string>()
    .Members(flag => flag switch
    {
        Access.Read => "view",
        Access.Write => "edit"
    });
builder.Map<string, Access>()
    .Members(part => part switch
    {
        "view" => Access.Read,
        "edit" => Access.Write
    });
```

ReadWrite → `"view, edit"` → ReadWrite. Если не задать обратные aliases,
второе преобразование бросит. Если выходной Read возвращает `""`, то
ReadWrite → `"edit"` → Write: явное удаление вклада намеренно теряет информацию.
Числовая выходная строка `"3"` также не создаёт автоматический numeric parser
в обратной паре.
