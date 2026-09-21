# First-class enum mapping: исследование и набросок дизайна

Дата: 2026-09-21. Статус: предложение для обсуждения, не принятый контракт и
не обещание реализации. Редакция 2: switch-based DSL по замечанию пользователя.
Названия нового API предварительные; fluent rules первого наброска заменены.
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

## 3. Основной принцип: пользователь пишет C#

Пользователь подтвердил направление feature, но отклонил таблицу из fluent
`MapValue`/`FallbackValue`/`RejectValue`: Morphant должен приближать описание
mapping к естественному C#. Эта редакция заменяет такую таблицу декларативным
callback `Values`. Его имя ещё обсуждается; направление на switch принято.

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Values(status => status switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted,
        DomainStatus.Archived  => ApiStatus.Hidden,
        DomainStatus.Paused    => ApiStatus.OnHold,
        _ => Auto(fallback: ApiStatus.Unknown)
    });
```

Десять overrides — десять обычных switch arms внутри одного выражения.
Несколько source-значений объединяются через `or`; исключение пишется через
`throw`. Значения справа могут быть вычисляемыми, а не только константами:

```csharp
.Values(status => status switch
{
    DomainStatus.New or DomainStatus.Pending => ApiStatus.Pending,
    DomainStatus.Active when UseLegacyCode() => GetLegacyStatus(),
    DomainStatus.Corrupt => throw new InvalidOperationException(),
    _ => Auto()
});
```

Сохранять порядок arms, short-circuit guards, пользовательские вычисления и
исключения. Нельзя извлекать из такого switch плоскую таблицу и затем
переставлять cases, выполнять выражения заранее или применять одну ветку
несколько раз. Обычная семантика switch описана в
[C# reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/switch-expression).

`Values` — декларативная форма рядом с `Members`, а не новая семантика
существующего `Convert`. Последний остаётся ordinary C# callback с текущим
контрактом. `Construct` не подходит: его существующая семантика ограничена
созданием при отсутствии destination, тогда как значение enum нужно
вычислять и при Update.

## 4. Конвенция, fallback и explicit mapping

В отсутствие `Values` применяется обычный enum algorithm. Внутри callback
он вызывается только там, где пользователь написал `Auto()`.

| Написанный код | Смысл |
|---|---|
| `_ => Auto()` | Применить конвенцию к текущему входу; отсутствие соответствия — mapping exception |
| `_ => Auto(fallback: ApiStatus.Unknown)` | Применить конвенцию, а при отсутствии соответствия вернуть указанное значение |
| `_ => ApiStatus.Unknown` | Любой оставшийся вход сразу преобразовать в `Unknown`; конвенция здесь не запускается |
| `DomainStatus.Corrupt => throw new ...` | Явно запретить этот случай обычным C# |
| Switch без `Auto()` | Полностью явный алгоритм; дополнительный режим `Explicit` не нужен |

Поэтому прежние fluent methods для одного значения, fallback и reject
не входят в предлагаемый API. Отдельный `UnknownEnumValueHandling`,
дублирующий эти возможности, также не нужен.

### Политики остаются небольшими настройками

Для разных enum предлагается `ByName` с точным ordinal-сравнением имён.
`Active = 1` может соответствовать `Active = 10`. Для числового контракта
остаётся `ByValue` с проверкой destination:

```csharp
builder.Map<WireStatus, StorageStatus>()
    .Values(status => Auto(strategy: EnumMappingStrategy.ByValue));

builder.Map<WireCode, StoredCode>()
    .Values(code => Auto(
        strategy: EnumMappingStrategy.ByValue,
        validation: EnumValueValidation.None));
```

`ByValue` сохраняет математическое число; `Defined` проверяет destination,
`None` разрешает неназванные destination-значения. Диапазон underlying type
проверяется в обоих случаях: без усечения и зависимости от checked options
consumer. Переполнение не поглощается fallback. Неизвестное в source число
может пройти `Defined`, если оно допустимо в destination.

Общие strategy/validation defaults могут задаваться на mapper/assembly и
следуют текущей precedence. Named arguments у `Auto` локально уточняют их.
Большой switch не обрастает вызовами настройки для каждого значения.
`Default` продолжает цепочку настроек; `Explicit` в новой модели не нужен.

`Auto()` относится к текущему значению первого параметра своего callback,
а не к произвольному governing expression ближайшего вложенного switch.
Для `Values` это целое значение, для `Flags` — отдельный бит. Эта привязка
должна быть явной частью контракта; нельзя угадывать её по форме выражения.
Настройки автоматического mapping не изменяют явно возвращаемый результат.

### Вычисляемый fallback

Значение-константа пишется коротко. Для вычисления fallback предлагается
явная lazy-форма:

```csharp
.Values(status => status switch
{
    DomainStatus.Cancelled => ApiStatus.Deleted,
    _ => Auto(fallback: () => ResolveUnknownStatus(status))
});
```

Callback fallback выполняется только при неудаче конвенции, один раз.
Перегрузка с непосредственным значением принимает compile-time constant;
вычисляемый аргумент требует lambda. Это не даёт незаметно поменять обычную
eager-семантику аргументов C# на lazy. Fallback не перехватывает исключения
пользовательских guards, expressions, inherited callbacks или своего тела.

## 5. Полнота: проверять запрос пользователя

Сохраняется независимая `UnmappedEnumValueValidation`:
`Default / None / Source / Destination / Strict`. Предлагаемый default для
разных enum — `Source`, с error при непокрытом объявленном значении. Object
member validation и её default `None` не меняются.

Однако естественный switch требует различать два намерения:

```csharp
// Остаток должен иметь соответствие по конвенции.
.Values(status => status switch
{
    DomainStatus.Cancelled => ApiStatus.Deleted,
    _ => Auto(fallback: ApiStatus.Unknown)
});

// Остаток намеренно сводится к одному значению.
.Values(status => status switch
{
    DomainStatus.Active => ApiStatus.Active,
    _ => ApiStatus.Unknown
});
```

В первом случае добавленный `DomainStatus.Paused` без соответствия вызывает
diagnostic даже при fallback: запрос конвенционного покрытия не выполнен.
Во втором случае catch-all уже явно обрабатывает новый вариант. Ошибка
неполноты исказила бы пользовательский C#. Аналогично `_ => throw ...`
явно запрещает весь остаток. Это уточнение первого наброска, а не потеря
строгого режима: выбор виден непосредственно в switch.

`None` по-прежнему позволяет намеренно разрешить пробелы конвенции;
runtime fallback/throw при этом остаётся. Компиляторские предупреждения о
неполном C# switch не заменять и не подавлять этой настройкой. Не вставлять
неявный `Auto()` в switch без последней ветки.

### Guards и вычисляемые результаты

`Source.A when Check()` не закрывает `A` целиком: путь с false должен быть
обработан следующими ветками или конвенцией. Генератор не выполняет
пользовательский код при компиляции и не предполагает, что guard всегда true.
Проверять возможные пути для конечного набора enum-значений, а не просто
собирать упоминания имён слева от `=>`.

Для source coverage любой явный результат или throw обрабатывает выбранный
путь, включая вызов метода справа. Для destination coverage произвольный
вызов метода не доказывает набор возможных результатов. Если запрошенные
`Destination`/`Strict` нельзя подтвердить, нужен diagnostic «покрытие не
может быть доказано», а не ложное утверждение о конкретном unmapped member.
Пользователь может выбрать `Source`/`None` либо сделать результаты явными.
Такая же граница нужна для сложных guards и изменённых входных значений,
когда точность анализа недостаточна. Не строить интерпретатор C# ради proof.

Строки и числа не объявляются полностью проверенными: coverage относится
только к конечной enum-стороне. Неназванные enum-числа покрывает runtime
policy. При aliases проверяется одно физическое значение.

## 6. Flags: два явно названных уровня

Обычная ветка `SourceAccess.Read => ...` совпадает с целым `Read`, но не с
`Read | Write`. Нельзя сделать вид, что пользовательский switch является
таблицей перевода всех содержащихся в значении флагов.

Предлагается отдельная форма `Flags`, обозначающая перевод каждого
установленного одноразрядного бита:

```csharp
builder.Map<SourceAccess, TargetAccess>()
    .Flags(flag => flag switch
    {
        SourceAccess.Read   => TargetAccess.View,
        SourceAccess.Write  => TargetAccess.Edit,
        SourceAccess.Delete => TargetAccess.None,
        _ => Auto()
    });
```

`Read | Write` переводится как `View | Edit`. Отображение `Delete` в ноль —
явное намерение пользователя убрать этот флаг. `or` pattern объединяет
альтернативные single-bit случаи; он не проверяет присутствие маски.

Whole-value overrides и whole-value fallback остаются обычным `Values`:

```csharp
builder.Map<SourceAccess, TargetAccess>()
    .Values(value => value switch
    {
        SourceAccess.All => TargetAccess.All,
        SourceAccess.LegacyReadWrite => TargetAccess.Compatibility,
        _ => Auto(fallback: TargetAccess.Unknown)
    })
    .Flags(flag => flag switch
    {
        SourceAccess.Read  => TargetAccess.View,
        SourceAccess.Write => TargetAccess.Edit,
        _ => Auto()
    });
```

Это два разных алгоритмических уровня, независимо от числа значений в них.
Порядок конфигурационных вызовов не меняет семантику: `Values` обрабатывает
целый вход; достигнутый `Auto` в режиме `ByName` использует `Flags` для битов;
`Auto` внутри `Flags` использует встроенную конвенцию одного бита и не
вызывает тот же callback рекурсивно.

Правила:

1. Whole-value ветки выполняются в написанном C# порядке. Ветка для `All`
   не применяется к совпавшей части большей маски.
2. При декомпозиции каждый установленный объявленный single-bit source
   обрабатывается один раз; результаты объединяются через OR. Порядок —
   возрастание позиции бита, включая signed high bit последним. Это важно
   для вычисляемых результатов и исключений.
3. Неизвестные биты проверяются перед вызовом per-bit callback. Они дают
   failure всего автоматического преобразования; внешний fallback может
   обработать его. Нет молчаливого удаления битов или частичного результата.
4. Для известных битов пользовательские guards/expressions выполняются
   последовательно. Если более поздний бит не удалось перевести, внешняя
   конвенция завершается failure; уже выполненные вычисления не откатываются.
   Пользовательские исключения всегда проходят наружу, не в fallback.
5. Fallback внутри `Flags` относится к одному биту, снаружи — ко всей маске.
   Поэтому пользователь явно выбирает, допустим ли fallback отдельной части.
6. Ноль переводится в ноль без вызова `Flags`. Другой результат для нуля
   задаётся ordinary arm в `Values`, в том числе когда нет имени `None`.
7. Composite declarations — имена целых масок, не дополнительные атомарные
   биты. Они не переопределяют написанный `Flags` callback и не участвуют
   автоматически в сопоставлении по имени. Для особого composite нужен
   `Values`; декомпозируемый composite уже покрыт переводом его битов.
8. Маска разрешённых битов строится из single-bit declarations в точной
   разрядности типа. `All = -1` не разрешает все неизвестные биты; его можно
   обработать явно на уровне `Values`.

Таким образом, вместо особого приоритета composite rules используются
порядок C# и явно разделённые whole-value/per-bit callbacks. Прежняя идея
диагностировать несовпадение одноимённых composites не нужна: их имена не
определяют смысл per-bit преобразования.

Без `Flags` callback две `[Flags]` стороны получают перевод атомарных битов
по имени. При `[Flags]` только с одной стороны `ByName` не угадывает смысл:
нужен explicit `Values`, осознанный `ByValue` или `Convert`.

В `ByValue` сохраняется число без перестановки битов и без вызова `Flags`.
При `Defined` destination допускает ноль, точно объявленное значение или
комбинацию объявленных single-bit values. Одно объявление `Pair = 3` не
разрешает значения `1` и `2`. [Enum.IsDefined](https://learn.microsoft.com/en-us/dotnet/api/system.enum.isdefined?view=net-10.0)
сам по себе недостаточен для проверки комбинаций flags.

## 7. Aliases, строки и числа

```csharp
enum SourceState { Ready = 1, Active = 1 }
enum TargetState { Ready = 10, Active = 20 }
```

Эти source aliases неразличимы в runtime. Если значение достигает `Auto`,
сопоставление имён даёт конфликт и diagnostic. Явная ветка
`SourceState.Ready => TargetState.Active` выбирает результат для числа `1`
и тем самым для обоих имён. Два конфликтующих обычных case для aliases уже
проверяются C# как повторное/недостижимое сопоставление; не обходить его
через генерацию таблицы.

Для enum-to-string одно число требует одного выходного имени. При aliases
его выбирает explicit switch arm; не брать первое поле декларации.
[Enum.GetName](https://learn.microsoft.com/en-us/dotnet/api/system.enum.getname?view=net-10.0)
не гарантирует конкретного имени среди дубликатов.

```csharp
builder.Map<ApiStatus, string>()
    .Values(status => status switch
    {
        ApiStatus.Deleted => "removed",
        _ => Auto()
    });

builder.Map<string, ApiStatus>()
    .Values(text => text switch
    {
        "removed" or "deleted" => ApiStatus.Deleted,
        "pending" or "queued"  => ApiStatus.Pending,
        _ => Auto(fallback: ApiStatus.Unknown)
    });
```

Направления независимы; reverse не угадывает, какое из входных имён должно
стать единственным выходным. Можно пользоваться `when` и обычными
строковыми сравнениями. Встроенный ignore-case остаётся отдельным возможным
расширением с ordinal comparison и проверкой коллизий.

Для enum/string `Auto` использует CLR-имена, для enum/целое число — числа.
Поддерживаемые целые типы: `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`,
`long`, `ulong`. Enum-to-number сохраняет также неназванное значение;
number-to-enum проверяет диапазон и выбранную destination validation.
Вычисляемые explicit expressions остаются обычным C# и не получают скрытой
валидации своего результата.

Числовые строки, пробелы и пустая строка не получают специального смысла
в `Auto`. Их можно явно обработать switch. Автоматический flags/string
parse/format, сторонние wire attributes, naming policies и prefix/suffix
преобразования остаются за границей предлагаемой первой версии, как и в
предыдущем наброске. Explicit `Values` и `Convert` сохраняют такие ручные
сценарии. Числа floating-point, decimal, char, bool и native-sized integers
не добавляются в enum-конвенцию.

## 8. Nullable, операции и composition

`Values` получает non-null source после существующей null policy, как
декларативные construction/member callbacks. Null не смешивается с нулём
или неизвестным enum-числом. Возврат null допустим при nullable destination:

```csharp
builder.Map<DomainStatus?, ApiStatus?>()
    .Values(status => status switch
    {
        DomainStatus.Missing => null,
        DomainStatus.Active => ApiStatus.Active,
        _ => Auto()
    });
```

`NullSourceHandling.ReturnNull` для non-nullable value destination по-прежнему
даёт ноль, независимо от enum-validation. Для пользовательской обработки
самого null source остаётся `Convert`. Nullable-пары сохраняют точную
идентичность регистрации; не искать неявно mapper для underlying типов.

`Create` и `Update` вычисляют результат одним enum algorithm после общих
guards. Для правил, зависящих от previous/context, можно предоставить
перегрузки `Values` по существующей форме declarative callbacks:
`(source, previous)` и `(source, previous, context)`. Их применение не должно
менять смысл `Auto`. Минимальному сценарию достаточно одной source lambda.

Без enum configuration `E -> E` предлагается оставить identity. Explicit
`Values` всегда выполняет написанный код, включая same-type mapping.
Встроенную валидацию identity можно запросить через `Auto(strategy: ...)`.
Этот default, как и остальные defaults feature, ещё требует согласования.

### Inheritance без слияния switch arms

Same-pair `IncludeBase` может наследовать `Values` и `Flags` как целые
callbacks. Local callback заменяет соответствующий inherited callback;
нельзя автоматически вклеить чужие arms в пользовательский switch.
Чтобы сохранить возможность частичного переопределения, предлагается
явный marker `Inherited()`:

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .IncludeBase<DomainStatus, ApiStatus>()
    .Values(status => status switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted,
        _ => Inherited()
    });
```

Пример предполагает same-pair mapping в подключённом base mapper.
`Inherited()` входит в ближайший included enum callback того же уровня:
`Values -> Values`, `Flags -> Flags`; без такого mapping нужен diagnostic.
У базового callback собственная цепочка `Inherited` идёт дальше по base
configuration. `Auto` всегда означает конвенцию, а не скрытый вызов
inherited `Values`. Fallback и guards базового алгоритма сохраняются.
Контекст операции и null guards не запускаются повторно.

Наследуемые defaults следуют общей precedence. На одном уровне допустим
один `Values` и один `Flags`; дубли диагностируются. Конфигурационные вызовы
не задают порядок выполнения. `Convert`/`ConstructUsing`/`ResolveUsing`
остаются самостоятельными algorithms с текущими контрактами; `Inherited`
не превращает их автоматически в enum DSL.

### Вложенное использование

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Values(status => status switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted,
        _ => Auto()
    });

builder.Map<Order, OrderDto>()
    .Members((source, _) => new()
    {
        Status = Map(source.Status)
    });
```

Это сохраняет явный nested mapping. Автоматические enum conversions внутри
`Members.Auto()` остаются отдельным решением о контракте всего Morphant.
Нет изменений `IMapper`, DI, точного выбора пары или get-only value members.

## 9. Типизация и generated code

Временный isolated compiler probe на SDK 10.0.100 с `LangVersion=9`, nullable
и warnings-as-errors подтвердил основную форму DSL. Это проверка C# binding,
не реализованный enum generator и не проверка Rider IntelliSense.

### Важные результаты проверки

- Switch с десятью overrides и typed fallback компилируется.
- `or`, `when`, throw, вызовы методов справа, block lambda и lazy fallback
  совместимы с target-typed декларативным результатом.
- Один generic compile-time result marker с implicit conversions от
  destination, `AutoMarker` и `AutoMarker<T>` позволяет оставить короткий
  `Auto()` без отдельного overload на каждый case. Marker отсутствует в
  итоговом runtime mapping.
- Простой struct result marker ломает natural `null`. Nullable reference
  result marker допускает `null` и target-typed `default`; генератор обязан
  проверять их по destination contract, не по техническому marker type.
  Для non-nullable destination явный null должен диагностироваться.
- При промежуточном `var result = ...` контекст результата lambda не
  распространяется на initializer. Уже существующий generic `Auto<T>()`
  позволяет задать тип, например `_ => Auto<ApiStatus>()`. Не обещать
  компиляцию untyped `var` со смесью enum и bare `Auto()`.
- Для nullable destination generic marker не получает nullable lifting
  автоматически: работает `Auto<ApiStatus?>(fallback: ApiStatus.Unknown)`
  или `Auto(fallback: (ApiStatus?)null)`. Короткий non-null fallback для такой
  пары требует дополнительного API-решения; не скрывать это ограничение.
- Неверный enum/string result и fallback неподходящего типа отвергаются
  компилятором. У nullability и `default` остаётся дополнительная обязанность
  генератора; недостаточно того, что Configure компилируется.

Эти ограничения нужно учесть до реализации. Не заменять типизированный
callback на `object`/`dynamic` ради видимости удобного синтаксиса.

`Values`/`Flags` принимают inline lambdas.
Обычный helper можно вызвать справа от `=>`. Method group и произвольный
imperative algorithm с loops/try относятся к существующему `Convert`;
не требовать анализа тела чужого метода для доказательства покрытия.
Поддержка блока следует существующим declarative statement boundaries.

### Форма реализации

- Переиспользовать доступный анализ declarative control flow и перенос
  выражений. Не считать поддержку нового callback автоматической: у него
  новый scalar result и собственные coverage rules.
- Описание enum из Roslyn включает underlying type, single-bit mask, aliases,
  constants и locations. `ulong` не приводить к signed `long` для удобства.
- Типизированные pair extensions дают `Values`/`Flags`; небольшой marker API
  обеспечивает binding. Не создавать construction/member surfaces для enum.
- Explicit C# сохраняется. `Auto`/`Inherited` заменяются сгенерированными
  выражениями или typed helper calls в своих исходных позициях.
- Не сливать или переставлять пользовательские switch arms. Guards могут
  иметь побочные эффекты; сохранять их условное выполнение после совпадения
  pattern. Не просаживать каждую ветку в local.
- Flags без пользовательского callback могут получать прямые bit operations.
  Произвольный callback применяется один раз на бит в описанном порядке;
  нельзя дублировать его вычисления или подавлять исключения ради fallback.
- Convention failure — отдельный внутренний исход, позволяющий fallback
  без перехвата пользовательских exceptions. Не строить эту семантику на
  catch вокруг всего пользовательского callback.
- Сохранить null/operation guards, typed failure stubs, compatibility manifest,
  incrementality по обоим enum и callbacks, cancellation/recovery.
- Runtime reflection, Enum.Parse/Enum.ToString и boxing в обычном enum
  algorithm не нужны. Open `T : Enum` без известных полей требует Convert
  либо diagnostic, а не runtime introspection.

Пример формы generated algorithm, без interface/null обвязки:

```csharp
return status switch
{
    DomainStatus.Cancelled => ApiStatus.Deleted,
    DomainStatus.Archived => ApiStatus.Hidden,
    _ => __MapStatusByName(status)
};
```

Helper содержит автоматическую таблицу и указанный fallback. Он не вызывает
`Values` заново и не вмешивается в выбор explicit arms. При отсутствии
потребности в helper использовать столь же краткую встроенную форму.

## 10. Предлагаемая граница и дальнейшие решения

Сохраняется функциональный охват: enum-to-enum, aliases, flags, nullable,
enum/integer, ordinary enum/string, unknown values, strict coverage,
explicit rules, inheritance и Create/Update. Улучшения нового подхода —
естественные grouped patterns, guards, throw и вычисляемые результаты.

До реализации согласовать:

1. Имя `Values` и отдельный per-bit callback `Flags`.
2. `Auto(fallback: ...)` с короткой constant и явной lazy-формой.
3. Strict source coverage только на путях, запросивших конвенцию; explicit
   catch-all обрабатывает остаток ровно так, как написано.
4. `Inherited()` для явной композиции base algorithm.
5. Defaults: ByName, source error, identity-пара и explicit nested mapping.
6. Типизацию nullable fallback и приемлемость generic формы в редких местах,
   где C# не выводит destination type.

Этапы возможной реализации:

1. `Values`, Auto/fallback, ordinary enum pairs, typed expressions, source
   coverage, aliases, nullable, inheritance и lifecycle.
2. `Flags`, whole-value/per-bit composition, все underlying types и numeric
   conversions с определённым overflow behavior.
3. Ordinary enum/string с входными aliases и выходной канонизацией.

Будущее покрытие должно проверять поведение и полный generated source:

| Группа | Существенные сценарии |
|---|---|
| C# control flow | Десять overrides; or/when/throw; порядок guards; computed results; block locals; только выбранная ветка |
| Конвенция | Auto, constant/lazy fallback; explicit catch-all; no Auto; неизвестный runtime-вход; новый объявленный member |
| Coverage | Guard true/false paths; недоказуемый result; Source/Destination/Strict; неполный C# switch; finite/infinite sides |
| Flags | Перестановка битов; whole exception; per-bit overrides; zero; удаление бита; неизвестный бит; lazy outer fallback; порядок effects; All=-1; high bit |
| Aliases/strings | Alias conflict только на автоматическом пути; canonical output; несколько входных строк; null/empty/numeric/whitespace |
| Числа/nullability | Восемь underlying types; signed/unsigned; ulong выше long.MaxValue; checked/unchecked consumer; nullable fallback; null/default markers |
| Композиция | Values/Flags/Inherited scopes; отсутствие base; повторы callbacks; Convert/Using; exact nullable pairs; nested constructor/member/tuple |
| Генератор | C# 9; metadata types; current locations; incremental edit имён/чисел/Flags/callbacks; obsolete; cancellation/failure/recovery |

Изменён только внутренний дизайн. Production API, generator и постоянные
тесты feature не реализованы. Сравнительное исследование в разделе 1
сохраняется как источник идей, не как обоснование отвергнутого fluent DSL.
