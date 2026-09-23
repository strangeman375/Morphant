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

- [Обычные enum](#обычные-enum) — 01–10
- [Выражения и управление выполнением](#выражения-и-управление-выполнением) — 11–17
- [Обычные enum и строки](#обычные-enum-и-строки) — 18–23
- [Числа и диапазоны](#числа-и-диапазоны) — 24–32
- [Nullable, Create, Update и фабрики](#nullable-create-update-и-фабрики) — 33–44
- [Наследование и настройки](#наследование-и-настройки) — 45–50
- [Flags между enum](#flags-между-enum) — 51–62
- [Flags и числовые стратегии](#flags-и-числовые-стратегии) — 63–69
- [Строка в flags](#строка-в-flags) — 70–84
- [Flags в строку](#flags-в-строку) — 85–95
- [Coverage и намеренные исключения](#coverage-и-намеренные-исключения) — 96–107
- [Вложенные пары и границы API](#вложенные-пары-и-границы-api) — 108–118
- [Обычные типы и кортежи из enum и в enum](#обычные-типы-и-кортежи-из-enum-и-в-enum) — 119–132

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

## Coverage и намеренные исключения

### 96. Runtime-допустимость при flags в тот же enum

```csharp
builder.Map<Access, Access>();
```

| Вход | ByBit + ByName | ByMask + ByName | ByMask + ByValue |
|---|---|---|---|
| ReadWrite = 3 | 3 | 3, объявленное имя | 3 |
| Read \| Audit = 5 | 5, имена отдельных битов есть | Исключение: целого имени нет | 5, OR объявлений |
| `(Access)16` | Исключение | Исключение | Исключение |
| 0 | 0 | 0 | 0 |

ByValueAllowUndefined допускает 16. Никакая из этих стратегий не должна
заменяться безусловным identity-копированием. Включение/выключение coverage
не меняет эти runtime-результаты.

Общие типы следующих примеров:

```csharp
enum CoverageSource { Active = 1, Cancelled = 2, New = 3 }
enum CoverageTarget { Active = 10, Deleted = 20, Internal = 30 }
```

### 97. Source coverage замечает новое необработанное значение

```csharp
builder.Map<CoverageSource, CoverageTarget>()
    .UnmappedMemberValidation(UnmappedMemberValidation.Source)
    .Members(value => value switch
    {
        CoverageSource.Cancelled => CoverageTarget.Deleted
    });
```

| Вход | Runtime | Coverage |
|---|---|---|
| Active | Active по конвенции | Покрыт |
| Cancelled | Deleted | Покрыт |
| New | Исключение | Warning: объявленное source-значение не обработано |

При UnmappedMemberValidation.None warning исчезнет, но New всё ещё бросит.
Warning можно повысить до error обычными средствами; новая runtime-стратегия
для этого не требуется.

### 98. Явный запрет закрывает source coverage, Auto — не обязательно

В конфигурации 97 добавить одну из веток:

| Добавление | Вход New | Source coverage New |
|---|---|---|
| `CoverageSource.New => throw new InvalidOperationException()` | Пользовательское исключение | Закрыт: намеренный запрет |
| `CoverageSource.New => CoverageTarget.Deleted` | Deleted | Закрыт |
| `CoverageSource.New => Auto()` | Исключение маппинга | Не закрыт: конвенционного соответствия нет |
| `_ => CoverageTarget.Internal` | Internal | Закрыт для всех оставшихся source-значений |
| `_ => throw new InvalidOperationException()` | Пользовательское исключение | Закрыт для всех оставшихся source-значений |

Общий fallback удобен для forward compatibility, но coverage уже не предупредит
о появлении следующего source-значения, которое попадёт в этот fallback.

### 99. Destination coverage и many-to-one

```csharp
builder.Map<CoverageSource, CoverageTarget>()
    .UnmappedMemberValidation(UnmappedMemberValidation.Destination)
    .Members(value => value switch
    {
        CoverageSource.Cancelled or CoverageSource.New => CoverageTarget.Deleted
    });
```

Active → Active; Cancelled и New → Deleted. Destination Active и Deleted покрыты;
Internal даёт warning. Два source-значения для Deleted допустимы: обратимость
или взаимно однозначная таблица не требуется.

### 100. Discard исключает destination из coverage, сохраняя алгоритм

```csharp
builder.Map<CoverageSource, CoverageTarget>()
    .UnmappedMemberValidation(UnmappedMemberValidation.Strict)
    .Members(value =>
    {
        _ = CoverageTarget.Internal;
        return value switch
        {
            CoverageSource.Cancelled or CoverageSource.New => CoverageTarget.Deleted
        };
    });
```

| Вход | Результат |
|---|---|
| Active | Active |
| Cancelled, New | Deleted |
| `(CoverageSource)123` | Исключение |

Coverage этой таблицы закрыт: Internal намеренно исключён. Discard не добавляет
ветку, fallback или присваивание результата. Если позднее в destination появится
другое несопоставленное значение, Strict снова даст warning.

### 101. При E → E discard подтверждает только destination

```csharp
enum ReviewStatus { Active = 1, Hidden = 2 }

builder.Map<ReviewStatus, ReviewStatus>(MappingMode.Update)
    .MemberSelection(MemberSelection.Explicit)
    .UnmappedMemberValidation(UnmappedMemberValidation.Strict)
    .Members(status =>
    {
        _ = ReviewStatus.Hidden;
        return status switch { ReviewStatus.Active => ReviewStatus.Active };
    });
```

| Вариант | Update Hidden, previous = Active | Coverage |
|---|---|---|
| Код выше | Исключение | Hidden исключён только со стороны destination; source warning остаётся |
| Добавить `ReviewStatus.Hidden => Ignore()` | Active | Source Hidden обработан; coverage закрыт |
| Вместо Explicit выбрать Auto | Hidden | Конвенция сохранила Hidden; discard её не выключил |

Если в варианте с Ignore previous = Hidden, результат тоже Hidden. Ignore
подтверждает source-случай, но не означает «возвратить ноль».

### 102. Неназванный runtime-source не закрывает destination coverage

```csharp
enum DeclaredSource { Active = 1 }
enum DeclaredTarget { Active = 1, Archived = 2 }

builder.Map<DeclaredSource, DeclaredTarget>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValue)
    .UnmappedMemberValidation(UnmappedMemberValidation.Destination);
```

`Active → Active`, `(DeclaredSource)2 → Archived`. При этом Archived даёт
coverage warning: объявленного source-соответствия или явного правила для него
нет. Добавление `(DeclaredSource)2 => DeclaredTarget.Archived` в Members
явно описывает соответствие и закрывает этот пробел.

### 103. Coverage flags проверяет участие битов, а ноль — отдельно

```csharp
[Flags] enum CoverageFlags { None = 0, Read = 1, Write = 2, ReadWrite = 3 }
[Flags] enum CoverageFlagsDto
{
    None = 0, View = 16, Edit = 32, ViewEdit = 48, Audit = 64
}

builder.Map<CoverageFlags, CoverageFlagsDto>()
    .MemberSelection(MemberSelection.Explicit)
    .UnmappedMemberValidation(UnmappedMemberValidation.Strict)
    .Members(flag =>
    {
        _ = CoverageFlagsDto.Audit;
        return flag switch
        {
            CoverageFlags.None => CoverageFlagsDto.None,
            CoverageFlags.Read => CoverageFlagsDto.View | CoverageFlagsDto.Edit,
            CoverageFlags.Write => CoverageFlagsDto.View
        };
    });
```

| Вход | Результат | Coverage |
|---|---|---|
| None | None | Ноль покрыт отдельно |
| Read | View \| Edit = 48 | Вклад покрывает View, Edit и ViewEdit |
| Write | View = 16 | Обычное явное правило |
| ReadWrite | View \| Edit = 48 | Source composite покрыт через Read и Write |

Warnings нет. Не нужно отдельно доказывать возможность вернуть только Edit.
Если убрать ветку None, появится непокрытый ноль с обеих сторон; Audit по-прежнему
исключён discard. Если выбрать ByMask, ReadWrite потребует собственного
соответствия: правила Read и Write не обрабатывают целую тройку. Destination Edit
в ByMask также не покрыт: целое значение 32 ни одним из этих правил не возвращается.

### 104. Неизвестный guard не доказывает полное покрытие

```csharp
builder.Map<CoverageSource, CoverageTarget>()
    .UnmappedMemberValidation(UnmappedMemberValidation.Source)
    .Members(value => value switch
    {
        CoverageSource.Cancelled => CoverageTarget.Deleted,
        CoverageSource.New when IsEnabled() => CoverageTarget.Deleted
    });
```

| Вход | IsEnabled | Результат |
|---|---|---|
| New | true | Deleted |
| New | false | Исключение: конвенции для New нет |
| Active | Не вызывается | Active |

Source coverage должен учитывать false-путь и предупреждать о New. Самого
присутствия именованного case с произвольным guard недостаточно.

### 105. Динамический результат отличается от доказанно непокрытого значения

`ComputeTarget` в пользовательском коде для Active возвращает Active, для других
значений — Deleted.

```csharp
builder.Map<CoverageSource, CoverageTarget>()
    .UnmappedMemberValidation(UnmappedMemberValidation.Strict)
    .Members(value => ComputeTarget(value));
```

Active даёт Active, New даёт Deleted. Source обработан прямым результатом;
генератор не анализирует тело метода, чтобы доказать все destination-результаты.
Ожидается warning о границе destination-анализа, а не утверждение, что конкретное
значение заведомо недостижимо.

Аналогично `_ => result` с фабрикой не перечисляет destination-значения.
Наличие Using само по себе не доказывает полноту правил Members.

### 106. Aliases в coverage — одна физическая группа

```csharp
enum CoveredAlias { Ready = 1, Active = 1 }
enum CoveredAliasDto { Ready = 10, Available = 10 }

builder.Map<CoveredAlias, CoveredAliasDto>()
    .UnmappedMemberValidation(UnmappedMemberValidation.Strict)
    .Members(value => value switch { CoveredAlias.Ready => CoveredAliasDto.Ready });
```

Оба source-имени дают число 10. Отдельных warnings для Active и Available нет:
каждая сторона имеет одну физическую группу. Discard одного destination alias
тоже относится ко всей группе, а не создаёт runtime-различие её имён.

### 107. Для integer/string проверяется конечная enum-сторона

```csharp
builder.Map<int, StoredCode>()
    .UnmappedMemberValidation(UnmappedMemberValidation.Destination);
builder.Map<DomainStatus, string>()
    .UnmappedMemberValidation(UnmappedMemberValidation.Source);
```

В первой паре 0, 1 и 2 соответствуют всем объявленным StoredCode; destination
coverage закрыт. 42 всё равно бросает. Во второй паре каждый объявленный
DomainStatus имеет строковое имя, source coverage закрыт; `(DomainStatus)123`
при ByName всё равно бросает. Все возможные integers и строки не перечисляются
ради compile-time coverage.

## Вложенные пары и границы API

### 108. Enum-пара внутри обычного объекта

```csharp
class Order { public DomainStatus Status { get; set; } }
class OrderDto { public ApiStatus Status { get; set; } }

builder.Map<DomainStatus, ApiStatus>()
    .Members(status => status switch { DomainStatus.Cancelled => ApiStatus.Deleted });
builder.Map<Order, OrderDto>()
    .Members(source => new() { Status = Map(source.Status) });
```

| Order.Status | OrderDto.Status |
|---|---|
| Active | Active |
| Cancelled | Deleted |
| Legacy | Исключение вложенного mapping |

Обе точные пары должны быть доступны mapper. Замена Map на Auto не запускает
enum-пару: между разными enum нет implicit C# conversion. Для свойства того же
enum обычное копирование значения остаётся доступным. Get-only enum-свойство
нельзя изменить «по месту» через nested Update: scalar возвращает новое значение,
которое нужно куда-то присвоить.

### 109. Enum-пара внутри именованного tuple

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status => status switch { DomainStatus.Cancelled => ApiStatus.Deleted });
builder.Map<DomainStatus, (ApiStatus Status, string Label)>()
    .Members(status => new()
    {
        Status = Map<ApiStatus>(status),
        Label = "status"
    });
```

Active даёт `(Status: ApiStatus.Active, Label: "status")`, Cancelled —
`(Status: ApiStatus.Deleted, Label: "status")`. Tuple остаётся обычной структурной
парой; scalar-правила находятся в отдельном вложенном enum mapping.

При Update Cancelled с destination `(Status: ApiStatus.Active, Label: "old")`
возвращается `(Status: ApiStatus.Deleted, Label: "status")`. Исходная переменная
с ValueTuple не меняется без присваивания возвращённого значения.

### 110. Nested Map, Create и Update внутри scalar Members

Для наглядности вложенная пара намеренно возвращает результат по операции:

```csharp
builder.Map<string, ApiStatus>()
    .Members((text, previous, result, context) =>
        context.Operation == MappingOperation.Create
            ? ApiStatus.Pending
            : ApiStatus.Archived);

builder.Map<DomainStatus, ApiStatus>()
    .Members(status => Map<ApiStatus>("wire"));
```

| Внешняя конфигурация / операция с source Active | Результат |
|---|---|
| Код выше, Create | Pending: nested Create, начального result нет |
| Код выше, Update с Disabled | Archived: nested Update с Disabled |
| Добавить ConstructUsing, возвращающий Disabled; внешний Create | Archived: фабрика уже дала destination для nested Update |
| Вместо Map написать `Create<ApiStatus>("wire")` | Pending при внешнем Create и Update |
| Вместо Map написать `Update<ApiStatus>("wire", ApiStatus.Disabled)` | Archived при обеих внешних операциях |

Nested helper получает явный source. Голое `Map()` в scalar Members не может
вывести его по имени принимающего свойства, поскольку такого свойства здесь нет.

### 111. Value и generic helpers сохраняют тип результата

```csharp
builder.Map<DomainStatus, ApiStatus?>()
    .Members(status => status switch
    {
        DomainStatus.Cancelled => Value<ApiStatus?>(null),
        DomainStatus.Suspended => Value<ApiStatus?>(ApiStatus.Disabled),
        _ => Auto<ApiStatus?>()
    });
```

Cancelled → null; Suspended → Disabled; Active → Active; Legacy → исключение.
Value задаёт явный результат и не запускает скрытую проверку объявленности.
Typed Auto решает target-typing задачу, сохраняя обычный контракт конвенции.

### 112. Enum-источник не лишает DTO структурного создания

```csharp
class StatusDto
{
    public StatusDto(int code) { Code = code; }
    public int Code { get; }
    public string Label { get; set; } = "";
}

builder.Map<DomainStatus, StatusDto>()
    .Construct(status => new((int)status))
    .Members(status => new() { Label = "status" });
```

Active даёт DTO с Code = 2 и Label = `"status"`. Это object mapping, поэтому
Construct применим. Для scalar enum → enum, enum ↔ string и enum ↔ integer
нет структурного конструктора: там используются Members, Using либо Convert.

### 113. Неприменимые настройки диагностируются

Каждая строка — отдельное добавление к корректной регистрации соответствующей пары.

| Пара | Явная pair-настройка | Ожидаемый результат конфигурации |
|---|---|---|
| `int → StoredCode` | `.EnumMappingStrategy(EnumMappingStrategy.ByName)` | MORPH0023 |
| `DomainStatus → int` | `.EnumMappingStrategy(EnumMappingStrategy.ByValue)` | MORPH0023 |
| `string → DomainStatus` | `.EnumMappingStrategy(EnumMappingStrategy.ByName)` | MORPH0023 |
| `DomainStatus → ApiStatus`, оба без Flags | `.FlagsMappingMode(FlagsMappingMode.ByBit)` | MORPH0023 |
| `int → Access` | `.FlagsMappingMode(FlagsMappingMode.ByMask)` | MORPH0023 |
| `Access → string` с ByValue | `.FlagsMappingMode(FlagsMappingMode.ByBit)` | MORPH0023 |
| `DomainStatus → ApiStatus` | `.ConstructorSelection(ConstructorSelection.Default)` | MORPH0023 даже при значении Default |

Общий mapper-level default на неприменимой паре игнорируется, а не превращает
любую регистрацию в ошибку. Некорректные значения enum-настроек всё равно
диагностируются по обычному контракту настроек. Using без Members не делает
применимую настройку запрещённой: она просто не преобразует готовый результат фабрики.

### 114. Ошибки типов и ограничения маркеров не скрываются

| Фрагмент | Ожидание |
|---|---|
| Ветка enum-result `=> ApiStatus.Unknown` | Корректный явно типизированный ноль |
| Ветка enum-result `=> (ApiStatus)0` или `=> default` | Поддерживается для non-nullable enum-результата |
| Ветка enum-result `=> 0` | Не поддерживается проверенной формой scalar marker; нужен typed zero |
| `var mapped = ... switch` со смесью enum и Auto | При нехватке target type нужен `Auto<ApiStatus>()`, как в сценарии 12 |
| Ветка non-nullable enum-result `=> null` | Ошибка типа/конфигурации, не скрытый default |
| Enum Members возвращает число другого типа без допустимого преобразования | Ошибка типа, не автоматический cast |
| Дублирующие cases aliases одного числа | Обычная диагностика C# недостижимой ветки |
| Неполный switch внутри выбранного результата или Using/Convert | Обычное предупреждение C# сохраняется |
| Обычная лямбда с именем метода Members в чужом API | Её диагностика не подавляется как Morphant DSL |

### 115. Неверный discard не становится подтверждением coverage

Вместо правильного верхнеуровневого `_ = CoverageTarget.Internal;` из сценария 100:

```csharp
.Members(value =>
{
    var _ = CoverageTarget.Active;
    _ = CoverageTarget.Internal;
    return value switch
    {
        CoverageSource.Cancelled or CoverageSource.New => CoverageTarget.Deleted
    };
})
```

Здесь `_` — настоящая переменная: присваивание не исключает Internal из coverage.
Ожидаемые runtime-результаты остаются Active/Deleted, а destination warning для
Internal сохраняется. Настоящий discard должен быть отдельным statement верхнего
уровня тела Members; подтверждение не прячется в условном или вложенном блоке.

### 116. Отложенные возможности не появляются из сходства имён

```csharp
enum ProtocolStatus { STATUS_PENDING_APPROVAL = 1 }
enum BusinessStatus { PendingApproval = 10 }

builder.Map<ProtocolStatus, BusinessStatus>()
    .Members(status => status switch
    {
        ProtocolStatus.STATUS_PENDING_APPROVAL => BusinessStatus.PendingApproval
    });
```

Вход STATUS_PENDING_APPROVAL даёт PendingApproval по явному правилу.
Без Members — отсутствие ByName-соответствия: регистронезависимое сравнение
не удаляет префикс/подчёркивания. `EnumMember`, `Description` и подобные атрибуты
также не подменяют CLR-имена конвенции. Автоматическое построение обратной пары, общий numeric parsing,
проекции, автоматическое отображение коллекций и настройка разделителя остаются
за текущей границей feature.

### 117. Result можно читать только на пути, где он существует

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members((status, previous, result, context) =>
    {
        if (context.Operation == MappingOperation.Update)
            return result;

        return ApiStatus.Pending;
    });
```

| Операция | Результат |
|---|---|
| Create Active | Pending; отсутствующий result не читается |
| Update Active, Archived | Archived |
| Update Active, Unknown = 0 | Unknown: ноль является доступным result |

Destination здесь non-nullable. При замене его на ApiStatus? одного условия
Operation == Update уже недостаточно: Update может получить null destination.
Без фабрики такой путь не даёт result и требует диагностики. Безусловное чтение
result на Create также недопустимо; для него сначала нужен Using.

### 118. Вычисляемый fallback выполняется только после неуспеха конвенции

`LogFallback` считает вызовы и возвращает ApiStatus.Unrecognized.

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status => status switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted,
        var remaining => LogFallback(remaining)
    });
```

| Вход | Результат | LogFallback |
|---|---|---|
| Cancelled | Deleted | Не вызывается |
| Active | Active по конвенции | Не вызывается |
| Legacy | Unrecognized | Один вызов с Legacy |
| `(DomainStatus)123` | Unrecognized | Один вызов с числом 123 |

`var remaining` без guard является завершающей веткой, как `_`. При Explicit
Active тоже даст Unrecognized с одним вызовом. Если же пользователь сам заранее
вычислит `var fallback = LogFallback(status);` перед switch, этот local сохранит
написанное место и вычислится при входе в блок, как в сценарии 47.

## Обычные типы и кортежи из enum и в enum

Здесь рассмотрены корневые пары: enum является целым source либо destination.
Маппинг enum-свойства между двумя DTO отдельно показан в сценарии 108.

| Направление | Сценарии |
|---|---|
| Enum → класс | 112 — конструктор DTO; 123 — flags в отдельные bool-свойства; 129 — nullable-обёртка |
| Класс → enum | 119 — выбор enum-свойства; 120 — вложенная enum-пара; 123 — сборка flags; 129 и 131 — null и lifecycle |
| Структура → enum / enum → структура | 121 / 122 |
| Enum → ValueTuple | 109, включая Create и Update |
| ValueTuple → enum | 124 — именованный; 125 — несколько объектов; 126 — ItemN; 130 и 132 — nullable |
| Enum → System.Tuple / System.Tuple → enum | 128 / 127 |

Для обычного объекта или кортежа на входе выражение пользователя определяет,
какие поля участвуют в результате. Примеры ниже используют существующие
[ResolveUsing](../api/resolve-using.md), [ConstructUsing](../api/construct-using.md)
и [Convert](../api/convert.md). Их тела — обычный C#: switch не дополняется
enum-конвенцией, а DSL Auto/Map внутри callback не используется.

Примеры с вложенным преобразованием дополнительно используют эти регистрации:

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status => status switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted
    });
builder.Map<DomainStatus, string>();
```

В Using/Convert вложенная пара вызывается через `context.Mapper`; обе пары
должны быть доступны mapper. Результат такого вызова подчиняется настройкам
вложенной пары. Готовое enum-значение, возвращённое самим callback, не получает
скрытой проверки имён или объявленности. Автоматический выбор свойства DTO,
элемента кортежа либо scalar Members для произвольного object/tuple source
этими примерами не вводится: отдельного такого контракта в дизайне пока нет.

### 119. Класс → enum: явно выбрать свойство

```csharp
class StatusCarrier
{
    public DomainStatus Status { get; set; }
    public string Comment { get; set; } = "";
}

builder.Map<StatusCarrier, DomainStatus>()
    .ResolveUsing((source, previous) => source.Status);
```

| Вход / операция | Результат |
|---|---|
| Create, Status = Active | Active |
| Create, Status = Cancelled, Comment = `"ignored"` | Cancelled |
| Update, Status = Active, previous = Suspended | Active |
| Create, Status = `(DomainStatus)123` | Неназванное 123: callback явно вернул готовое значение |

Поле Comment не участвует в вычислении. ResolveUsing повторно выбирает enum и
на Update; содержимое source не сопоставляется destination по названию Status.

### 120. Класс → другой enum через зарегистрированную пару

Используется StatusCarrier из сценария 119 и общая enum-пара выше.

```csharp
builder.Map<StatusCarrier, ApiStatus>()
    .ResolveUsing((source, previous, context) =>
        previous.TryGetValue(out var old)
            ? context.Mapper.Map<DomainStatus, ApiStatus>(source.Status, old)
            : context.Mapper.Map<DomainStatus, ApiStatus>(source.Status));
```

| Вход / операция | Вложенная операция | Результат |
|---|---|---|
| Create, Status = Active | Create | ApiStatus.Active |
| Create, Status = Cancelled | Create | ApiStatus.Deleted |
| Update, Status = Cancelled, previous = Archived | Update с Archived | Deleted: в общей enum-паре нет правила сохранения previous |
| Create или Update, Status = Legacy | Соответствующая операция | Исключение вложенного mapping |

Так передаётся previous во вложенный Update. Если вложенная пара позднее
получит правило сохранения Archived, оно начнёт действовать и здесь.
Один вызов `context.Mapper.Map<DomainStatus, ApiStatus>(source.Status)` без
destination всегда запросил бы вложенный Create, даже при внешнем Update.

### 121. Обычная структура → enum по нескольким признакам

```csharp
struct ApprovalFacts
{
    public bool IsPaid;
    public bool IsBlocked;
}

builder.Map<ApprovalFacts, ApiStatus>()
    .ResolveUsing((source, previous) => source switch
    {
        { IsBlocked: true } => ApiStatus.Disabled,
        { IsPaid: true } => ApiStatus.Active,
        _ => ApiStatus.Pending
    });
```

| IsPaid | IsBlocked | Результат Create и Update |
|---|---|---|
| false | false | Pending, в том числе для default(ApprovalFacts) |
| true | false | Active |
| false | true | Disabled |
| true | true | Disabled: первое правило имеет приоритет |

Это полное пользовательское решение по двум полям; конвенция enum-имён между
ветками обычного switch не добавляется.

### 122. Enum → обычная структура с вычисляемыми полями

```csharp
struct StatusFacts
{
    public bool IsActive;
    public bool IsCancelled;
}

builder.Map<DomainStatus, StatusFacts>()
    .Members(status => new()
    {
        IsActive = status == DomainStatus.Active,
        IsCancelled = status == DomainStatus.Cancelled
    });
```

| Source | Поля возвращённой структуры: IsActive / IsCancelled |
|---|---|
| Active | true / false |
| Cancelled | false / true |
| Pending | false / false |
| `(DomainStatus)123` | false / false по написанным C# сравнениям |

При Update оба поля получают новые значения. Как и для ValueTuple, вызывающий
код сохраняет результат: `facts = mapper.Map(status, facts)`.

### 123. Flags ↔ DTO с отдельными bool-свойствами

Используется Access из сценария 70: Read = 1, Write = 2, Audit = 4.

```csharp
class PermissionDto
{
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
}

builder.Map<Access, PermissionDto>()
    .Members(mask => new()
    {
        CanRead = (mask & Access.Read) != 0,
        CanWrite = (mask & Access.Write) != 0
    });

builder.Map<PermissionDto, Access>()
    .ResolveUsing((source, previous) =>
        (source.CanRead ? Access.Read : Access.None) |
        (source.CanWrite ? Access.Write : Access.None));
```

| Направление и вход | Результат |
|---|---|
| ReadWrite = 3 → DTO | CanRead = true, CanWrite = true |
| None = 0 → DTO | false / false |
| Read \| Audit = 5 → DTO | true / false: для Audit поля нет |
| DTO true / false → Access | Read = 1 |
| DTO true / true → Access | ReadWrite = 3 |
| Update Access, DTO false / false, previous = ReadWrite | None = 0 |

В обеих корневых парах пользовательские выражения работают с целым объектом или
маской. Здесь не запускается проход Members для каждого отдельного бита.
Roundtrip Read \| Audit → DTO → Access даёт только Read: потеря Audit следует
из явно выбранных полей DTO.

### 124. Именованный ValueTuple → enum с дополнительным условием

Используется общая DomainStatus → ApiStatus регистрация из начала раздела.

```csharp
builder.Map<(DomainStatus Status, bool IsArchived), ApiStatus>()
    .ResolveUsing((source, previous, context) => source.IsArchived
        ? ApiStatus.Archived
        : context.Mapper.Map<DomainStatus, ApiStatus>(source.Status));
```

| Входной tuple | Результат Create и Update |
|---|---|
| `(Status: Active, IsArchived: false)` | Active |
| `(Status: Cancelled, IsArchived: false)` | Deleted |
| `(Status: Legacy, IsArchived: false)` | Исключение вложенного mapping |
| `(Status: Legacy, IsArchived: true)` | Archived; вложенная пара не вызывается |

Status выбран явно. Из того, что tuple содержит enum-элемент, автоматический
выбор этого элемента для корневой enum-конвенции не следует.

### 125. Tuple из нескольких объектов → один enum

Order со свойством DomainStatus Status объявлен в сценарии 108.

```csharp
class AccountState
{
    public bool IsSuspended { get; set; }
}

builder.Map<(Order? Order, AccountState? Account), ApiStatus>()
    .ResolveUsing((source, previous, context) =>
    {
        if (source.Order is null || source.Account is null)
            throw new ArgumentException("Both inputs are required.");

        if (source.Account.IsSuspended)
            return ApiStatus.Disabled;

        return context.Mapper.Map<DomainStatus, ApiStatus>(source.Order.Status);
    });
```

| Order.Status / Account.IsSuspended | Результат |
|---|---|
| Active / false | Active |
| Cancelled / false | Deleted |
| Legacy / true | Disabled, до вложенного mapping |
| Order = null или Account = null | Пользовательский ArgumentException |

Сам ValueTuple не равен null. Null его элементов не является null корневого
source и обрабатывается написанными проверками. Та же схема позволяет передать
вместе с моделью пользовательскую политику выбора enum.

### 126. Безымянный ValueTuple → enum через ItemN

```csharp
builder.Map<(DomainStatus, bool), ApiStatus>()
    .ResolveUsing((source, previous, context) => source.Item2
        ? ApiStatus.Archived
        : context.Mapper.Map<DomainStatus, ApiStatus>(source.Item1));
```

| Вход | Результат |
|---|---|
| `(Active, false)` | Active |
| `(Cancelled, false)` | Deleted |
| `(Legacy, true)` | Archived |
| `(Legacy, false)` | Исключение вложенного mapping |

Это явные обращения к Item1/Item2, а не позиционная конвенция. Для tuple
`(bool, DomainStatus)` выражения нужно поменять: source.Item1 станет условием,
а source.Item2 — входом вложенной enum-пары.

### 127. System.Tuple → enum и null ссылочного tuple

```csharp
builder.Map<Tuple<DomainStatus, bool>?, ApiStatus?>()
    .ResolveUsing((source, previous, context) => source.Item2
        ? ApiStatus.Archived
        : context.Mapper.Map<DomainStatus, ApiStatus>(source.Item1));
```

| Вход / операция | Результат |
|---|---|
| Create, Tuple.Create(Active, false) | Active |
| Create, Tuple.Create(Cancelled, false) | Deleted |
| Update, Tuple.Create(Legacy, true), previous = Pending | Archived |
| Create null | null; ResolveUsing не вызывается |
| Update null, previous = Archived | null по default ReturnNull |

После null policy callback получает non-null System.Tuple. Его read-only
элементы можно читать; создавать изменённый tuple для этого не требуется.

### 128. Enum → System.Tuple: создание, сохранение и явная замена

Нужны обе вложенные пары из начала раздела: DomainStatus → ApiStatus и
DomainStatus → string.

```csharp
builder.Map<DomainStatus, Tuple<ApiStatus, string>>()
    .Construct(status => new(
        Create<ApiStatus>(status),
        Create<string>(status)));
```

| Операция | Результат |
|---|---|
| Create Active | Новый Tuple с Item1 = Active, Item2 = `"Active"` |
| Create Cancelled | Новый Tuple с Item1 = Deleted, Item2 = `"Cancelled"` |
| Update Cancelled, previous = Tuple.Create(ApiStatus.Active, `"old"`) | Тот же previous: Item1 = Active, Item2 = `"old"` |
| Update Cancelled, destination = null, policy Create | Новый Tuple: Deleted / `"Cancelled"`; операция остаётся Update |

System.Tuple read-only; обычный Update не пересоздаёт его ради scalar-элементов.
Если нужен новый tuple и на Update, вместо Construct явно выбрать Resolve:

```csharp
builder.Map<DomainStatus, Tuple<ApiStatus, string>>()
    .Resolve((status, previous) => new(
        Create<ApiStatus>(status),
        Create<string>(status)));
```

Теперь Update Cancelled с тем же previous возвращает **другой** Tuple:
Deleted / `"Cancelled"`. Явные Create для элементов запрашивают вложенный Create
в обеих внешних операциях. Отличие от mutable ValueTuple видно в сценарии 109.

### 129. Nullable enum ↔ nullable объект-обёртка

```csharp
class NullableStatusCarrier
{
    public DomainStatus? Status { get; set; }
}

builder.Map<NullableStatusCarrier?, DomainStatus?>()
    .ResolveUsing((source, previous) => source.Status);

builder.Map<DomainStatus?, NullableStatusCarrier?>()
    .Members(status => new() { Status = status });
```

| Направление и вход | Результат |
|---|---|
| null объект → enum | null по source policy, callback пропущен |
| Объект с Status = null → enum | null из callback |
| Объект с Status = Active → enum | Active |
| null enum → объект | null, не объект с пустым Status |
| Unknown = 0 → объект | Non-null объект со Status = Unknown |
| Active → объект | Объект со Status = Active |

Если для первой пары выбрать NullSourceHandling.ReturnDestination, null объект
на Update сохранит previous. Объект со Status = null всё равно вернёт null:
это результат callback, который завершает операцию без повторной null policy.

### 130. Nullable ValueTuple → enum с сохранением previous

```csharp
builder.Map<(DomainStatus Status, bool PreservePrevious)?, ApiStatus?>()
    .ResolveUsing((source, previous, context) =>
        source.PreservePrevious && previous.TryGetValue(out var old)
            ? old
            : context.Mapper.Map<DomainStatus, ApiStatus>(source.Status));
```

| Операция / вход | Результат |
|---|---|
| Create null | null; callback пропущен |
| Create `(Active, true)` | Active: previous отсутствует |
| Update `(Legacy, true)`, previous = Archived | Archived; вложенная пара не вызывается |
| Update `(Legacy, true)`, destination = null | Исключение вложенного mapping: сохранять нечего |
| Create non-null tuple `(Unknown, false)` | Non-null ApiStatus.Unknown = 0 |

Callback получает распакованный ValueTuple после null policy. Отсутствующий
tuple и tuple с нулевым enum-элементом различаются.

### 131. ConstructUsing при объект → enum не заменяет существующий enum

```csharp
builder.Map<StatusCarrier, DomainStatus>()
    .ConstructUsing(source => source.Status);
```

| Операция | Результат |
|---|---|
| Create, source.Status = Active | Active |
| Update, source.Status = Active, previous = Suspended | Suspended: фабрика пропущена |
| Update, source.Status = Active, previous = Unknown = 0 | Unknown: ноль — существующий destination |

Для выбора нового enum и на Update подходит ResolveUsing из сценария 119.
В варианте с nullable destination `DomainStatus?` и policy Create Update с null
destination вызвал бы ConstructUsing и вернул Active.

### 132. Convert задаёт собственную обработку nullable tuple

```csharp
builder.Map<(bool IsReady, bool IsBlocked)?, DomainStatus?>()
    .Convert(source => source is null
        ? DomainStatus.Unknown
        : source.Value.IsBlocked
            ? DomainStatus.Suspended
            : source.Value.IsReady
                ? DomainStatus.Active
                : DomainStatus.Pending);
```

| Вход | Результат Create и Update |
|---|---|
| null | Non-null Unknown = 0, выбранный самим callback |
| `(false, false)` | Pending |
| `(true, false)` | Active |
| `(false, true)`, `(true, true)` | Suspended |

Convert получает исходный nullable tuple и владеет алгоритмом целиком, включая
null source. Использование source.Value защищено написанной проверкой; null
не завершается до callback, как было бы при стандартной policy у ResolveUsing.

## Статус проверки каталога

Примеры сверены с согласованным дизайном; нумерация нужна для обсуждения конкретных
случаев и последующего переноса в тесты. Они не заменяют проверки production
генератора, реальной типизации Members, generated code и Rider. Новые ID/severity
enum-диагностик и точные имена enum-specific exceptions каталог не назначает.
