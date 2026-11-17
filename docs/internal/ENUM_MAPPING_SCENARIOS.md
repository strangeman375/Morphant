# Enum mapping: пользовательские сценарии

Обновлено 2026-11-17. Каталог для проверки **будущего поведения** до реализации feature.
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
- «Исключение маппинга» при отсутствии enum-результата означает будущий
  `Morphant.Exceptions.EnumMappingException`; то же относится к краткому
  «Исключение» в таблицах enum-конвенций. Явные пользовательские исключения,
  ошибки конфигурации и lifecycle названы отдельно. Контракт — в
  [дизайне](ENUM_MAPPING_DESIGN.md#ошибки-выполнения).
- «Диагностика» относится к конфигурации/компиляции, а не к возвращаемому значению.
  Enum coverage по умолчанию — `UnmappedEnumMemberValidation` со значением
  `UnmappedMemberValidation.Source`: необработанные объявленные source-значения
  дают warnings. Таблицы runtime-результатов не являются полным перечнем диагностик;
  отключить только coverage можно через `.UnmappedEnumMemberValidation(UnmappedMemberValidation.None)`.
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
- [Выбор входа конвенции](#выбор-входа-конвенции) — 133–135
- [IncludeBase и разные входы switch](#includebase-и-разные-входы-switch) — 136–142
- [Стратегия и тип входа конвенции](#стратегия-и-тип-входа-конвенции) — 143–149
- [Составной вход ByBit и вычисления](#составной-вход-bybit-и-вычисления) — 150–158
- [Ошибки enum-маппинга](#ошибки-enum-маппинга) — 159–163
- [Ignore в ByBit](#ignore-в-bybit) — 164–166
- [Независимая настройка enum coverage](#независимая-настройка-enum-coverage) — 167–169
- [Members для object/tuple → enum](#members-для-objecttuple--enum) — 170–175
- [Coverage выбранного enum-входа](#coverage-выбранного-enum-входа) — 176–178
- [Flags-конвенция для выбранного входа](#flags-конвенция-для-выбранного-входа) — 179–184
- [Null выбранного входа конвенции](#null-выбранного-входа-конвенции) — 185–189
- [Совместимые настройки и aliases](#совместимые-настройки-и-aliases) — 190–192

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

При default MemberSelection.Auto конвенция работает без вызова Members.

| Вход | Результат |
|---|---|
| `Pending = 1` | `ApiStatus.Pending = 10` |
| `Active = 2` | `ApiStatus.Active = 20` |
| `Unknown = 0` | `ApiStatus.Unknown = 0` |
| `Cancelled` | Исключение маппинга: имени нет |
| `(DomainStatus)20` | Исключение маппинга: совпадение с числом destination не заменяет имя |

Default Source предупреждает о Cancelled, Suspended, Corrupt и Legacy. Неназванное 20 и
дополнительные destination-значения в эту проверку не входят.

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

Неполный mapping switch не требует `_ => Auto()` ради устранения compiler/IDE warning.

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

В обычном C# Active тоже дал бы Unrecognized; здесь действует DSL-конвенция перед fallback.

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

Без явного правила ready: Warning и неуспех конвенции; fallback обрабатывает вход, но warning
сохраняет. Exact Ready/READY остаются 10/20. При обоих destination-числах 10 конфликта нет;
при единственном имени Ready все три source-имени переводятся в него. Culture, включая tr-TR,
не влияет.

### 09. Aliases одного source-числа

```csharp
enum AliasSource { Ready = 1, Active = 1 }
enum AliasTarget { Unknown = 0, Ready = 10 }

builder.Map<AliasSource, AliasTarget>();
```

Во всех вариантах AliasTarget содержит Unknown = 0; таблица меняет только
объявления, соответствующие source aliases. Вход Ready или Active — одно число 1.

| Имена destination для aliases | Диагностика неоднозначности | Результат |
|---|---|---|
| Только `Ready = 10` | Нет | 10: одного найденного имени достаточно |
| `Ready = 10, Active = 10` | Нет | 10: найденные соответствия согласованы |
| `Ready = 10, Active = 20` | Warning | EnumMappingException: нет однозначного соответствия и fallback |

Для последнего варианта каждая строка ниже задаёт отдельную конфигурацию
Members; вход — Ready или Active:

| Конфигурация | Диагностика неоднозначности | Результат |
|---|---|---|
| Switch с `_ => AliasTarget.Unknown` | Warning | Unknown = 0 через обычный fallback |
| Switch с `AliasSource.Ready => AliasTarget.Ready` | Нет | 10 для **обоих** aliases: явное правило обрабатывает число 1 |
| Switch с `AliasSource.Ready => Auto(), _ => AliasTarget.Unknown` | Warning | EnumMappingException немедленно; fallback не вызывается |
| Explicit и switch с `_ => AliasTarget.Unknown` | Нет | Unknown = 0; автоматического пути нет |
| Explicit и switch с `AliasSource.Ready => Auto(), _ => AliasTarget.Unknown` | Warning | EnumMappingException; явный Auto сохраняет автоматический путь |
| Прямое `Members(value => AliasTarget.Unknown)` | Нет | Unknown = 0; результат полностью задан пользователем |

Отдельные cases Ready/Active дают обычную C# ошибку недостижимой ветки. Warning
неоднозначности сохраняется и при enum coverage None.

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

Active → Pending; identity-оптимизация не обходит правило.

## Выражения и управление выполнением

### 11. Прямое выражение задаёт готовый результат

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status => Compute(status));
```

Если Compute всегда возвращает Disabled, то Active, Legacy и неназванное 123 дают Disabled.
Compute вызывается один раз, его исключение сохраняется.

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

Неизменяемый local сохраняет mapping switch. Для смеси enum/Auto под var может потребоваться
target type через Auto<ApiStatus>().

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

Cancelled → Deleted; Active → Active; Legacy → EnumMappingException; Corrupt →
InvalidOperationException. Удаление default оставит недопустимый C# путь без return.

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

### 16. Switch над вычисленным значением передаёт его конвенции

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
| `Active` | `ApiStatus.Pending` | Один вызов |
| `Legacy` | `ApiStatus.Pending` | Один вызов |

Если Normalize вернёт Cancelled, явная ветка даст Deleted для любого исходного
входа. Если Normalize бросит, исключение не становится fallback. При результате
Normalize = Legacy явный Auto бросает: исходный Active не используется повторно.

Вынос входа в local сохраняет применение конвенции. Следующий вариант показывает
**неявную конвенцию с fallback Unknown, без явного Auto()**:

```csharp
.Members(status =>
{
    var normalized = Normalize(status);
    return normalized switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted,
        _ => ApiStatus.Unknown
    };
})
```

| Normalize вернул | MemberSelection.Auto (default) | MemberSelection.Explicit |
|---|---|---|
| Pending | ApiStatus.Pending по неявной конвенции | ApiStatus.Unknown через fallback |
| Cancelled | ApiStatus.Deleted по явному правилу | ApiStatus.Deleted по явному правилу |
| Legacy | ApiStatus.Unknown через fallback | ApiStatus.Unknown через fallback |

Normalize вызывается один раз. С `_ => Auto()` Pending переводится и при Explicit, Legacy
бросает; local не меняет конвенцию.

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

Исходный `Active` даёт `ApiStatus.Pending`: switch захватывает уже присвоенный
Pending, и Auto использует это же значение. Если после присваивания вместо
switch написать `return Auto();`, результат также Pending: вне mapping switch
Auto получает текущее source в точке вызова.

Изменение source **внутри guard** не меняет уже вычисленный вход switch:

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status => status switch
    {
        DomainStatus.Active when (status = DomainStatus.Pending) == DomainStatus.Active
            => ApiStatus.Disabled,
        _ => Auto()
    });
```

Для исходного Active guard присваивает status = Pending и возвращает false.
Auto всё равно получает захваченное Active и возвращает ApiStatus.Active.
Обычное выражение с переменной status после guard видело бы уже Pending.

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

Cancelled: код 1 → Deleted, код 2 → Archived. Active → Active без GetCode. Внутренний switch —
C#: удаление `_` сохраняет warning о неполноте.

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

Обратная регистрация независима и не строится автоматически.

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

Trim можно задать самим входом mapping switch:

```csharp
builder.Map<string, DomainStatus>()
    .Members(text => text.Trim() switch { _ => DomainStatus.Unknown });
```

| Вход | Результат после явного Trim |
|---|---|
| `" Active "`, `" active "` | Active |
| `" Pending "` | Pending |
| `"   "`, `" 2 "`, `" Missing "` | Unknown |

Trim вызывается один раз. С `_ => Auto()` три последних входа бросают; без явно написанного
Trim пробелы сохраняются.

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
| `"ready"` с явной веткой `"ready" => Auto()` и fallback Unknown | EnumMappingException немедленно, без fallback |

Конфликт даёт Warning независимо от fallback. Правило для "ready" не покрывает другие
конфликтные варианты регистра. Если Ready = READY = 1, все варианты дают 1 без warning. ID
диагностики пока не назначен.

### 21. Canonical output при aliases

```csharp
enum AliasedStatus { Ready = 1, Active = 1 }

builder.Map<AliasedStatus, string>()
    .Members(status => status switch { AliasedStatus.Ready => "ready" });
```

Ready/Active → "ready" без warning. Без правила: Warning и EnumMappingException для 1; с `_ =>
"unknown"` — "unknown", warning остаётся; с `Ready => Auto()` — исключение даже при fallback.
Прямой Members(status => "ready") не предупреждается. То же относится к aliases, различающимся
только регистром.

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

AllowUndefined даёт те же строки; culture не меняет знак/группировку. Обратного numeric
parsing нет.

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

Префиксы, snake_case и wire attributes автоматически не обрабатываются; свой полный формат
задаётся прямым Members/Convert.

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

Здесь исключение означает EnumMappingException, не обязательный OverflowException.

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

Неназванный source 2 не закрывает Destination coverage Archived — см. 102.

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

ByValue и ByValueAllowUndefined допустимы и дают эти же результаты; ByName для enum → integer
ошибочна.

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

300 → 44 — эффект явного unchecked, без повторной проверки объявленности.

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

42 → неназванное 42 даже при строгом ByValue; 43 → Unknown. Явный результат не
перевалидируется.

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

Нужна точная nullable value-type пара; underlying регистрация её не заменяет.

### 34. Все политики null source

Для `builder.Map<DomainStatus?, ApiStatus?>()` меняется только
`.NullSourceHandling(...)`.

| Политика | Create null | Update null, previous = Archived |
|---|---|---|
| ReturnNull | null | null |
| ReturnDestination | null | Archived |
| Throw | NullSourceException | NullSourceException |

Для DomainStatus? → ApiStatus ReturnNull возвращает default = 0 даже без объявления нуля в
destination.

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

Create Active → Pending; Update Active с non-null destination → Archived. Неиспользуемый
result допустим без фабрики. Полный context.Mapper доступен в Using/Convert.

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

Nullable Update Active с null destination и policy Create: один вызов фабрики, Pending,
Operation.Update.

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

ResolveUsing вызывается один раз; старый destination доступен через previous, не result.

### 40. Switch над result корректирует результат фабрики

`ChooseInitial` возвращает значение из столбца «Начальный result» и вызывается
один раз на Create и Update.

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .ResolveUsing((status, previous) => ChooseInitial(status))
    .Members((status, previous, result) => result switch
    {
        ApiStatus.Disabled => ApiStatus.Archived,
        _ => result
    });
```

| Исходный вход | Начальный result | Результат |
|---|---|---|
| Active | Pending | Pending |
| Legacy | Pending | Pending |
| Active, Legacy | Disabled | Archived |
| Legacy | Active | Active |
| Active, Legacy | `(ApiStatus)123` | 123 через fallback |

Explicit для такой корректировки не нужен. Вход конвенции — result, исходный
status его не заменяет. При `_ => Auto()` объявленные результаты остаются теми
же, а неназванное 123 вызывает исключение: строгая E → E конвенция не является
безусловным identity. При Explicit явный Auto сохраняет этот контракт.

При `_ => ApiStatus.Unknown` известный Pending всё ещё даёт Pending по конвенции;
123 даёт Unknown через fallback. Для безусловного Unknown подходит прямое
`Members(status => ApiStatus.Unknown)` либо MemberSelection.Explicit.

Выбор `status switch` намеренно возвращает конвенцию к source:

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .ResolveUsing((status, previous) => ApiStatus.Pending)
    .Members((status, previous, result) => status switch
    {
        DomainStatus.Cancelled when result == ApiStatus.Pending
            => ApiStatus.Deleted,
        _ => result
    });
```

| Вход | Auto (default) | Explicit |
|---|---|---|
| Active | Active по конвенции source → destination | Pending |
| Cancelled | Deleted | Deleted |
| Legacy | Pending через fallback | Pending |

Вход конвенции задаёт switch, независимо от фабрики. Tuple-вариант — 133.

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

Без фабрики включённый Create диагностирует недоступный result для Ignore. При Update-only с
non-nullable destination result доступен.

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

Добавление Members(status => ApiStatus.Active): Active → Active, Corrupt → terminal null без
Members. Исключение фабрики сохраняется; явный nullable null-result не повторяет policies.

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

Nullable Update-only с null destination и policy Create допустим без включения Create: нужен
путь без чтения отсутствующего result либо фабрика.

### 44. Convert владеет всем алгоритмом

```csharp
builder.Map<DomainStatus?, ApiStatus?>()
    .Convert(status => status is null ? ApiStatus.Unknown : ApiStatus.Disabled);
```

| Вход | Результат |
|---|---|
| null | Unknown, не default null policy |
| Active, Cancelled, Legacy | Disabled, без enum-конвенции |

Локальный Convert несовместим с Members/Using; внутри обычный C# без
DSL-конвенции/Auto/Ignore.

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

Без локального fallback Legacy → Unknown. Suspended остаётся базовым правилом; включается та
же пара, не наследование enum-типов.

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

Замена guard на Cancelled => Auto(): Cancelled бросает до базового Cancelled → Deleted.

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

Имена MemberSelection в таблице сокращены. Последняя запись побеждает; Default продолжает
наследование; расположение mapper-level вызова относительно Map не влияет.

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

Результаты: 1 → Pending, 42 → исключение. Общий ByName неприменим, остаётся строгий ByValue;
нижний MSBuild AllowUndefined не ищется.

### 50. Несколько уровней используют одну неявную конвенцию и один result

Для цепочки `Base → Middle → Application` каждый уровень вызывает
`base.Configure(builder)` и включает точную пару через IncludeBase.

| Уровень | Members |
|---|---|
| Base | `Cancelled => Deleted`, `Suspended => Disabled`, `_ => Unknown` |
| Middle | `Cancelled => Archived` |
| Application | `Corrupt => Unrecognized` |

Это те же формы switch над неизменённым status, что в примере 45, с
квалифицированными именами enum. Разные входы уровней рассмотрены в сценариях 136–142.

| Вход | Результат |
|---|---|
| Cancelled | Archived: ближайший специальный case |
| Suspended | Disabled |
| Corrupt | Unrecognized |
| Active | Active: одна неявная конвенция после всех специальных правил |
| Legacy | Unknown: ближайший имеющийся fallback |

Inherited ResolveUsing выбирает один общий initial result; результат ветки не передаётся
следующему уровню.

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

Ноль: TargetAccess.None, (TargetAccess)0 или default; bare => 0 не поддерживается типизацией
marker.

### 53. Неизвестный бит проходит обычные правила

Используется конфигурация 51.

| Вход | С fallback Unknown | Если удалить fallback |
|---|---|---|
| `(SourceAccess)16` | Unknown = 128 | Исключение |
| Read \| (SourceAccess)16 | View \| Unknown = 144 | Исключение всей операции |
| (SourceAccess)16 \| (SourceAccess)32 | Unknown = 128 | Исключение |

С `(SourceAccess)16 => TargetAccess.Edit` вход Read | 16 даёт View | Edit = 48. Само
совпадение числа с View не создаёт ByName-соответствия.

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

В ByMask Read → Unknown: ReadWrite-правило не применяется к компонентам.

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

Вариант с особым результатом целой маски:

```csharp
builder.Map<NamedSource, NamedTarget>()
    .FlagsMappingMode(FlagsMappingMode.ByMask)
    .Members(mask => mask switch
    {
        NamedSource.ReadWrite => NamedTarget.Audit,
        _ => Auto()
    });
```

| Вход | ByMask, как выше | Если выбрать ByBit |
|---|---|---|
| ReadWrite = 3 | Audit = 32 по явному правилу | Read \| Write = 24; warning о недостижимом составном case |
| Read \| Audit = 5 | Исключение: имени целой маски нет | Read \| Audit = 40 |

Whole-mask override с побитовым переводом остальных комбинаций требует явного алгоритма:
[граница режимов](ENUM_MAPPING_DESIGN.md#bybit-и-bymask).

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

Явные ByBit-правила `(PairSource)1 => (PairTarget)4` и `(PairSource)2 => (PairTarget)8` дают
12; объявление Pair их не создаёт.

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

Без явного нулевого правила конвенция даёт 0 в обоих режимах, даже без имени нуля, при любой
применимой стратегии.

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

Alias View = Read не добавляет вызов. При исключении на Delete запись Read сохраняется, Audit
не вызывается, частичный результат не возвращается.

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

Для пропуска вклада Write с продолжением обработки подходят нулевая маска или Ignore().

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
| Read \| Delete | View = 16: Ignore пропускает вклад Delete |
| Write \| Delete | Audit = 64 |
| Delete | None = 0: начальная маска не восстанавливается |

Фабрика один раз получает полную маску. Все биты видят result = Audit, previous — полную
исходную destination-маску; только явный => result добавляет её вклад.

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

Read | Write: оба прохода видят result = 48, возвращают Audit, итог 64. Case достижим, как и
возможный composite из Normalize(flag). Если фабрика вернёт View, оба Auto получают View, итог
16; исходные Read/Write входом Auto не становятся.

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

Строгий режим не извлекает часть Pair. Integer-вход целый, FlagsMappingMode неприменим.

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

Flags только у destination: целое значение. ByName не разбирает ReadWrite на Read/Write; без
Flags у Bits строгий ByValue для 3 бросает.

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

AllowUndefined даёт 3 в обоих режимах с разным числом проходов. Явные ByBit-вклады Read →
(DestinationPair)1, Write → (DestinationPair)2 также дают 3 без повторной validation.

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

Маска не сужается заранее перед обходом битов.

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

ByName для SignedBits → UnsignedBits даёт High = 128 по имени. Обход ограничен 8 физическими
битами sbyte без расширения. Ulong-бит 1UL << 63 положителен (9223372036854775808) и
численно не помещается в long.

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

Смешанного прохода «маска, затем её биты» нет.

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

В таблице C# literals: \t/\r/\n — реальные whitespace; \| — Markdown-экранирование обычного |.

### 71. Пустая строка, нулевой текст и пустые элементы списка

Та же регистрация без Members.

| Вход | Токены ByBit после trim | Результат |
|---|---|---|
| `""`, `"   "` | Один пустой токен | 0 |
| `"0"`, `" 0 "` | `"0"` | 0 |
| `"None"` | `"None"` | 0 по объявленному имени |
| `",;\|"` | Четыре пустых токена | 0 |
| `",Read"` | `""`, `"Read"` | Read = 1 |
| `"Read,"` | `"Read"`, `""` | Read = 1 |
| `"Read,,Write;"` | `"Read"`, `""`, `"Write"`, `""` | 3 |
| `"Read,  ,Write"` | `"Read"`, `""`, `"Write"` | 3 |
| `"Read,0,None"` | Три токена | Read = 1 |

Без None токены ""/"0" всё равно дают 0, имя "None" перестаёт распознаваться. ByMask сохраняет
эти нулевые вклады с одним проходом Members. Пустые токены доходят до правил в любой позиции —
см. 74–75.

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

"VIEW;Write" → 3; " view " → 1; "missing" → исключение. False guard оставляет конвенционное
Write → Write.

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
| `""`, `"  "`, `",Read"`, `"Read,,Write"`, `"Read,"` | FormatException |
| `"Read,Write"` | 3 |
| `"0"` | 0: нулевой текст не является пустым токеном |

После Read ошибка не возвращает частичную маску. `"" or "0" => throw ...` запретит и нулевой
текст. Чтобы разрешить пустую строку, но запретить пустые элементы списка, проверяется целая
строка через ByMask/Convert.

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

Явные правила предшествуют и конвенции пустоты/нуля.

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

С `_ => Auto()` CLR-имена, "" и "0" снова распознаются; неизвестный токен бросает.

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

В ByBit whole-string pattern с запятой не совпадает с отдельным токеном.

### 78. Пользовательский alias не становится частью ByMask-parser

Конфигурация 72 с `.FlagsMappingMode(FlagsMappingMode.ByMask)`.

| Вход | ByBit | ByMask |
|---|---|---|
| `"view"` | Read = 1 | Read = 1, совпала целая строка |
| `"view,Write"` | Read \| Write = 3 | Unknown = 8 |
| `"Read,Missing"` | Read \| Unknown = 9 | Unknown = 8, неуспех всей конвенции |
| `"Read,Write"` | 3 | 3 |
| `" view "` | Read = 1 | Unknown = 8: pattern всей строки не совпал, CLR-имени view нет |

Без fallback ByMask бросает. Явное "view,Write" => Access.ReadWrite задаёт whole-string
исключение.

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

Единица — токен, включая composite name; повторного flags → flags mapping нет.

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

ByMask: один Record с исходной строкой, ошибка всей попытки.

### 81. Using видит полную строку, Ignore пропускает токен

`InitialMask` получает вход, считает вызовы и возвращает Access.Audit.

```csharp
builder.Map<string, Access>()
    .ResolveUsing((text, previous) => InitialMask(text))
    .Members((part, previous, result) => part switch
    {
        "skip" => Ignore(),
        "keep" => result,
        _ => Auto()
    });
```

| Вход | Результат | InitialMask |
|---|---|---|
| `"Read"` | Read = 1 | Один вызов с `"Read"`; Audit не добавляется |
| `"Read;skip"` | Read = 1 | Один вызов с `"Read;skip"` |
| `"skip;skip"` | None = 0 | Один вызов; оба токена пропущены |
| `"Read;keep"` | Read \| Audit = 5 | Один вызов с `"Read;keep"` |
| `"keep;keep"` | Audit = 4 | Один вызов; оба явных result дают ту же маску |

Skip даёт 0, keep явно добавляет initial result. Null фабрики nullable destination завершает
mapping до токенизации.

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

Локальный `_ => Auto()` сохраняет alias view, но заменяет базовый fallback: "view;Missing"
бросает.

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

"Full;control" → 3 в ByMask; ByBit разделяет её на два токена. Quote/escape-протокола нет.

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

После неуспеха целого имени ByMask не пробует биты.

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

Без явной ветки: Warning, ноль → EnumMappingException. С `_ => "unknown"`: ноль → "unknown",
warning сохраняется. None => "none" разрешает оба aliases. Пустая входная строка означает 0,
не обязательный пустой вывод.

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

Пустой итог остаётся "", без None/"0".

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

Без fallback неизвестный вход бросает. Явный (Access)16 => Auto() бросает для 16 даже при
fallback "unknown".

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

ReadWrite → "access, access"; ReadWrite | Audit → "access, access, audit". Порядок — биты 1,
2, 4 независимо от объявлений/порядка операндов; повторы сохраняются.

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

Для пропуска Write с продолжением нужны "" или Ignore(), не null.

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

AllowUndefined даёт то же; без Members ReadWrite → "3". FlagsMappingMode неприменим.

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
| ReadWrite | `"read"`, без разделителя для Write |
| Write | `""`, начальная строка не восстанавливается |
| Write \| Audit | `"cached"`, только явный вклад Audit |

Фабрика один раз получает полную маску; previous — исходная строка Update. Без Members любой
вход даёт "cached".

Варианты: Audit => Ignore<string>() даёт Write | Audit → ""; None => Ignore() даёт 0 → "".
Если Write возвращает result, Write | Audit → "cached, cached".

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

Auto для нуля NoZeroName вернёт "0".

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

Без имени нуля последние входы дают "0". Access → string → Access сохраняет эти значения при
совместимых правилах; исходное текстовое оформление не сохраняется.

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

ReadWrite → "view, edit" → ReadWrite; без обратных aliases второй mapping бросает. При Read =>
"": ReadWrite → "edit" → Write. Числовой вывод "3" не включает обратный numeric parser.

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
    .UnmappedEnumMemberValidation(UnmappedMemberValidation.Source)
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

Без настройки действует тот же Source. None убирает warning, но New бросает; severity можно
повысить до error.

### 98. Явный запрет закрывает source coverage, Auto — не обязательно

В конфигурации 97 добавить одну из веток:

| Добавление | Вход New | Source coverage New |
|---|---|---|
| `CoverageSource.New => throw new InvalidOperationException()` | Пользовательское исключение | Закрыт: намеренный запрет |
| `CoverageSource.New => CoverageTarget.Deleted` | Deleted | Закрыт |
| `CoverageSource.New => Auto()` | Исключение маппинга | Не закрыт: конвенционного соответствия нет |
| `_ => CoverageTarget.Internal` | Internal | Закрыт для всех оставшихся source-значений |
| `_ => throw new InvalidOperationException()` | Пользовательское исключение | Закрыт для всех оставшихся source-значений |

После добавления `Suspended = 4` в CoverageSource, без изменений mapping и
CoverageTarget, действуют те же правила:

| Завершающая ветка | Runtime для Suspended | Source coverage Suspended |
|---|---|---|
| `_ => CoverageTarget.Internal` | Internal | Покрыт; warning нет |
| `_ => throw new InvalidOperationException()` | Пользовательское исключение | Покрыт; warning нет |
| `_ => Auto()` | Исключение маппинга | Warning: конвенционного соответствия нет |
| Отсутствует | Исключение маппинга | Warning: значение не обработано |

Общий fallback закрывает новые Source-значения и source-часть Strict без отдельных cases;
Destination проверяется отдельно. [Контракт
coverage](ENUM_MAPPING_DESIGN.md#проверка-покрытия).

### 99. Destination coverage и many-to-one

```csharp
builder.Map<CoverageSource, CoverageTarget>()
    .UnmappedEnumMemberValidation(UnmappedMemberValidation.Destination)
    .Members(value => value switch
    {
        CoverageSource.Cancelled or CoverageSource.New => CoverageTarget.Deleted
    });
```

Active → Active; Cancelled/New → Deleted. Active/Deleted покрыты, Internal даёт warning;
many-to-one допустим.

### 100. Discard исключает destination из coverage, сохраняя алгоритм

```csharp
builder.Map<CoverageSource, CoverageTarget>()
    .UnmappedEnumMemberValidation(UnmappedMemberValidation.Strict)
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

Coverage закрыт; discard исключает Internal без runtime-правила. Новое несопоставленное
destination-значение снова даст warning.

### 101. При E → E discard подтверждает только destination

```csharp
enum ReviewStatus { Active = 1, Hidden = 2 }

builder.Map<ReviewStatus, ReviewStatus>(MappingMode.Update)
    .MemberSelection(MemberSelection.Explicit)
    .UnmappedEnumMemberValidation(UnmappedMemberValidation.Strict)
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

При previous = Hidden Ignore тоже вернёт Hidden; это обработка Source, не возврат нуля.

### 102. Неназванный runtime-source не закрывает destination coverage

```csharp
enum DeclaredSource { Active = 1 }
enum DeclaredTarget { Active = 1, Archived = 2 }

builder.Map<DeclaredSource, DeclaredTarget>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValue)
    .UnmappedEnumMemberValidation(UnmappedMemberValidation.Destination);
```

Active → Active; (DeclaredSource)2 → Archived с Destination warning: объявленного соответствия
нет. Явное (DeclaredSource)2 => DeclaredTarget.Archived закрывает его.

### 103. Coverage flags проверяет участие битов, а ноль — отдельно

```csharp
[Flags] enum CoverageFlags { None = 0, Read = 1, Write = 2, ReadWrite = 3 }
[Flags] enum CoverageFlagsDto
{
    None = 0, View = 16, Edit = 32, ViewEdit = 48, Audit = 64
}

builder.Map<CoverageFlags, CoverageFlagsDto>()
    .MemberSelection(MemberSelection.Explicit)
    .UnmappedEnumMemberValidation(UnmappedMemberValidation.Strict)
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

Warnings нет, отдельная достижимость Edit не требуется. Удаление None-ветки оставит непокрытый
ноль с обеих сторон; Audit остаётся исключённым. ByMask дополнительно требует Source ReadWrite
и Destination Edit = 32: отдельные Read/Write не покрывают целую маску, а целое 32 не
возвращается.

### 104. Неизвестный guard не доказывает полное покрытие

```csharp
builder.Map<CoverageSource, CoverageTarget>()
    .UnmappedEnumMemberValidation(UnmappedMemberValidation.Source)
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

Warning для New: false-путь произвольного guard не обработан.

### 105. Динамический результат отличается от доказанно непокрытого значения

`ComputeTarget` в пользовательском коде для Active возвращает Active, для других
значений — Deleted.

```csharp
builder.Map<CoverageSource, CoverageTarget>()
    .UnmappedEnumMemberValidation(UnmappedMemberValidation.Strict)
    .Members(value => ComputeTarget(value));
```

Active → Active, New → Deleted. Source покрыт; Destination даёт warning о границе анализа,
поскольку тело ComputeTarget не анализируется. `_ => result`/Using также не перечисляют
destination-значения и не доказывают полноту Members.

### 106. Aliases в coverage — одна физическая группа

```csharp
enum CoveredAlias { Ready = 1, Active = 1 }
enum CoveredAliasDto { Ready = 10, Available = 10 }

builder.Map<CoveredAlias, CoveredAliasDto>()
    .UnmappedEnumMemberValidation(UnmappedMemberValidation.Strict)
    .Members(value => value switch { CoveredAlias.Ready => CoveredAliasDto.Ready });
```

Оба aliases → 10, без отдельных warnings для Active/Available. Discard относится ко всей
числовой группе.

### 107. Для integer/string проверяется конечная enum-сторона

```csharp
builder.Map<int, StoredCode>()
    .UnmappedEnumMemberValidation(UnmappedMemberValidation.Destination);
builder.Map<DomainStatus, string>()
    .UnmappedEnumMemberValidation(UnmappedMemberValidation.Source);
```

Integer → StoredCode: 0/1/2 закрывают Destination, 42 бросает. DomainStatus → string: все
объявления закрывают Source, неназванное 123 бросает. Бесконечные множества строк/чисел не
перечисляются.

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

Обе пары доступны mapper. Auto вместо Map не подходит: между разными enum нет implicit
conversion; same-enum property копируется. Get-only enum нельзя обновить по месту — нужно
присвоить возвращённый scalar.

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

Active → (Active, "status"), Cancelled → (Deleted, "status") с именами Status/Label. Update
Cancelled с (Active, "old") возвращает (Deleted, "status"); исходный ValueTuple меняется
только после присваивания результата. Tuple-пара остаётся структурной.

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

Scalar Map требует явного source: принимающего member для вывода имени нет.

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

Cancelled → null; Suspended → Disabled; Active → Active; Legacy → исключение. Value задаёт
готовый результат, typed Auto — типизацию конвенции.

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

Active → DTO { Code = 2, Label = "status" }. Структурный Construct допустим; у scalar
destination вместо него Members/Using/Convert.

### 113. Неприменимые настройки диагностируются

Каждая строка — отдельное добавление к корректной регистрации соответствующей пары
без изменения типа входа конвенции. Вычисляемые входы рассмотрены в сценариях 143–149.

| Пара | Явная pair-настройка | Ожидаемый результат конфигурации |
|---|---|---|
| `int → StoredCode` | `.EnumMappingStrategy(EnumMappingStrategy.ByName)` | MORPH0023 |
| `DomainStatus → int` | `.EnumMappingStrategy(EnumMappingStrategy.ByName)` | MORPH0023 |
| `string → DomainStatus` | `.EnumMappingStrategy(EnumMappingStrategy.ByValue)` | MORPH0023 |
| `DomainStatus → ApiStatus`, оба без Flags | `.FlagsMappingMode(FlagsMappingMode.ByBit)` | MORPH0023 |
| `int → Access` | `.FlagsMappingMode(FlagsMappingMode.ByMask)` | MORPH0023 |
| `Access → string` с ByValue | `.FlagsMappingMode(FlagsMappingMode.ByBit)` | MORPH0023 |
| `DomainStatus → ApiStatus` | `.ConstructorSelection(ConstructorSelection.Default)` | MORPH0023 даже при значении Default |

Неприменимый общий default игнорируется; неверное значение настройки диагностируется. Using
без Members допускает совместимую настройку без конвенции поверх результата.

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

Здесь `_` — переменная: Internal не исключён, warning сохраняется; runtime — Active/Deleted.
Настоящий discard нужен отдельным statement верхнего уровня Members.

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

STATUS_PENDING_APPROVAL → PendingApproval по правилу; без него ByName не находит соответствия.
Префиксы, подчёркивания, EnumMember/Description, reverse, общий numeric parsing,
коллекции/проекции и настройка разделителей остаются вне feature.

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

С ApiStatus? условия Update недостаточно: destination может быть null, result недоступен.
Такое чтение и безусловное чтение на Create требуют фабрики либо другого пути.

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

При Explicit Active → Unrecognized с одним вызовом. Var-pattern без guard — fallback. Заранее
вычисленный `var fallback = LogFallback(status);` сохранит вызов перед switch, как в 47.

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
какие поля участвуют в результате. Scalar Members поддерживает такой source
и enum destination; выбор входа конвенции показан в [сценариях 170–175](#members-для-objecttuple--enum).
[ResolveUsing](../api/resolve-using.md), [ConstructUsing](../api/construct-using.md)
и [Convert](../api/convert.md) также доступны. Их тела — обычный C#: switch
не дополняется enum-конвенцией, а DSL Auto/Map внутри этих callback не используется.

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
элемента кортежа или вложенной пары не выполняется: пользователь задаёт
нужное выражение и явно вызывает вложенный mapping.

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

Comment не участвует. Прямое Members(source => source.Status) даёт те же результаты, включая
123; Update тоже выбирает новое значение.

### 120. Класс → другой enum через зарегистрированную пару

Используется StatusCarrier из сценария 119 и общая enum-пара выше.

```csharp
builder.Map<StatusCarrier, ApiStatus>()
    .Members(source => Map<ApiStatus>(source.Status));
```

| Вход / операция | Вложенная операция | Результат |
|---|---|---|
| Create, Status = Active | Create | ApiStatus.Active |
| Create, Status = Cancelled | Create | ApiStatus.Deleted |
| Update, Status = Cancelled, previous = Archived | Update с Archived | Deleted: в общей enum-паре нет правила сохранения previous |
| Create или Update, Status = Legacy | Соответствующая операция | Исключение вложенного mapping |

Map выбирает Update по доступному result (включая Using на внешнем Create), иначе Create.
Новое правило вложенной пары, сохраняющее Archived, подействует здесь. Явный
Create<ApiStatus>(source.Status) всегда вызывает Create.

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

Using содержит обычный C# switch без добавленной конвенции.

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

Update вычисляет оба поля заново; результат сохраняется присваиванием `facts =
mapper.Map(status, facts)`.

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
    .Members(source =>
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

Обе пары обрабатывают source целиком. Roundtrip Read | Audit → DTO → Access даёт Read: для
Audit поля нет.

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

Status выбран выражением пользователя; tuple не выбирает enum-компонент автоматически.

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

ValueTuple не null; null его элементов обрабатывает явный guard. Tuple позволяет передать
вместе модель и политику выбора.

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

Для (bool, DomainStatus) Item1 станет условием, Item2 — входом mapping; обращения явные,
позиционной конвенции нет.

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

После null policy callback читает non-null System.Tuple; read-only элементы не требуют
пересоздания.

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

Resolve возвращает другой Tuple: Update Cancelled → Deleted / "Cancelled". Create элементов
используется при обеих внешних операциях; mutable ValueTuple — 109.

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

ReturnDestination сохранит previous для null объекта на Update; объект с Status = null вернёт
null из callback без повторной policy.

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

Callback получает распакованный tuple; отсутствие tuple отличается от нулевого enum-элемента.

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

Для нового значения на Update нужен ResolveUsing (119). Nullable destination с null и policy
Create вызовет ConstructUsing: результат Active.

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

Convert получает исходный nullable tuple; source.Value защищён явным guard. В отличие от
ResolveUsing, null не завершается до callback.

## Выбор входа конвенции

### 133. Tuple switch не выбирает компонент для конвенции

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .ResolveUsing((status, previous) => ApiStatus.Pending)
    .Members((status, previous, result) => (status, result) switch
    {
        (DomainStatus.Cancelled, ApiStatus.Pending) => ApiStatus.Deleted,
        _ => result
    });
```

| Вход | Результат при Auto и Explicit |
|---|---|
| Cancelled | Deleted |
| Active | Pending |
| Legacy | Pending |

Для tuple → ApiStatus конвенции нет, поэтому неявный автоматический шаг
отсутствует. Ни первый элемент, ни элемент enum-типа не выбирается за пользователя.
Перестановка в `(result, status)` с соответствующей перестановкой pattern не
меняет результат. Замена fallback на `_ => Auto()` в обоих вариантах требует
диагностики: у текущего tuple-входа нет поддерживаемой конвенции.

Если нужна конвенция source → destination, тот же специальный случай выражается
через source switch и guard, без новой формы Auto:

```csharp
.Members((status, previous, result) => status switch
{
    DomainStatus.Cancelled when result == ApiStatus.Pending
        => ApiStatus.Deleted,
    _ => Auto()
})
```

Cancelled → Deleted; Active → Active; Legacy → исключение. `_ => result` сохраняет фабричное
значение при неуспехе (40); result switch выбирает его как вход, оставляя status для guard.

### 134. Object-вход switch не означает автоматический поиск enum-поля

`GetDetails` возвращает объект с полями, указанными в таблице:

```csharp
sealed class StatusDetails
{
    public DomainStatus Status { get; init; }
    public bool IsBlocked { get; init; }
}

builder.Map<DomainStatus, ApiStatus>()
    .Members(status => GetDetails(status) switch
    {
        { IsBlocked: true } => ApiStatus.Disabled,
        _ => ApiStatus.Unknown
    });
```

| Source | GetDetails: Status / IsBlocked | Результат |
|---|---|---|
| Active | Active / false | Unknown |
| Legacy | Active / false | Unknown |
| Active | Active / true | Disabled |

GetDetails вызывается один раз; конвенция не выбирает ни исходный status, ни поле Status.
Явный Auto диагностируется. Whole-object регистрация следует тому же правилу (172).

### 135. Нормализация бита меняет вход конвенции, сохраняя проходы Members

Используются SourceAccess и TargetAccess из раздела flags между enum.
`NormalizeFlag` записывает переданный бит в список и всегда возвращает
SourceAccess.Audit. Конфигурация сохраняет default ByBit и ByName:

```csharp
builder.Map<SourceAccess, TargetAccess>()
    .Members(flag => NormalizeFlag(flag) switch { _ => Auto() });
```

| Вход | Вызовы NormalizeFlag | Результат |
|---|---|---|
| Read | Read | TargetAccess.Audit = 64 |
| Read \| Write | Read, затем Write | Audit = 64: OR двух одинаковых вкладов |
| `(SourceAccess)16` | 16 | Audit = 64 |
| None = 0 | 0 один раз | Audit = 64 |

Auto получает вычисленное Audit без повторного NormalizeFlag. Составной вход и границы
fallback — [150–158](#составной-вход-bybit-и-вычисления).

## IncludeBase и разные входы switch

Используются DomainStatus и ApiStatus из начала каталога. Настройки по умолчанию —
ByName и MemberSelection.Auto. Фрагменты базы и локальной пары размещаются в той
же конструкции наследования, что в сценарии 45: точная пара включается через
IncludeBase после base.Configure. Все результаты относятся к Create и Update.

### 136. Локальная нормализация не меняет вход базовых правил

```csharp
// Базовый mapper.
builder.Map<DomainStatus, ApiStatus>()
    .Members(status => status switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted,
        _ => ApiStatus.Unknown
    });

// Текущий mapper после base.Configure(builder).
builder.Map<DomainStatus, ApiStatus>()
    .IncludeBase<DomainStatus, ApiStatus>()
    .Members(status => Normalize(status) switch
    {
        DomainStatus.Suspended => ApiStatus.Disabled
    });
```

Normalize возвращает указанное в таблице значение и вызывается один раз.

| Исходный status | Normalize вернул | Результат |
|---|---|---|
| Active | Pending | Pending по локальной конвенции |
| Cancelled | Pending | Deleted: специальное правило базы раньше конвенции |
| Legacy | Suspended | Disabled: локальное специальное правило |
| Pending | Legacy | Unknown: локальная конвенция неуспешна, выполняется базовый fallback |
| Unknown | Cancelled | Unknown: базовый case проверяет исходный Unknown, а не нормализованный Cancelled |

При Explicit Active → нормализованный Pending даёт Unknown; остальные строки прежние. Базовый
Cancelled обрабатывается до fallback.

### 137. Унаследованный Auto и отсутствующий fallback могут различаться

Используется сценарий 136 с исходным Pending и результатом Normalize = Legacy.
Специальные ветки не совпадают. Каждая строка — отдельный вариант fallback:

| Fallback базы | Локальный fallback | Результат при Auto |
|---|---|---|
| Отсутствует | Отсутствует | Исключение: локальная конвенция для Legacy неуспешна |
| `_ => ApiStatus.Unknown` | Отсутствует | Unknown |
| `_ => Auto()` | Отсутствует | Pending: базовый Auto получает исходный Pending |
| `_ => Auto()` | `_ => Auto()` | Исключение: локальный Auto получает Legacy и заменяет базовый fallback |
| `_ => Auto()` | `_ => ApiStatus.Unrecognized` | Unrecognized |

Базовый Auto без локального fallback: Normalize = Active даёт локальный Active, а при Explicit
— базовый Pending. Source Legacy и Normalize = Corrupt: базовый Auto тоже бросает. Normalize
не повторяется. Одна неявная попытка может продолжиться явной для другого входа; поэтому
inherited Auto отличается от отсутствия fallback.

### 138. Auto в специальной базовой ветке завершает поиск немедленно

В сценарии 136 заменить базовый Members на:

```csharp
.Members(status => status switch
{
    DomainStatus.Active or DomainStatus.Cancelled => Auto(),
    _ => ApiStatus.Unknown
})
```

Локальный Members прежний; Normalize возвращает Pending.

| Исходный status | Результат |
|---|---|
| Active | Active: базовая специальная ветка Auto работает с исходным Active |
| Cancelled | Исключение: у Cancelled нет соответствия, локальная конвенция для Pending не достигается |
| Legacy | Pending: специальная базовая ветка не совпала, работает локальная конвенция |

Auto не переходит к Unknown. Замена специальной ветки на Active when IsEnabled() => Auto() с
false даёт Pending по локальной конвенции.

### 139. Без локального Members вход задаёт ближайший унаследованный Members

Цепочка Base → Middle → Application включает одну точную пару через IncludeBase
на каждом уровне. Base содержит правила сценария 136, Middle — его нормализующий
Members. Для исходного Active метод Normalize уровня Middle возвращает Pending.
Отдельный NormalizeApplication возвращает Legacy.

| Members уровня Application | Результат для исходного Active |
|---|---|
| Не задан, только IncludeBase | Pending: вход неявной конвенции задаёт унаследованный Members уровня Middle |
| `status switch { DomainStatus.Corrupt => ApiStatus.Unrecognized }` | Active: теперь вход задаёт локальный switch |
| `NormalizeApplication(status) switch { DomainStatus.Corrupt => ApiStatus.Unrecognized }` | Unknown: конвенция для Legacy неуспешна, затем базовый fallback |
| Последний вариант с MemberSelection.Explicit | Unknown: неявная конвенция пропущена |

Неявные Middle.Pending/Base.Active не пробуются после локального неуспеха; их специальные
правила сохраняются. Без Application Members при Explicit результат Unknown из inherited
fallback.

### 140. Неподдерживаемый локальный вход не заменяется базовым

Base остаётся как в сценарии 136. Локальная пара:

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .IncludeBase<DomainStatus, ApiStatus>()
    .ResolveUsing((status, previous) => ApiStatus.Pending)
    .Members((status, previous, result) => (status, result) switch
    {
        (DomainStatus.Corrupt, ApiStatus.Pending) => ApiStatus.Unrecognized
    });
```

| Исходный status | Результат |
|---|---|
| Active | Unknown: tuple не имеет конвенции; базовый source не выбирается вместо него |
| Cancelled | Deleted по специальному правилу базы |
| Corrupt | Unrecognized по локальному правилу |
| Legacy | Unknown |

Базовый `_ => Auto()`: Active → Active, Legacy бросает. Локальный Auto для tuple ошибочен;
локальный `_ => result` допустим и даёт Pending для Active/Legacy, сохраняя специальные
Cancelled/Corrupt.

### 141. Входы вычисляются один раз, fallback сохраняет свой контекст

```csharp
// Members базы; ReadBaseInput считает вызовы.
.Members(status =>
{
    var input = ReadBaseInput(status);
    return input switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted,
        var remaining => RecordFallback(remaining)
    };
})

// Локальный Members после IncludeBase; NormalizeLocal считает вызовы.
.Members(status => NormalizeLocal(status) switch
{
    DomainStatus.Suspended => ApiStatus.Disabled
})
```

RecordFallback записывает свой аргумент и возвращает ApiStatus.Unrecognized.
Исходный status = Active; строки задают отдельные варианты поведения методов.

| NormalizeLocal вернул | ReadBaseInput вернул | Результат | Вызовы Local / Base / Fallback |
|---|---|---|---|
| Suspended | Не вызывается | Disabled | 1 / 0 / 0 |
| Pending | Cancelled | Deleted | 1 / 1 / 0 |
| Pending | Legacy | Pending по локальной конвенции | 1 / 1 / 0 |
| Legacy | Corrupt | Unrecognized; RecordFallback получил Corrupt | 1 / 1 / 1 |
| Pending | Бросает InvalidOperationException | То же исключение, конвенция для Pending не запускается | 1 / 1 / 0 |

Fallback использует вход своего switch. С базовым Auto и ReadBaseInput = Active локальный
Legacy даёт Active; оба метода вызваны один раз. Независимые вычисления не объединяются.

### 142. Локальный result switch и базовый source switch сохраняют разные входы

ChooseInitial возвращает значение из таблицы. Base:

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .ResolveUsing((status, previous) => ChooseInitial(status))
    .Members(status => status switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted,
        _ => Auto()
    });
```

Локальная пара после base.Configure:

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .IncludeBase<DomainStatus, ApiStatus>()
    .Members((status, previous, result) => result switch
    {
        ApiStatus.Disabled => ApiStatus.Archived
    });
```

| Исходный status | Начальный result | Итог |
|---|---|---|
| Active, Legacy | Pending | Pending по локальной конвенции result → result |
| Cancelled | Pending | Deleted: базовое специальное правило раньше конвенции |
| Cancelled | Disabled | Archived: локальное специальное правило раньше базового |
| Active | `(ApiStatus)123` | Active: локальная строгая E → E конвенция неуспешна, затем базовый Auto для source |
| Legacy | `(ApiStatus)123` | Исключение: базовый Auto тоже не нашёл соответствия |

Локальный `_ => result` заменит базовый Auto и сохранит 123 для Active/Legacy. Using
вызывается один раз; result общий для уровней.

## Стратегия и тип входа конвенции

Используются обычные enum DomainStatus/ApiStatus из начала каталога и
WireCode/StoredCode из раздела «Числа и диапазоны», без Flags. Правило выбора стратегии — в
[дизайне](ENUM_MAPPING_DESIGN.md#стратегия-для-входа-конвенции).

### 143. Default выбирается после определения типа входа

```csharp
builder.Map<WireCode, StoredCode>()
    .Members(code => ((int)code) switch { _ => StoredCode.Unknown });
```

| Исходный code | Вход конвенции | Результат |
|---|---|---|
| Active = 1 | int 1 | Pending = 1 |
| Pending = 2 | int 2 | Active = 2 |
| `(WireCode)42` | int 42 | Unknown через fallback |
| Large = 300 | int 300 | Unknown через fallback; число не помещается в byte |

Default здесь — строгий ByValue для int → StoredCode. Он не фиксируется как
ByName при регистрации WireCode → StoredCode. Вариант с `code switch` использует
ByName и даёт Active → Active, Pending → Pending. Явное значение настройки
`EnumMappingStrategy.Default` без настроек на других уровнях не меняет результаты.

Обратное изменение типа также меняет library default:

```csharp
builder.Map<int, StoredCode>()
    .Members(code => ((WireCode)code) switch { _ => StoredCode.Unknown });
```

1 → StoredCode.Active = 2; 2 → StoredCode.Pending = 1 по имени после явного C# cast. Отдельная
WireCode → StoredCode регистрация не нужна.

### 144. Строковый вход получает конвенцию имён

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status => NormalizeText(status) switch { _ => ApiStatus.Unknown });
```

NormalizeText возвращает string и вызывается один раз. Исходный status = Active;
строки таблицы задают отдельные варианты результата метода.

| NormalizeText вернул | Результат |
|---|---|
| `"Pending"` | Pending |
| `"active"` | Active |
| `"20"` | Unknown; числовой текст не разбирается |
| `" Active "` | Unknown; скрытого Trim нет |
| `"Missing"` | Unknown |

Auto вместо fallback бросает для последних трёх входов; typed Auto эквивалентен. ByName
допустим; ByValue/AllowUndefined дают MORPH0023 независимо от fallback, без numeric parsing.

### 145. Совместимая явная стратегия сохраняется при смене типа

Recode возвращает StoredCode.Active = 2:

```csharp
builder.Map<WireCode, string>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValue)
    .Members(code => Recode(code) switch { _ => Auto() });
```

Для исходного WireCode.Active = 1 результат — `"2"`, число вычисленного входа.
ByValueAllowUndefined даёт ту же строку; ByName и отсутствие настроенной стратегии
дают `"Active"`. Это StoredCode → string, а не повторная обработка исходного WireCode.

Другие отдельные конфигурации используют ту же форму
`Input(source) switch { _ => Auto() }`; Input возвращает значение указанного типа:

| Зарегистрированная пара | Тип и результат Input | Явная стратегия пары | Результат |
|---|---|---|---|
| WireCode → StoredCode | int 42 | ByValue | Исключение маппинга: число не объявлено |
| WireCode → StoredCode | int 42 | ByValueAllowUndefined | Неназванное 42 |
| WireCode → StoredCode | int 300 | ByValueAllowUndefined | Исключение: диапазон byte не расширяется |
| WireCode → StoredCode | int 1 | ByName | MORPH0023 |
| string → StoredCode | int 1 | ByValue | Pending = 1 |
| int → StoredCode | WireCode.Active = 1 | ByName | Active = 2 |
| WireCode → byte | StoredCode.Active = 2 | Не задана | byte 2 |
| WireCode → byte | StoredCode.Active = 2 | ByValue / ByValueAllowUndefined | byte 2 |
| WireCode → byte | StoredCode.Active = 2 | ByName | MORPH0023: enum → integer требует числовую стратегию |

Тип выбранного входа определяет совместимость: string-регистрация с вычисленным int допускает
ByValue. Неуспех допустимой конвенции обрабатывается fallback, несовместимая стратегия —
ошибка конфигурации.

### 146. Разные пути Members могут иметь разные library defaults

```csharp
builder.Map<WireCode, StoredCode>()
    .Members(code =>
    {
        if (UseNumericInput())
            return ((int)code) switch { _ => StoredCode.Unknown };

        return code switch { _ => StoredCode.Unknown };
    });
```

| Настройка пары | Active = 1, условие true | Active = 1, условие false |
|---|---|---|
| Не задана | Pending = 1, int → enum по ByValue | Active = 2, enum → enum по ByName |
| ByValue | Pending = 1 | Pending = 1 |
| ByValueAllowUndefined | Pending = 1 | Pending = 1 |
| ByName | MORPH0023 для числового пути; конфигурация ошибочна независимо от результата UseNumericInput | Та же диагностика |

Explicit без Auto: обе ветви дают Unknown, ByName остаётся допустимой по исходной enum-паре.
Auto в int-пути снова делает её несовместимой. Auto вне switch использует WireCode, поэтому
ByName допустима. Прямой результат/Using также допускает ByName без конвенции поверх
результата.

### 147. Общий default отличается от явной настройки пары

Для числового switch из сценария 143 исходный code = `(WireCode)42`.
Каждая строка — отдельная конфигурация; имена значений EnumMappingStrategy сокращены.

| MSBuild | Текущий mapper | Текущая пара | Результат |
|---|---|---|---|
| Не задано | Не задано | Не задано | Unknown: строгий library default |
| Не задано | ByValueAllowUndefined | Default | Неназванное 42 |
| ByValueAllowUndefined | ByName | Default | Unknown: общий ByName неприменим к int → enum; нижний AllowUndefined повторно не ищется |
| Не задано | ByValueAllowUndefined | ByValue | Unknown: явный строгий выбор пары |
| Не задано | ByValueAllowUndefined | ByName | MORPH0023; явный выбор пары не игнорируется |

Default продолжает наследование; mapper/MSBuild задают общие defaults, pair-настройка —
требование конкретной пары.

### 148. IncludeBase сохраняет выбор стратегии, но не заранее вычисленный default

База:

```csharp
builder.Map<int, StoredCode>()
    .Members(code => code switch { _ => Auto() });
```

Локальная пара после base.Configure:

```csharp
builder.Map<int, StoredCode>()
    .IncludeBase<int, StoredCode>()
    .Members(code => Decode(code) switch
    {
        WireCode.Unknown => StoredCode.Unknown
    });
```

Decode возвращает WireCode; строки задают отдельные варианты результата метода.
Настройка ByValue/ByValueAllowUndefined, когда указана, задаётся локальной паре.

| Исходный code | Decode вернул | Стратегия не задана | ByValue | ByValueAllowUndefined |
|---|---|---|---|---|
| 1 | Active = 1 | Active = 2 по локальному ByName | Pending = 1 | Pending = 1 |
| 1 | Large = 300 | Pending = 1 через базовый Auto с default ByValue | Pending = 1 через базовый Auto | Pending = 1 через базовый Auto; локальное 300 вне диапазона |
| 42 | `(WireCode)42` | Исключение базового Auto | Исключение базового Auto | Неназванное 42 по локальной конвенции |

Default выбирается отдельно: локальный enum — ByName, базовый int — ByValue. Explicit
пропускает локальную попытку; базовый Auto для 42 бросает. Decode один раз.

Inherited AllowUndefined сохраняется через локальный Default и выше mapper ByName; локальный
ByValue задаёт строгую стратегию обоим входам. Локальный ByName несовместим с базовым int
Auto. Замена этого fallback локальным Unknown убирает конфликт: при source 1 и Decode = Large
результат Unknown. Без локального Members ByName по-прежнему неприменима.

### 149. Смена типа не создаёт новых видов автоматического преобразования

Каждая строка — отдельный mapping switch над вычисленным входом. Во всех строках
EnumMappingStrategy не задана; у mapping switch есть указанный обычный fallback.

| Зарегистрированная пара | Статический тип и значение входа switch | Fallback | Результат |
|---|---|---|---|
| WireCode → string | int 1 | `"fallback"` | `"fallback"`; integer → string не входит в enum-конвенцию |
| WireCode → string | string `"Active"` | `"fallback"` | `"fallback"`; string → string также не входит |
| WireCode → int | int 1 | `-1` | -1; общего integer → integer mapping здесь нет |
| WireCode → StoredCode | object, содержащий WireCode.Active | StoredCode.Unknown | Unknown; runtime enum не меняет статический object-вход |

Auto вместо fallback во всех строках диагностируется. Runtime object не выбирает скрытый enum;
явный результат остаётся допустимым.

## Составной вход ByBit и вычисления

Сценарии 150–152 используют SourceAccess и TargetAccess из раздела flags между
enum; последующие примеры задают свои типы. Настройки по умолчанию — ByBit,
ByName и MemberSelection.Auto. Проверяются
[побитовая конвенция вычисленной маски и fallback всего текущего вклада](ENUM_MAPPING_DESIGN.md#составной-вычисленный-вход-в-bybit),
с сохранением исходных проходов и однократных пользовательских вычислений.

### 150. Составной результат Normalize не запускает специальные правила заново

NormalizeFlag записывает свой аргумент и возвращает SourceAccess.ReadWrite.
RecordRead/RecordWrite считают вызовы и возвращают TargetAccess.View/Edit.

```csharp
builder.Map<SourceAccess, TargetAccess>()
    .Members(flag => NormalizeFlag(flag) switch
    {
        SourceAccess.Read => RecordRead(),
        SourceAccess.Write => RecordWrite(),
        _ => Auto()
    });
```

Для исходного Read NormalizeFlag вызывается один раз с Read. Вход switch —
ReadWrite, поэтому RecordRead и RecordWrite не вызываются. Auto получает
ReadWrite целиком и переводит его биты встроенной конвенцией. В TargetAccess
нет имён Read и Write, поэтому конвенция неуспешна. Явные Read → View и
Write → Edit не становятся её таблицей соответствий и не запускаются повторно.

| Selection | Завершающая ветка | Согласованное поведение для исходного Read |
|---|---|---|
| Auto | `_ => Auto()` | Исключение маппинга: нет конвенционного перевода битов Read/Write |
| Explicit | `_ => Auto()` | То же исключение: явный Auto доступен и при Explicit |
| Auto | `_ => TargetAccess.Unknown` | Unknown = 128 после неуспеха всей попытки конвенции |
| Explicit | `_ => TargetAccess.Unknown` | Конвенция пропущена, итог Unknown = 128 |

В последнем варианте Read | Write вызывает NormalizeFlag дважды, по порядку Read/Write, даёт
128 без RecordRead/RecordWrite. Два независимо написанных NormalizeFlag внутри одного прохода
также не объединяются.

### 151. Составной локальный вход не подменяет и не повторяет правила IncludeBase

ReadBaseInput записывает аргумент и возвращает его без изменений;
RecordBaseRead считает вызовы и возвращает TargetAccess.View. База:

```csharp
builder.Map<SourceAccess, TargetAccess>()
    .Members(flag => ReadBaseInput(flag) switch
    {
        SourceAccess.Read => RecordBaseRead(),
        _ => TargetAccess.Unknown
    });
```

Локальная пара после base.Configure; NormalizeFlag возвращает значение из таблицы:

```csharp
builder.Map<SourceAccess, TargetAccess>()
    .IncludeBase<SourceAccess, TargetAccess>()
    .Members(flag => NormalizeFlag(flag) switch
    {
        SourceAccess.Delete => TargetAccess.None
    });
```

| Исходный бит | NormalizeFlag вернул | Вызовы Normalize / ReadBaseInput / RecordBaseRead | Согласованное поведение |
|---|---|---|---|
| Read | ReadWrite | 1 / 1 / 1 | View = 16: специальное правило базы сработало для её входа Read |
| Read | Delete | 1 / 0 / 0 | None = 0: локальное правило завершило проход до базы |
| Audit | ReadWrite | 1 / 1 / 0 | Unknown = 128: локальная конвенция не переводит Read/Write, выбран fallback базы |

В строке 3 Read внутри ReadWrite не вызывает RecordBaseRead. Вариант: Explicit, базовый Auto,
ReadBaseInput возвращает ReadWrite — исключение, поскольку TargetAccess не имеет Read/Write.
Normalize/ReadBaseInput по одному разу, RecordBaseRead не вызывается.

### 152. Полный result передаётся конвенции без повторного Members

CreateInitial считает вызовы и возвращает TargetAccess.View | TargetAccess.Edit.
Observe записывает исходный бит; RecordView считает вызовы и возвращает TargetAccess.Audit.

```csharp
builder.Map<SourceAccess, TargetAccess>()
    .ResolveUsing((mask, previous) => CreateInitial(mask))
    .Members((flag, previous, result) =>
    {
        Observe(flag);
        return result switch
        {
            TargetAccess.View => RecordView(),
            _ => Auto()
        };
    });
```

Source Read: фабрика/Observe по одному разу, RecordView не вызывается; Auto переводит полный
result = 48, итог 48 (также при Explicit/typed Auto). ByMask бросает: целого имени 48 нет.

Explicit с `_ => result`, source Read | Write: итог 48 без конвенции, Observe(Read),
Observe(Write), одна фабрика. Result не накапливает вклады.

### 153. Конвенция вычисленной маски переводит биты, даже если есть composite name

Типы для сценариев 153–156 и 158:

```csharp
[Flags] enum InputAccess
{
    None = 0, Read = 1, Write = 2, Audit = 4, ReadWrite = Read | Write
}

[Flags] enum OutputAccess
{
    None = 0, Read = 16, Write = 32, ReadWrite = 128, Unknown = 256
}

builder.Map<InputAccess, OutputAccess>()
    .Members(flag => Normalize(flag) switch { _ => OutputAccess.Unknown });
```

Исходный вход — Read. Normalize вызывается один раз и возвращает указанную маску:

| Вычисленный вход | Результат ByBit | Результат ByMask |
|---|---|---|
| None = 0 | 0 | 0 |
| Read = 1 | OutputAccess.Read = 16 | 16 |
| ReadWrite = 3 | Read \| Write = 48 | OutputAccess.ReadWrite = 128 |
| Read \| Audit = 5 | Unknown = 256 | Unknown = 256 |
| `(InputAccess)8` | Unknown = 256 | Unknown = 256 |

Composite name не меняет ByBit, None не требуется для 0. Explicit + Auto/typed Auto: ReadWrite
→ 48, Read | Audit бросает. Explicit + обычный fallback всегда даёт Unknown.

### 154. Fallback заменяет весь вклад неудачной попытки, включая успешные биты

Исходный вход — InputAccess.Read; Normalize возвращает Read | Audit = 5.
Фабрика создаёт доступный начальный result = OutputAccess.Write = 32:

```csharp
builder.Map<InputAccess, OutputAccess>()
    .ResolveUsing((mask, previous) => OutputAccess.Write)
    .Members((flag, previous, result) => Normalize(flag) switch
    {
        _ => result
    });
```

Read переводится в 16, Audit не переводится. Частичный перевод 16 отбрасывается.
Каждая строка заменяет завершающую ветку; Normalize и выбранное выражение
fallback вычисляются по одному разу:

| Завершающая ветка | Итог текущего прохода и всей операции |
|---|---|
| `_ => OutputAccess.Unknown` | 256, а не 272 |
| `_ => (OutputAccess)0` | 0, а не 16 |
| `_ => result` | 32, а не 48 |
| `_ => Ignore()` или `_ => Ignore<OutputAccess>()` | 0: весь вклад пропущен, ни частичный 16, ни initial result = 32 не добавляются |
| `_ => Auto()` или отсутствие fallback | Исключение маппинга |

Var remaining передаёт RecordFallback полный Read | Audit = 5 один раз; результат 256.
Explicit с обычным fallback даёт те же значения без конвенции, с Auto бросает. Исключения
Normalize/RecordFallback сохраняются.

### 155. Исходная комбинация и вычисленная комбинация имеют разные границы fallback

Во всех строках используется InputAccess → OutputAccess, обычный завершающий
fallback `_ => OutputAccess.Unknown` и настройки по умолчанию.

| Исходный вход | Вход mapping switch в каждом проходе | Итог |
|---|---|---|
| Read \| Audit = 5 | Неизменённый flag: Read, затем Audit | Read \| Unknown = 272 |
| Read = 1 | Normalize(Read) = Read \| Audit | Unknown = 256 |
| Read \| Write = 3 | Normalize(Read) = Read \| Audit; Normalize(Write) = Write | Unknown \| Write = 288 |
| Read \| Write = 3 | Normalize(Read) = Read; Normalize(Write) = Read \| Audit | Read \| Unknown = 272 |

Normalize один раз на исходный бит, Read перед Write. Fallback заменяет только свой проход.
Вынос Normalize до mapping может менять итог; local внутри прохода — нет. Без fallback в
последней строке второй проход бросает без частичного результата, эффекты обоих вызовов
сохраняются.

### 156. Унаследованный Auto не получает частичный результат локальной конвенции

Базовая конфигурация; локальная пара включает её после base.Configure:

```csharp
// База.
builder.Map<InputAccess, OutputAccess>()
    .Members(flag => flag switch { _ => Auto() });

// Локальная пара.
builder.Map<InputAccess, OutputAccess>()
    .IncludeBase<InputAccess, OutputAccess>()
    .Members(flag => Normalize(flag) switch
    {
        InputAccess.None => OutputAccess.None
    });
```

Source Write = 2; один Normalize → Read | Audit = 5. Локальная попытка отбрасывает частичный
Read = 16, базовый Auto переводит свой Write: итог **32, не 48**. Локальный fallback Unknown
даст 256; локальный Auto бросит для 5 вместо обращения к базовому.

### 157. Известный composite в result не гарантирует строгий ByValue в ByBit

SourceBits и DestinationPair — типы сценария 66; в DestinationPair объявлено
только Pair = 3. Исходный вход — SourceBits.Read:

```csharp
builder.Map<SourceBits, DestinationPair>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValue)
    .ResolveUsing((mask, previous) => DestinationPair.Pair)
    .Members((flag, previous, result) => result switch { _ => Auto() });
```

| Режим / стратегия / вариант Members | Результат |
|---|---|
| ByBit / ByValue / как выше | Исключение: отдельные 1 и 2 не допустимы в DestinationPair |
| ByBit / ByValue / `_ => result` | Pair = 3 как окончательный fallback после неуспеха конвенции |
| ByBit / ByValueAllowUndefined / как выше | Pair = 3, OR допустимых 1 и 2 |
| ByMask / ByValue / как выше | Pair = 3, проверяется целое объявленное значение |
| ByBit / ByValue / `((int)result) switch { _ => Auto() }` | Pair = 3: фактический int-вход переводится целиком |

Одна фабрика; result — полный Pair. Cast в int меняет конвенцию на целое число, сохраняя
исходные проходы; typed Auto эквивалентен.

WideMask → NarrowMask (67): source 1, один Normalize → All = -1. ByBit + AllowUndefined не
переводит положительный бит 128 в sbyte: fallback 0 даёт 0 без меньших битов, Auto бросает.
ByMask даёт -1; AllowUndefined не разрешает overflow.

### 158. Строковый вывод вычисленной маски тоже образует один вклад

```csharp
builder.Map<InputAccess, string>()
    .Members(flag => Normalize(flag) switch { _ => "unknown" });
```

Исходный вход — Read; Normalize вызывается один раз. Режим — ByBit + ByName:

| Вычисленная маска | Результат |
|---|---|
| ReadWrite = 3 | `"Read, Write"`, а не `"ReadWrite"` |
| `Read \| (InputAccess)8` | `"unknown"`, а не `"Read, unknown"` |
| None = 0 | `"None"` |

Во второй строке Auto бросает, fallback "" даёт "". Если source Read | Write и оба Normalize
возвращают ReadWrite, итог "Read, Write, Read, Write". ByMask даёт целое "ReadWrite". Это не
меняет атомарность composite name во входном string-токене (79).

## Ошибки enum-маппинга

Ниже проверяются тип исключения и смысл сообщения по
[согласованному контракту](ENUM_MAPPING_DESIGN.md#ошибки-выполнения).
Формулировки в таблицах не задают точный текст сообщения. EnumMappingException
и генерация этих ошибок ещё не реализованы.

### 159. Один тип исключения для неуспешной конвенции и отсутствующего правила

Используются DomainStatus/ApiStatus и StoredCode из предыдущих разделов.
Каждая строка — отдельная конфигурация:

| Конфигурация и вход | Результат | Причина в сообщении |
|---|---|---|
| Bare Map<DomainStatus, ApiStatus>(), Legacy | EnumMappingException | Для DomainStatus.Legacy = 6 нет ByName-соответствия |
| Явное `DomainStatus.Legacy => Auto()` и fallback Unknown; вход Legacy | EnumMappingException | Неуспех выбранного Auto для Legacy; ветка уже совпала |
| Explicit, только правило `Active => ApiStatus.Active`; вход Pending | EnumMappingException | Подходящего явного правила нет; конвенция отключена, хотя имя Pending есть в destination |
| Bare Map<int, StoredCode>(), 42 | EnumMappingException | Число 42 не объявлено в destination при строгом ByValue |
| Та же пара, 300 | EnumMappingException | Число 300 вне диапазона byte; это не пользовательский OverflowException |
| DomainStatus → ApiStatus с обычным `_ => ApiStatus.Unknown`; вход Legacy | Unknown | Исключения нет: fallback обработал неуспех конвенции |
| Конфликтный AliasSource → AliasTarget из сценария 09 без fallback; вход 1 | Warning при генерации, EnumMappingException при вызове | Неоднозначное соответствие для числа 1, а не отсутствие имени |
| AliasedStatus → string из сценария 21 без явного правила и fallback; вход 1 | Warning при генерации, EnumMappingException при вызове | Неоднозначное выходное имя для числа 1 |

Operation соответствует вызову; SourceType/DestinationType — зарегистрированные типы, например
int/StoredCode, не byte. Nullable<T> сохраняется в паре даже при non-null входе Members.

### 160. Ошибка указывает вычисленную маску и её непереведённый бит

Используются InputAccess/OutputAccess из сценария 153. Normalize считает вызовы
и возвращает InputAccess.Read | InputAccess.Audit = 5:

```csharp
builder.Map<InputAccess, OutputAccess>()
    .Members(flag => Normalize(flag) switch { _ => Auto() });
```

Для исходного InputAccess.Read = 1 Normalize вызывается один раз. Read переводится,
Audit не переводится; итог — EnumMappingException. Сообщение указывает:

- вход попытки: InputAccess со значением 5;
- проблемный бит: Audit = 4, для которого нет имени в OutputAccess;
- причину: отсутствие ByName-соответствия.

Сообщения только об исходном Read = 1 недостаточно. Частичный OutputAccess.Read
не возвращается; ради сообщения Normalize не запускается повторно.

Отдельный вариант той же пары:

```csharp
builder.Map<InputAccess, OutputAccess>()
    .ResolveUsing((mask, previous) => OutputAccess.Read | (OutputAccess)512)
    .Members((flag, previous, result) => result switch { _ => Auto() });
```

Source Read: одна фабрика; ошибка сообщает OutputAccess = 528 и бит 512, свойства пары
остаются InputAccess/OutputAccess. Повторного вычисления для сообщения нет.

### 161. Ошибка унаследованного Auto описывает его собственный вход

Вариант 141: базовый fallback Auto, локального нет; source Pending, NormalizeLocal → Legacy =
6, ReadBaseInput → Corrupt = 5. После локального неуспеха базовый Auto бросает
EnumMappingException для Corrupt, оба метода по одному разу.

ReadBaseInput → Active даст Active без исключения. Локальный `_ => Auto()` заменит базовый и
бросит для Legacy = 6.

### 162. Строковая ошибка сохраняет границу токена или целой попытки

Используется Access из сценария 70. Исходная строка — `"Read,Missing,Write"`;
Unknown в Access имеет значение 8:

| Конфигурация string → Access | Результат | Сообщение при ошибке |
|---|---|---|
| Bare registration, ByBit | EnumMappingException; Write не достигается | Вход попытки `"Missing"`, имя не распознано |
| Bare registration, ByMask | EnumMappingException | Вход попытки `"Read,Missing,Write"`, проблемный токен `"Missing"` |
| ByBit, обычный fallback `_ => Access.Unknown` | Read \| Unknown \| Write = 11 | Исключения нет |
| ByMask, тот же fallback | Unknown = 8 | Исключения нет; частичный Read не добавляется |

Пара ошибки — string/Access, повторного Members нет. Неоднозначность из 20 сообщает именно
конфликт, не отсутствие имени.

### 163. Пользовательские вычисления, конфигурация и lifecycle сохраняют свои ошибки

Каждая строка — отдельный вариант уже приведённых конфигураций:

| Ситуация | Ожидаемое поведение |
|---|---|
| Normalize бросает InvalidOperationException | То же исключение без оборачивания; Normalize не повторяется |
| Явный checked cast в fallback сценария 30 переполняется | Пользовательский OverflowException |
| Fallback сам бросает FormatException | Пользовательский FormatException |
| Вложенный enum mapping из object mapping не находит результата | Его EnumMappingException с парой и операцией вложенного mapping; внешняя пара его не оборачивает |
| Null source при NullSourceHandling.Throw | NullSourceException до Members |
| Update с non-null source и null destination при NullDestinationHandling.Throw | NullDestinationException до Members |
| Create отключён настройкой MappingMode | MappingOperationNotSupportedException |
| Явный ByName для int → StoredCode | MORPH0023; если вызвана типизированная заглушка некорректной пары — MappingConfigurationException |
| Явный Auto для object/tuple-входа без конвенции | Диагностика конфигурации, не runtime-подбор enum-поля |

В 134 убрать fallback, GetDetails возвращает IsBlocked = false, ToString считает вызовы и
бросает InvalidOperationException. Ожидается EnumMappingException с типом StatusDetails:
GetDetails один раз, ToString ни разу, поля ради сообщения не исследуются.

## Ignore в ByBit

Каноническая семантика — в [дизайне](ENUM_MAPPING_DESIGN.md#ignore).
Дополняет маски с фабрикой (61), строковые токены (81), строковые вклады (92)
и fallback вычисленной маски (154).

### 164. Ignore без фабрики: Create, Update, nullable и независимые вклады

Используются SourceAccess/TargetAccess из раздела «Flags между enum».
Record добавляет полученный non-null SourceAccess в список и возвращает его:

```csharp
builder.Map<SourceAccess?, TargetAccess?>()
    .MemberSelection(MemberSelection.Explicit)
    .UnmappedEnumMemberValidation(UnmappedMemberValidation.Source)
    .Members(flag =>
    {
        var recorded = Record(flag);
        return recorded switch
        {
            SourceAccess.Read => TargetAccess.View | TargetAccess.Edit,
            SourceAccess.Write => Ignore(),
            _ => Ignore<TargetAccess?>()
        };
    });
```

| Source | Результат Create и Update | Вызовы Record по порядку |
|---|---|---|
| null | null по default source policy | Нет |
| None = 0 | Non-null 0 | None один раз |
| Write | Non-null 0 | Write |
| Write \| Delete | Non-null 0 | Write, Delete |
| Read \| Write | View \| Edit = 48 | Read, Write |
| `(SourceAccess)16` | Non-null 0 | 16 |

Update с Unknown/null destination (policy Create) даёт те же результаты. Source coverage
закрыт; Ignore не требует result и не очищает Edit от Read. Record сохраняется при всех
Ignore. Замена Write на null/default nullable завершит операцию до Delete.

### 165. Унаследованный Ignore использует effective mode текущей пары

Используется Access из сценария 70. В базе:

```csharp
builder.Map<Access, Access>()
    .FlagsMappingMode(FlagsMappingMode.ByMask)
    .ConstructUsing(mask => Access.Audit)
    .Members(mask => mask switch
    {
        Access.Read => Ignore(),
        _ => Auto()
    });
```

В текущем mapper:

```csharp
base.Configure(builder);
builder.Map<Access, Access>()
    .IncludeBase<Access, Access>()
    .FlagsMappingMode(FlagsMappingMode.ByBit)
    .Members(flag => flag switch { Access.Write => Access.Read });
```

| Effective mode / source | Create | Update с destination = ReadWrite |
|---|---|---|
| ByBit / Read | 0 | 0 |
| ByBit / ReadWrite | Read = 1: вклад Write сохранён | Read = 1 |
| Убрать локальный ByBit: inherited ByMask / Read | Audit = 4 | ReadWrite = 3 |
| Убрать локальный ByBit: inherited ByMask / ReadWrite | ReadWrite = 3 по конвенции | ReadWrite = 3 |

ByBit Ignore завершает Read без конвенции, но не мешает Write добавить Read. ConstructUsing:
один вызов на Create, ноль на non-null Update. ByMask Ignore требует initial result: без
фабрики Create диагностируется; ByBit такого ограничения нет.

### 166. Ignore в специальной ветке, fallback и прямом результате

```csharp
builder.Map<Access, Access>()
    .Members(flag => flag switch
    {
        Access.Read => Ignore(),
        _ => Ignore()
    });
```

| Source | Default Auto | Добавить MemberSelection.Explicit |
|---|---|---|
| Read | 0: специальное правило до конвенции | 0 |
| Write | Write = 2: конвенция до fallback | 0 |
| ReadWrite | Write = 2 | 0 |
| `(Access)16` | 0: fallback после неуспеха | 0 |
| None | 0 по конвенции | 0 через fallback |

Create/Update без фабрики дают одинаковое. Прямой Members(_ => Ignore()): flags → flags всегда
0; string → Access, включая "Read;;Missing", non-null 0; Access → string всегда "", включая
ноль. Initial result/конвенционные имена не добавляются.

## Независимая настройка enum coverage

Канонические правила — в [дизайне](ENUM_MAPPING_DESIGN.md#проверка-покрытия).
Используются CoverageSource и CoverageTarget из сценария 97.

### 167. Общий Destination для объектов и Source для enum

```csharp
class CoverageOrder
{
    public int Id { get; set; }
    public string Legacy { get; set; } = "";
    public CoverageSource Status { get; set; }
}

class CoverageOrderDto
{
    public int Id { get; set; }
    public CoverageTarget Status { get; set; }
    public string Description { get; set; } = "";
}

builder.UnmappedMemberValidation(UnmappedMemberValidation.Destination);
builder.UnmappedEnumMemberValidation(UnmappedMemberValidation.Source);

builder.Map<CoverageSource, CoverageTarget>()
    .Members(value => value switch
    {
        CoverageSource.Cancelled => CoverageTarget.Deleted
    });
builder.Map<CoverageOrder, CoverageOrderDto>()
    .Members((source, _) => new() { Status = Map(source.Status) });
```

| Необработанный элемент | Диагностика |
|---|---|
| CoverageOrder.Legacy | Нет: обычная политика проверяет только destination |
| CoverageOrderDto.Description | Warning обычного member coverage |
| CoverageSource.New | Warning enum source coverage |
| CoverageTarget.Internal | Нет: enum-политика проверяет только source |

Id — конвенция, Status — nested Map. Без общей enum-настройки остаётся default Source. Обычный
None убирает только Description warning, enum None — только New.

### 168. Default и None в независимой цепочке enum-настройки

Во всех вариантах используется Members из сценария 97. Обычная общая
UnmappedMemberValidation задана как Strict, но не участвует в enum coverage.
В таблице перечислены только значения UnmappedEnumMemberValidation;
MSBuild-свойство — MorphantUnmappedEnumMemberValidation. «Нет» означает,
что на уровне нет конкретного enum-значения. Base mappers не задают иных
enum-настроек; IncludeBase при наличии подключён обычным способом.

| MSBuild | Текущий mapper | Включённая пара | Текущая пара | Effective / warnings текущей пары |
|---|---|---|---|---|
| Нет | Default | Нет | Default | Source / New |
| Destination | Default | Нет | Default | Destination / Internal |
| Destination | None | Нет | Default | None / нет |
| Destination | None | Strict | Default | Strict / New и Internal |
| Destination | None | Strict | Source | Source / New |
| Destination | None | Strict | None | None / нет |
| Destination | None | Strict | Сначала None, затем Default | Strict / New и Internal |

Default продолжает enum-цепочку, None отключает coverage, последняя запись побеждает. Runtime
всех строк: Active → Active, Cancelled → Deleted, New бросает.

### 169. Source не меняет сторону проверки для string/integer

```csharp
builder.Map<string, CoverageTarget>()
    .MemberSelection(MemberSelection.Explicit)
    .Members(text => text switch
    {
        "active" => CoverageTarget.Active,
        _ => CoverageTarget.Deleted
    });
```

| Enum coverage | Диагностика |
|---|---|
| Не задан / Source | Нет: у string нет объявленных enum source-значений |
| Destination / Strict | Warning для неиспользованного CoverageTarget.Internal |
| None | Нет |

Все варианты: "active" → Active, остальные non-null строки → Deleted. Bare int →
CoverageTarget также не получает Source warnings: 10/20/30 допустимы по ByValue, 123 бросает.
Enum → string/integer проверяет объявленный source (107).

## Members для object/tuple → enum

Согласованный API — в [дизайне](ENUM_MAPPING_DESIGN.md#objecttuple--enum).
Здесь показаны ожидаемые результаты и доступность DSL. Source coverage выбранного
enum-входа рассмотрен в [сценариях 176–178](#coverage-выбранного-enum-входа).
Прямая конвенция flags/string-входа из объекта показана в
[сценариях 179–184](#flags-конвенция-для-выбранного-входа).

### 170. Конвенция для явно выбранного enum-свойства

Используется StatusCarrier из сценария 119:

```csharp
builder.Map<StatusCarrier, ApiStatus>()
    .Members(source => source.Status switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted
    });
```

| Source.Status | Create и Update с previous = Archived |
|---|---|
| Active | Active = 20 по имени |
| Cancelled | Deleted = 30 по явному правилу |
| Legacy | EnumMappingException |
| `(DomainStatus)20` | EnumMappingException: число destination не заменяет имя source |

Status читается один раз, Comment не участвует; отдельная enum-пара не нужна и не вызывается.
Ошибка: пара StatusCarrier/ApiStatus, фактический DomainStatus-вход. Default Source
предупреждает о Suspended/Corrupt/Legacy. Explicit: Active бросает, Cancelled → Deleted; Auto
возвращает конвенцию. Struct с таким свойством эквивалентен.

### 171. Tuple выбирает enum-вход и учитывает дополнительное условие

```csharp
builder.Map<(DomainStatus Status, bool IsArchived), ApiStatus>()
    .Members(source => source.Status switch
    {
        _ when source.IsArchived => ApiStatus.Archived,
        DomainStatus.Cancelled => ApiStatus.Deleted
    });
```

| Tuple | Create и Update |
|---|---|
| `(Active, false)` | Active |
| `(Cancelled, false)` | Deleted |
| `(Legacy, false)` | EnumMappingException |
| `(Active, true)` или `(Legacy, true)` | Archived: специальная ветка до конвенции |

Вход — Status, guard — IsArchived; unnamed/System.Tuple используют Item1/Item2. Вариант (int
Code, bool IsArchived), Code switch с тем же guard и Auto вместо Cancelled: (20,false) →
Active, (30,false) → Deleted, (2,false) бросает, (2,true) → Archived; default ByValue.

### 172. Whole-object patterns возвращают enum без выбора скрытого входа

Используется StatusDetails из сценария 134:

```csharp
builder.Map<StatusDetails, ApiStatus>()
    .Members(source => source switch
    {
        { IsBlocked: true } => ApiStatus.Disabled,
        { Status: DomainStatus.Cancelled } => ApiStatus.Deleted,
        _ => ApiStatus.Unknown
    });
```

| Status / IsBlocked | Результат |
|---|---|
| Active / false | Unknown: у целого объекта нет enum-конвенции |
| Cancelled / false | Deleted |
| Cancelled / true | Disabled: первая специальная ветка |
| Legacy / false | Unknown |

Без fallback Active/Legacy при IsBlocked=false бросают; Auto вместо fallback диагностируется.
Property/whole-tuple patterns сами допустимы.

### 173. IncludeBase композиционно дополняет object → enum

Базовая конфигурация:

```csharp
builder.Map<StatusCarrier, ApiStatus>()
    .Members(source => source.Status switch
    {
        DomainStatus.Suspended => ApiStatus.Disabled,
        _ => ApiStatus.Unknown
    });
```

В текущем mapper:

```csharp
base.Configure(builder);
builder.Map<StatusCarrier, ApiStatus>()
    .IncludeBase<StatusCarrier, ApiStatus>()
    .Members(source => source.Status switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted
    });
```

| Source.Status | Результат |
|---|---|
| Cancelled | Deleted по локальному правилу |
| Suspended | Disabled по базовому правилу |
| Active | Active по конвенции после специальных веток |
| Legacy | Unknown по унаследованному fallback |

Два source.Status — независимые чтения одной включённой пары; базовое лениво и не выполняется
для локального Cancelled. Общий inherited fallback закрывает Source coverage всей композиции.

### 174. Nullable object, previous, result и Operation сохраняют lifecycle

```csharp
builder.Map<StatusCarrier?, ApiStatus?>()
    .ResolveUsing((source, previous) => ApiStatus.Pending)
    .Members((source, previous, result, context) =>
    {
        if (context.Operation == MappingOperation.Update &&
            previous.TryGetValue(out var old))
            return old;

        return source.Status switch
        {
            DomainStatus.Cancelled => ApiStatus.Deleted,
            _ => result
        };
    });
```

| Операция / source / previous | Результат |
|---|---|
| Create / Active | Active по конвенции, а не фабричный Pending |
| Create / Legacy | Pending через fallback |
| Create / Cancelled | Deleted |
| Update / Cancelled / Archived | Archived через явный return old |
| Update / Cancelled / null | Deleted; Operation остаётся Update |
| Update / Legacy / null | Pending через fallback |
| Create / null | null, фабрика и Members пропущены |
| Update / null / Archived | null по default ReturnNull; фабрика и Members пропущены |

ResolveUsing вызывается один раз для non-null source даже при return old. Ignore вместо result
сохраняет таблицу; без Using такое чтение/Ignore на Create диагностируется.

### 175. Объект передаёт полную flags-маску явному nested Map

Используются SourceAccess и TargetAccess из раздела «Flags между enum».

```csharp
class AccessCarrier
{
    public SourceAccess Permissions { get; set; }
}

builder.Map<SourceAccess, TargetAccess>()
    .Members(flag => flag switch
    {
        SourceAccess.Read => TargetAccess.View,
        SourceAccess.Write => TargetAccess.Edit,
        _ => TargetAccess.Unknown
    });
builder.Map<AccessCarrier, TargetAccess>()
    .Members(source => Map<TargetAccess>(source.Permissions));
```

| Permissions | Create и Update с previous = Unknown |
|---|---|
| None | None = 0 |
| ReadWrite | View \| Edit = 48 |
| Read \| Audit | View \| Audit = 80 |
| Read \| Delete | View \| Unknown = 144 |

Permissions читается один раз; полную маску обрабатывает nested ByBit-пара со своей coverage.
Adaptive Map использует доступность result, явные Create/Update — свою операцию. Прямая сборка
маски — 123.

## Coverage выбранного enum-входа

Контракт — в [дизайне](ENUM_MAPPING_DESIGN.md#проверка-покрытия).
Используются CoverageSource { Active = 1, Cancelled = 2, New = 3 } и
CoverageTarget { Active = 10, Deleted = 20, Internal = 30 } из сценария 97.
Диагностики относятся к конфигурации и не зависят от фактического runtime-вызова.

### 176. Source проверяет enum-свойство и сохраняет обычные правила покрытия

```csharp
class CoverageEnvelope
{
    public CoverageSource Status { get; set; }
    public string Note { get; set; } = "";
}

builder.Map<CoverageEnvelope, CoverageTarget>()
    .Members(source => source.Status switch
    {
        CoverageSource.Cancelled => CoverageTarget.Deleted
    });
```

| Status | Runtime |
|---|---|
| Active | Active = 10 по конвенции |
| Cancelled | Deleted = 20 по явному правилу |
| New | EnumMappingException |
| `(CoverageSource)123` | EnumMappingException; неназванное число не входит в coverage |

| UnmappedEnumMemberValidation | Warnings |
|---|---|
| Не задан / Source | CoverageSource.New не обработан |
| Destination | CoverageTarget.Internal не используется |
| Strict | New и Internal |
| None | Нет coverage warnings; runtime тот же |

Source проверяет объявления CoverageSource, хотя TSource пары — CoverageEnvelope.
Note не анализируется этой enum-проверкой. Для struct или tuple с явно выбранным
элементом CoverageSource результаты проверки те же.

Каждая строка ниже — отдельное дополнение к исходному switch при default Source:

| Добавленная ветка | Source coverage New |
|---|---|
| `CoverageSource.New => CoverageTarget.Internal` | Закрыт |
| `CoverageSource.New when IsEnabled() => CoverageTarget.Internal` | Warning: false-путь не обработан |
| `_ => CoverageTarget.Internal` | Закрыт обычным fallback |
| `_ => throw new InvalidOperationException()` | Закрыт явным запретом |
| `_ => Auto()` | Warning: у New нет конвенционного соответствия |

Inherited общий fallback также закрывает coverage всей композиции, не отдельных неполных
Members (173).

### 177. Local и Normalize не сужают набор объявлений enum для проверки

Normalize всегда возвращает CoverageSource.Active и считает вызовы:

```csharp
builder.Map<CoverageEnvelope, CoverageTarget>()
    .Members(source =>
    {
        var status = Normalize(source.Status);
        return status switch
        {
            CoverageSource.Cancelled => CoverageTarget.Deleted
        };
    });
```

Каждая строка — отдельный вариант того же Members при source.Status = New:

| Выбор входа switch | Runtime | Default Source coverage |
|---|---|---|
| `source.Status switch` | Исключение | Warning для New |
| `var status = source.Status; return status switch ...` | Исключение | Warning для New |
| `Normalize(source.Status) switch` | Active | Warning для New |
| Normalize через local, как выше | Active | Warning для New |

Normalize один раз; проверяются все объявления его return type без анализа тела и обещания
достижимости New. Fallback закрывает coverage, None отключает; фактический Active сохраняется.
Это относится и к enum-input метода внутри string/integer-регистрации; сами string/integer
следуют 169.

### 178. Whole-object switch и nested Map не запускают скрытую проверку свойств

Whole-object вариант:

```csharp
builder.Map<CoverageEnvelope, CoverageTarget>()
    .Members(source => source switch
    {
        { Status: CoverageSource.Cancelled } => CoverageTarget.Deleted,
        _ => CoverageTarget.Internal
    });
```

Default Source не перебирает Status: вход switch — CoverageEnvelope.
New возвращает Internal; прямое enum-значение справа от ветки не создаёт
дополнительной Source-проверки. Без fallback New бросит, но warning о
CoverageSource.New не появится: enum-свойства whole-object patterns не сканируются.
Для whole-tuple switch действует та же граница.

Отдельный вариант с зарегистрированной enum-парой:

```csharp
builder.Map<CoverageSource, CoverageTarget>()
    .UnmappedEnumMemberValidation(UnmappedMemberValidation.Source)
    .Members(value => value switch
    {
        CoverageSource.Cancelled => CoverageTarget.Deleted
    });
builder.Map<CoverageEnvelope, CoverageTarget>()
    .UnmappedEnumMemberValidation(UnmappedMemberValidation.None)
    .Members(source => Map<CoverageTarget>(source.Status));
```

Warning New принадлежит вложенной паре; внешний None не отключает и не дублирует его.
Вложенный None уберёт warning, но New продолжит бросать.

## Flags-конвенция для выбранного входа

Контракт — в [дизайне](ENUM_MAPPING_DESIGN.md#flagsmappingmode-и-выбранный-вход).
Общие типы:

```csharp
[Flags]
enum RequestAccess
{
    None = 0, Read = 1, Write = 2, Legacy = 4, ReadWrite = Read | Write
}

[Flags]
enum ApiAccess { None = 0, Read = 8, Write = 16, Special = 32, Unknown = 64 }

class AccessRequest
{
    public RequestAccess Access { get; set; }
    public string Text { get; set; } = "";
}
```

### 179. Маска из объекта: режим конвенции и общий fallback

```csharp
builder.Map<AccessRequest, ApiAccess>()
    .Members(request => request.Access switch { _ => ApiAccess.Unknown });
```

| Access | Default ByBit | Добавить FlagsMappingMode.ByMask |
|---|---|---|
| None | None = 0 | None = 0 |
| Read | Read = 8 | Read = 8 |
| ReadWrite = 3 | Read \| Write = 24 | Unknown = 64: у destination нет имени ReadWrite |
| Read \| Legacy = 5 | Unknown = 64 | Unknown = 64 |

Один внешний проход/чтение Access, одинаково Create и Update с Special. Read | Legacy: весь
fallback 64, не 72. Без fallback/с Auto неуспех бросает. Explicit + обычный fallback даёт
Unknown с допустимым неактивным mode; Auto (включая typed) возвращает конвенцию. Tuple
эквивалентен.

### 180. Whole-mask override не становится правилом для каждого бита

```csharp
builder.Map<AccessRequest, ApiAccess>()
    .Members(request => request.Access switch
    {
        RequestAccess.Read => ApiAccess.Special,
        _ => ApiAccess.Unknown
    });
```

Для сравнения отдельная конфигурация регистрирует RequestAccess → ApiAccess
с теми же ветками над flag и вызывает её через
`.Members(request => Map<ApiAccess>(request.Access))`:

| Access | Whole-mask switch выше | Явный nested Map в flags-пару с ByBit |
|---|---|---|
| Read | Special = 32 | Special = 32 |
| Write | Write = 16 | Write = 16 |
| ReadWrite | Read \| Write = 24 | Special \| Write = 48 |
| Read \| Legacy | Unknown = 64 | Special \| Unknown = 96 |

Whole-mask Read-ветка не применяется внутри комбинации; nested ByBit применяет её к каждому
биту. Добавление ReadWrite => Special даёт Special в обоих режимах без warning недостижимости;
Read | Legacy остаётся Unknown.

### 181. Строка из объекта разбирается целиком без повторения Members

```csharp
builder.Map<AccessRequest, ApiAccess>()
    .Members(request => request.Text switch
    {
        "Read" => ApiAccess.Special,
        _ => ApiAccess.Unknown
    });
```

| Text | Результат при ByBit и ByMask |
|---|---|
| `"Read"` | Special = 32 по whole-string ветке |
| `"Read, Write"` | Read \| Write = 24 по встроенному разбору |
| `" Read ; Write "` | Read \| Write = 24; trim выполняет конвенция |
| `"Read,,Write;"` | Read \| Write = 24; пустые токены дают 0 |
| `"Read\|Missing"` | Unknown = 64, а не Read \| Unknown |
| `""`, `"   "`, `"0"` | None = 0 |
| `"8"` | Unknown: произвольный numeric parsing не включён |

Text читается один раз; Read-ветка не применяется к токенам списка. В обоих режимах
неизвестный токен отменяет весь разбор, fallback один раз. Для правил на токенах нужен nested
string → flags ByBit. Explicit + обычный fallback даёт Unknown для списка/пустоты с допустимым
mode; Auto включает разбор и бросает для "Read|Missing".

### 182. Ignore сохраняет начальный result внешнего scalar-прохода

```csharp
builder.Map<AccessRequest, ApiAccess>()
    .FlagsMappingMode(FlagsMappingMode.ByBit)
    .ConstructUsing(request => ApiAccess.Special)
    .Members(request => request.Access switch { _ => Ignore() });
```

| Access / mode | Create | Update с previous = Unknown |
|---|---|---|
| Read / ByBit | Read = 8 | Read = 8 |
| ReadWrite / ByBit | Read \| Write = 24 | Read \| Write = 24 |
| Read \| Legacy / ByBit | Special = 32 | Unknown = 64 |
| ReadWrite / ByMask | Special = 32 | Unknown = 64 |

Ignore/typed Ignore сохраняет initial result без частичного Read. Фабрика: один раз на Create,
пропуск на non-null Update. Explicit сохраняет result для всех входов, mode допустим
неактивным.

Без фабрики Create диагностируется; Update-only non-nullable допускает previous, включая 0.
Nullable: non-null Special сохраняется; null Update destination вызывает фабрику при policy
Create, операция Update. Прямой Members(_ => Ignore()) без mode сохраняет result без
конвенции.

### 183. Режим применим к выбранному входу и наследуется обычным способом

База регистрирует AccessRequest → ApiAccess с ByMask и Members из сценария 179.
Текущий mapper после base.Configure включает ту же пару:

```csharp
builder.Map<AccessRequest, ApiAccess>()
    .IncludeBase<AccessRequest, ApiAccess>()
    .FlagsMappingMode(FlagsMappingMode.ByBit);
```

| Локальная настройка | Access = ReadWrite |
|---|---|
| ByBit, как выше | 24: применяется к унаследованному входу request.Access |
| Default или отсутствует | 64: inherited ByMask |
| ByMask | 64 |

Если базовый fallback заменить на Auto, ByBit по-прежнему даёт 24, а ByMask
бросает вместо 64. Inherited Auto использует собственный вход и effective mode
текущей пары; дополнительных проходов Members не появляется.

Отдельные варианты без IncludeBase:

| Конфигурация AccessRequest → ApiAccess | Применимость FlagsMappingMode.ByBit |
|---|---|
| `request.Access switch { _ => Auto() }` | Применима к flags-входу |
| `request.Text switch { _ => Auto() }` | Применима к string → flags конвенции |
| `((int)request.Access) switch { _ => Auto() }` | Явная pair-настройка даёт MORPH0023; общий default игнорируется, число переводится целиком |
| Прямой `ApiAccess.Special`, либо Using без Members | Явная pair-настройка даёт MORPH0023: нет применимого исходного прохода или flags-конвенции |
| Один путь Members выбирает flags-вход, другой — int-вход | Режим применим к flags-пути; int переводится целиком |

У исходной flags-пары mode применим к проходам даже при выбранном int-входе Auto или
отсутствии конвенции (157).

### 184. Coverage не переносит whole-mask overrides на биты комбинации

```csharp
[Flags]
enum RenamedAccess { None = 0, View = 8, Write = 16 }

builder.Map<AccessRequest, RenamedAccess>()
    .Members(request => request.Access switch
    {
        RequestAccess.Read => RenamedAccess.View
    });
```

| Access | Runtime при default ByBit | Source coverage |
|---|---|---|
| None | 0 | Покрыт нулевой конвенцией |
| Read | View = 8 | Покрыт явным результатом |
| Write | Write = 16 | Покрыт конвенцией |
| Legacy | Исключение | Warning для Legacy |
| ReadWrite | Исключение | Warning для объявленного composite ReadWrite |

ReadWrite не совпадает с Read-веткой, конвенция не знает Read в RenamedAccess. Fallback None
закрывает оба warnings; явная ReadWrite-ветка — только ReadWrite. Nested ByBit-пара даёт
ReadWrite → View | Write = 24, предупреждая только о Legacy.

## Null выбранного входа конвенции

Контракт — в [дизайне](ENUM_MAPPING_DESIGN.md#null-выбранного-входа).
DomainStatus/ApiStatus заданы в начале каталога; RequestAccess/ApiAccess —
в сценариях 179–184. Общий объект следующих примеров:

```csharp
class NullableInputRequest
{
    public DomainStatus? Status { get; set; }
    public string? Text { get; set; }
    public int? Code { get; set; }
    public RequestAccess? Access { get; set; }
}
```

### 185. Null объекта и null его свойства имеют разные точки обработки

```csharp
builder.Map<NullableInputRequest?, ApiStatus?>()
    .Members(request => request.Status switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted,
        _ => ApiStatus.Unknown
    });
```

Меняется только NullSourceHandling этой пары:

| Вход / операция | Default ReturnNull | ReturnDestination | Throw |
|---|---|---|---|
| Create, request = null | null | null | NullSourceException |
| Update, request = null, previous = Archived | null | Archived | NullSourceException |
| Create, request не null, Status = null | Non-null Unknown = 0 | Non-null Unknown = 0 | Non-null Unknown = 0 |
| Update, request не null, Status = null, previous = Archived | Non-null Unknown = 0 | Non-null Unknown = 0 | Non-null Unknown = 0 |
| Non-null request, Status = Active | Active = 20 | Active = 20 | Active = 20 |

Null объекта пропускает Members. Null свойства один раз доходит до fallback, без повторной
policy, NullReferenceException или неявного Nullable.Value.

### 186. Явный null-result, обычный fallback и Auto

Каждая строка — отдельное изменение Members из сценария 185 при non-null request
и Status = null:

| Изменение | Результат |
|---|---|
| Нет изменений | Non-null Unknown через fallback |
| Добавить `null => null` | null как явный результат правила |
| Добавить `null => ApiStatus.Archived` | Archived |
| Добавить `null => throw new InvalidOperationException()` | Пользовательское исключение без оборачивания |
| Добавить `null => Auto()` при сохранённом `_ => Unknown` | EnumMappingException немедленно; обычный fallback не достигается |
| Заменить fallback на `_ => Auto()` или `_ => Auto<ApiStatus?>()` | EnumMappingException |
| Убрать fallback, оставив Cancelled-ветку | EnumMappingException |
| Добавить MemberSelection.Explicit к исходному варианту | Unknown без попытки конвенции |

Ошибка сообщает null/DomainStatus?, исходную пару и операцию без повторного чтения.
Non-nullable destination также не получает default: отсутствие результата бросает, null-result
недопустим. Coverage проверяет DomainStatus, не null; C# проверки сохраняются.

### 187. Nullable-элемент tuple и результат Normalize сохраняют эффекты

Tuple-вариант:

```csharp
builder.Map<(DomainStatus? Status, bool PreserveNull), ApiStatus?>()
    .Members(source => source.Status switch
    {
        null when source.PreserveNull => null,
        _ => ApiStatus.Unknown
    });
```

| Tuple | Результат |
|---|---|
| `(null, true)` | null по явной ветке |
| `(null, false)` | Non-null Unknown через fallback |
| `(Active, true)` или `(Active, false)` | Active по конвенции |

Для System.Tuple<DomainStatus?, bool> выбирается Item1 и guard читает Item2.
Null самого ссылочного tuple обрабатывается общей policy до Members.

Отдельная конфигурация: Normalize возвращает DomainStatus? со значением null;
CreateInitial имеет return type ApiStatus? и возвращает ApiStatus.Pending.
Оба метода принимают DomainStatus и записывают свои вызовы:

```csharp
builder.Map<DomainStatus, ApiStatus?>()
    .ConstructUsing(CreateInitial)
    .Members(status => Normalize(status) switch { _ => ApiStatus.Unknown });
```

| Операция / вариант | Результат | Вызовы по порядку |
|---|---|---|
| Create с Active | Non-null Unknown | CreateInitial, Normalize |
| Update с Active, previous = Archived | Non-null Unknown | Normalize |
| Create, заменить fallback на Auto | EnumMappingException | CreateInitial, Normalize |
| CreateInitial вернул null | null, Members пропущен | Только CreateInitial |
| Create, Normalize бросил пользовательское исключение | То же исключение; fallback не выполняется | CreateInitial, Normalize |

Каждый вызов — один раз, без повторных policies/фабрики; local сохраняет результат. В string →
ApiStatus `text = null!; return Auto();` бросает для текущего null. Явные .Value/.Trim()
сохраняют собственные исключения даже с fallback.

### 188. Null выбранной строки, числа или flags не означает нулевое значение

Каждая строка задаёт отдельный вариант пары NullableInputRequest → ApiAccess?
с Members вида `request => <вход> switch { _ => ApiAccess.Unknown }`:

| Вход switch | Значение | Результат |
|---|---|---|
| request.Text | null | Non-null Unknown = 64 |
| request.Text | `""`, `"   "` или `"0"` | Non-null None = 0 |
| request.Text | `"Read, Write"` | Read \| Write = 24 |
| request.Code | null | Non-null Unknown = 64 |
| request.Code | 0 | Non-null None = 0 |
| request.Code | 8 | Read = 8 по строгому ByValue |
| request.Access | null | Non-null Unknown = 64 |
| request.Access | None = 0 | Non-null None = 0 |
| request.Access | ReadWrite | Read \| Write = 24 при default ByBit |

Auto вместо fallback бросает для всех трёх null-входов. String/flags null неуспешен и при
ByMask; non-null nullable input сохраняет обычную стратегию.

### 189. Null вычисленного входа ByBit отличается от возвращённого null

NormalizeFlag записывает исходный RequestAccess-бит и возвращает RequestAccess?:
для Write — null, для остальных значений — переданное значение.

```csharp
builder.Map<RequestAccess, ApiAccess?>()
    .Members(flag => NormalizeFlag(flag) switch { _ => ApiAccess.Unknown });
```

| Source / вариант Members | Результат | Вызовы NormalizeFlag |
|---|---|---|
| Write, как выше | Non-null Unknown = 64 | Write |
| ReadWrite, как выше | Read \| Unknown = 72 | Read, Write |
| Read \| Write \| Legacy, как выше | Read \| Unknown = 72 | Read, Write, Legacy |
| Read \| Write \| Legacy, добавить `null => null` | null: выбранный результат завершает операцию | Read, Write; Legacy не достигается |
| Read \| Write \| Legacy, заменить fallback на Auto | EnumMappingException; частичный Read не возвращается | Read, Write |
| Read \| Write \| Legacy, заменить fallback на Ignore() | Non-null Read = 8 | Read, Write, Legacy |

Null входа передаёт управление fallback текущего бита; Ignore даёт 0 без фабрики. Null-result
завершает операцию; effects сохраняются, результат Unknown повторно не обрабатывается.

## Совместимые настройки и aliases

### 190. Явная стратегия может подтверждать стандартное поведение

Каждая строка — отдельная регистрация; используются DomainStatus и WireCode.

| Пара | Явная EnumMappingStrategy | Вход → результат |
|---|---|---|
| string → DomainStatus | ByName | `"active"` → Active; `"2"` → EnumMappingException |
| WireCode → byte | ByValue | Active → 1; `(WireCode)42` → 42; Large = 300 → EnumMappingException |
| WireCode → byte | ByValueAllowUndefined | Те же результаты, включая проверку диапазона |
| string → DomainStatus | ByValue / ByValueAllowUndefined | MORPH0023; numeric parsing не добавляется |
| WireCode → byte | ByName | MORPH0023 |

Первые три строки равнозначны отсутствию настройки. Обычный fallback обрабатывает неуспех;
Auto бросает. Выбранный вход/inherited-настройка следуют тому же правилу.

### 191. Explicit сохраняет совместимую настройку неактивной

```csharp
builder.Map<AccessRequest, ApiAccess>()
    .FlagsMappingMode(FlagsMappingMode.ByBit)
    .MemberSelection(MemberSelection.Explicit)
    .Members(request => request.Access switch { _ => ApiAccess.Unknown });
```

| Вариант | Результат для Access = ReadWrite |
|---|---|
| Как выше | Unknown = 64; MORPH0023 нет |
| Заменить ByBit на ByMask | Unknown = 64; настройка также неактивна |
| Удалить Explicit, сохранив ByBit | Read \| Write = 24 |
| Сохранить Explicit и заменить fallback на Auto | 24 с ByBit; EnumMappingException с ByMask |
| Настройка ByBit и Members унаследованы; локально только Explicit | Unknown = 64; удалять настройку базы не требуется |

CoverageEnvelope → CoverageTarget: ByName + Explicit + Status switch с fallback Internal
допустимы, итог Internal. С Auto: Active → Active, New бросает.

Без автопутей достаточно исходной пары либо выбранного статического входа; реальные Auto
должны быть совместимы все (ByName для int Auto — MORPH0023). Прямой результат/Using object →
enum без подходящего входа не делает настройку применимой; исходные flags-пары сохраняют свой
режим.

### 192. Точные совпадения выбираются для всей группы aliases

Все source-имена в строке имеют число 1; меняются только объявления enum.
Регистрация без Members, default ByName:

| Source aliases | Destination | Результат для 1 |
|---|---|---|
| Ready, ready | Ready = 10, READY = 20 | 10; точное Ready исключает ignore-case этап |
| Ready, ACTIVE | Ready = 10, Active = 20 | 10; ignore-case ACTIVE не конкурирует с exact Ready |
| Ready, READY | Ready = 10, READY = 20 | Неоднозначность двух точных чисел |
| ready, rEaDy | Ready = 10, READY = 20 | Неоднозначность ignore-case этапа: точных нет |
| Ready, Active | Ready = 10, Active = 10 | 10; несколько точных имён дают одно число |
| Missing, ready | Ready = 10 | 10; отсутствие соответствия для Missing не отменяет успех |

Порядок объявлений не влияет. Неоднозначность даёт Warning и неуспех: fallback — свой
результат, отсутствие/Auto — EnumMappingException. Явное правило 1 разрешает всю группу. Enum
→ string с aliases по-прежнему требует canonical output.

## Статус проверки каталога

Примеры сверены с согласованным дизайном; нумерация нужна для обсуждения конкретных
случаев и последующего переноса в тесты. Они не заменяют проверки production
генератора, реальной типизации Members, generated code и Rider. Новые ID
enum-диагностик пока не назначены; согласованные уровни указаны в соответствующих
сценариях. Тип EnumMappingException и требования к его сообщению согласованы;
его runtime-реализации пока нет.
