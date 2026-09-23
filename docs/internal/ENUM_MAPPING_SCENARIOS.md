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
