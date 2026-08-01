# Новый дизайн mapping API Morphant

Статус документа: согласованный рабочий дизайн, зафиксированный перед началом
переработки реализации. Документ описывает целевой API. Текущий код и
`IMPLEMENTATION_PLAN.md` пока отражают прежний `Template()`-дизайн и должны
быть пересмотрены отдельно после проверки этого документа.

## 1. Цель переработки

Текущий `Template()` одновременно описывает создание destination, параметры
конструктора, body-members, работу с существующим destination и полностью
ручной mapping. Из-за этого одна lambda имеет различную семантику в `MapNew` и
`MapExisting`, а `TemplateMode` дополнительно переключает способ её
интерпретации.

Новый API разделяет эти обязанности:

| API | Единственный вопрос |
|---|---|
| `Create` | Как получить result и настроить его constructor parameters? |
| `Members` | Как маппить body-members destination? |
| `MapManually` | Как целиком выполнить mapping без декларативного pipeline? |

Из целевого дизайна удаляются:

- `Template()`;
- `TemplateMode` и разделение на `Dsl` / `Raw`;
- отдельный `SelectResult`;
- отдельный builder для ручного mapping;
- специальный `Skip()` для полного пропуска member mapping;
- служебный `with`-overlay для template-типа.

`Create`, `Members` и `MapManually` не являются тремя последовательными
стадиями одного обязательного pipeline. `Create` и `Members` образуют
декларативный mapping, а `MapManually` является полностью отдельной
альтернативой ему.

## 2. Термины

В документе используются следующие термины:

- `MapNew` — публичная операция `Map(source)`;
- `MapExisting` — публичная операция `Map(source, destination)`;
- `previous` — фактический экземпляр destination, переданный в `MapExisting`;
  в declarative pipeline он формируется после null-предобработки, а в manual
  mapping — непосредственно из исходного аргумента;
- `result` — объект или значение, которое выбрано для применения member rules
  и в итоге возвращается из `Map`;
- `structured creation plan` — сгенерированное описание вызова поддерживаемого
  destination-конструктора либо выбора factory/previous, а не готовый
  `TDestination`; direct `Create` возвращает готовый destination без такого
  промежуточного plan;
- `member plan` — сгенерированное описание body-member mappings, а не готовый
  `TDestination`.

`previous` и `result` намеренно различаются. `Create` может выбрать previous,
создать replacement, получить объект из factory или cache. Поэтому identity
`result` не обязана совпадать с identity переданного destination.

Названия `MapNew` и `MapExisting` описывают форму публичного вызова, а не
гарантию новой или сохранённой identity результата.

## 3. Публичный mapping contract

Базовый runtime-контракт не меняется:

```csharp
public interface IMapper
{
    TDestination Map<TSource, TDestination>(TSource source);

    TDestination Map<TSource, TDestination>(
        TSource source,
        TDestination destination);
}
```

Generated `ITypeMapper<TSource, TDestination>` также продолжает содержать обе
перегрузки с `MappingContext`.

Обе операции остаются в generated contract для любой зарегистрированной пары.
Эффективный `MappingMode` определяет, какая из них поддерживается; вызов
отключённой операции по-прежнему завершается `NotSupportedException`.

Возвращаемое значение `Map` всегда авторитетно:

```csharp
destination = mapper.Map(source, destination);
```

Вызов с проигнорированным результатом потенциально ошибочен, поскольку
declarative и manual mapping могут вернуть replacement:

```csharp
mapper.Map(source, destination);
```

Для такой формы в будущем нужен analyzer warning.

## 4. Общая форма pair-builder

Декларативный mapping выглядит так:

```csharp
builder.Map<Source, Destination>()
    .Create(source => new(
        id: source.Id,
        tenantId: Auto()))
    .Members((source, _) => new()
    {
        Name = source.Name,
        Code = source.Code
    });
```

Полностью ручной mapping регистрируется на том же pair-builder:

```csharp
builder.Map<Source, Destination>()
    .MapManually((source, previous, context) =>
        MapCore(source, previous, context));
```

Для одной canonical mapping-пары разрешён либо декларативный набор
`Create` / `Members`, либо один `MapManually`. Смешивать эти модели нельзя.

## 5. `Previous<TDestination>`

Для представления возможного previous используется отдельная value-type
обёртка, аналогичная `Nullable<T>`:

```csharp
public readonly struct Previous<T>
{
    public bool HasValue { get; }

    public T Value { get; }

    public bool TryGetValue(out T value);
}
```

Точный nullable-контракт `Value` и `TryGetValue` будет выражен подходящими
аннотациями при реализации. Семантически:

- `HasValue == false` означает отсутствие пригодного previous;
- `Value` возвращает previous, если он существует;
- обращение к `Value` при `HasValue == false` ошибочно так же, как у
  `Nullable<T>.Value`;
- сама обёртка не бывает `null`.

В декларативном pipeline `Previous<TDestination>` не хранит `Some(null)`.
Явный `null` destination сначала обрабатывается `NullDestinationHandling`, и
только затем формируется `Previous<TDestination>`.

Та же обёртка передаётся в `MapManually`, но там она формируется из исходного
destination без null-предобработки. Поэтому explicit `null` представлен как
`Previous.None`, а отличие `Map(source, null)` от `Map(source)` сообщает
`MappingContext.Operation`.

## 6. `Create`

### 6.1. Ответственность

`Create` отвечает только за:

- выбор способа получить `result`;
- выбор destination-конструктора;
- mapping constructor parameters;
- convention construction;
- factory construction;
- прямое получение готового destination, когда constructor-plan отсутствует;
- выбор existing destination как `result`, когда previous существует.

Body-members в `Create` не настраиваются. В частности, свойства и поля,
включая `init` и `required`, принадлежат только `Members`.

### 6.2. Выбор generated surface

После применения общей destination-type policy форма `Create` определяется
только наличием constructor surface, который Morphant действительно умеет
вызвать:

| Constructor capability | Generated `Create` | Что возвращает lambda |
|---|---|---|
| Есть хотя бы один поддерживаемый constructor | Structured | `DestinationCreation` |
| Поддерживаемого constructor surface нет | Direct | Настоящий `TDestination` |

Structured `Create` нужен для выбора destination-конструктора и настройки его
параметров. Если такого конструктора нет, моделировать промежуточный
constructor-plan бессмысленно: direct `Create` сразу получает готовый instance.

Наличие body-members не влияет на выбор формы `Create`. Оно независимо
определяет наличие `Members`. Поэтому interface или factory-only class с
writable members получает direct `Create` вместе с `Members`, а scalar без
members — только direct `Create`.

Одна mapping-пара никогда не получает обе формы. Пользовательский mode для
переключения между structured и direct surface не вводится.

### 6.3. Две перегрузки и общая семантика arity

Для structured surface генерируются:

```csharp
Create(
    Func<TSource, DestinationCreation> create);

Create(
    Func<TSource, Previous<TDestination>, DestinationCreation> create);
```

`DestinationCreation` — сгенерированный creation-plan для конкретного
destination. Это не настоящий `TDestination`.

Для direct surface генерируются:

```csharp
Create(
    Func<TSource, TDestination> create);

Create(
    Func<TSource, Previous<TDestination>, TDestination> create);
```

Обе формы используют один закон выбора result:

| Настройка | Previous отсутствует | Previous существует |
|---|---|---|
| `Create(source)` | Lambda определяет result | Lambda не вызывается; previous становится result |
| `Create(source, previous)` | Lambda вызывается с `Previous.None` | Lambda вызывается с `Previous.Some` |

Source-only structured `Create` концептуально эквивалентен:

```csharp
Create((source, previous) =>
    previous.HasValue
        ? previous
        : CreateFromSource(source));
```

Source-only direct `Create` имеет ту же семантику, но возвращает настоящий
destination:

```csharp
Create((source, previous) =>
    previous.HasValue
        ? previous.Value
        : CreateFromSource(source));
```

Эта небольшая синтаксическая асимметрия намеренна. Structured lambda выбирает
ветку creation-plan, поэтому `Previous<TDestination>` неявно преобразуется в
`DestinationCreation`. Direct lambda уже обязана вернуть `TDestination`, поэтому
после проверки `HasValue` явно извлекается `previous.Value`. Отдельный
`DirectCreation<T>`, implicit conversion `Previous<T> -> T`, `AsResult()` и
`UsePrevious()` не вводятся.

Настоящий return type direct source-only перегрузки также сохраняет естественные
method groups:

```csharp
builder.Map<string, Guid>()
    .Create(Guid.Parse);
```

Для одной пары можно настроить только один `Create`, независимо от выбранной
перегрузки. Повторный вызов является diagnostic; две перегрузки не образуют
отдельные `MapNew`- и `MapExisting`-правила.

### 6.4. Почему две перегрузки нужны только здесь

У `Create` arity действительно меняет политику:

```csharp
.Create(source => new(source.Id))
```

не заменяет existing destination, а:

```csharp
.Create((source, _) => new(source.Id))
```

создаёт result и при `MapNew`, и при `MapExisting`.

Это намеренное различие, а не сокращённая запись одной и той же операции.

Тот же закон действует для direct surface:

```csharp
.Create(Parse)
```

сохраняет existing destination, а:

```csharp
.Create((source, _) => Parse(source))
```

получает replacement и для `MapNew`, и для `MapExisting`.

### 6.5. Generated structured creation-plan

Creation-plan зеркалит поддерживаемые destination-конструкторы и использует
`ConstructorMember<T>` для их параметров. Концептуально:

```csharp
internal sealed class DestinationCreation
{
    public DestinationCreation(
        ConstructorMember<Guid> id,
        ConstructorMember<Guid> tenantId);

    public DestinationCreation(
        ByConventionMarker marker,
        DestinationConstructorMembers? members = null);

    public DestinationCreation(
        IByFactoryMarker<Destination> marker);

    public static implicit operator DestinationCreation(
        Previous<Destination> previous);
}
```

Это сохраняет полноценный DSL для constructor parameters:

```csharp
.Create(source => new(
    source.Id,
    Auto(),
    Map()))
```

Поддерживаемые формы creation-plan:

- явный destination-конструктор;
- `ByConvention()`;
- `ByConvention()` с явными constructor-member rules;
- factory через `new(ByFactory(...))`;
- existing previous как result в previous-aware перегрузке.

Произвольный готовый `TDestination` не преобразуется в structured
creation-plan. Готовый или cached instance выражается явно как factory-ветка:

```csharp
.Create(source => new(ByFactory(() => cache.Get(source.Id))))
```

Форма `new(ByFactory(...))` обязательна: marker передаётся generated
constructor-у creation-plan, а implicit conversion от marker-interface не
генерируется.

Constructor-member rules сохраняют текущую модель:

| Запись | Семантика |
|---|---|
| Явное выражение | Вычислить и передать значение параметра |
| `Auto()` | Обязательно получить параметр по convention |
| `Ignore()` | Опустить параметр, когда это допустимо для optional / `params` |
| `Map()` | Выполнить nested mapping и передать его результат |

`Create` не гарантирует новую identity. В частности, `ByFactory()` может
вернуть cached instance. Название означает получение базового `result`, а не
обязательное выделение нового объекта.

### 6.6. Поведение по умолчанию

Для structured surface, если previous отсутствует и `Create` не настроен,
Morphant выполняет обычное convention construction с эффективным
`ConstructorSelection`. Текущим default остаётся `Unambiguous`.

У direct surface нет default creation: отсутствие поддерживаемого constructor
как раз означает, что Morphant не может самостоятельно получить instance. Если
доступная операция может прийти в no-previous ветку, direct `Create` должен быть
настроен. Отсутствие настройки является ошибочной конфигурацией, а не поводом
для скрытого fallback на `MapManually` или runtime conversion.

Если previous существует и configured `Create` — source-only, lambda не
вычисляется вообще. Constructor arguments, factory и любые используемые только
в этой lambda выражения также не вычисляются.

Если previous-aware structured `Create` выбирает previous, он становится
`result`. Constructor, convention или factory дают replacement-result. В
direct surface lambda возвращает либо `previous.Value`, либо готовый
replacement непосредственно.

Никакого скрытого fallback между различными ветками `Create` нет.

## 7. `Members`

### 7.1. Единственная перегрузка

Концептуальная сигнатура:

```csharp
Members(
    Func<TSource, Previous<TDestination>, DestinationMembers> members);
```

Source-only перегрузки нет. Если previous не нужен, пользователь пишет `_`:

```csharp
.Members((source, _) => new()
{
    Name = source.Name,
    Age = source.Age
});
```

Это явно показывает, что member plan применяется в обеих операциях, а
previous может существовать, хотя данному правилу он не нужен.

### 7.2. Ответственность

`Members` является единственным местом настройки всех поддерживаемых
body-members destination:

- properties с обычным `set`;
- `init`-only properties;
- `required` properties и fields;
- writable fields;
- поддерживаемые унаследованные body-members.

Constructor parameters не входят в `Members`, потому что они не являются
body-members.

Концептуальный generated plan:

```csharp
internal sealed class DestinationMembers
{
    public Member<string> Name { get; init; }

    public Member<string> Code { get; init; }

    public Member<int> Revision { get; init; }
}
```

Собственные `init`-сеттеры служебного типа нужны только для составления плана и
не связаны с `init`-семантикой destination.

Обычные `set`-сеттеры для `DestinationMembers` намеренно не генерируются. Они
были бы полезны только для императивной сборки и последующей мутации одного
member-plan, например через локальную переменную. Это добавило бы отдельную
семантику порядка повторных присваиваний, aliasing и изменения плана после его
создания. В согласованной модели каждая поддерживаемая ветка lambda возвращает
целиком сформированный plan, поэтому `init` достаточно и точнее выражает его
назначение. Если императивная сборка плана окажется нужна, её следует отдельно
согласовать вместе с поддерживаемыми control-flow constructs.

### 7.3. Применение плана

`Members` всегда применяется к выбранному `result`, а не к `previous`.
Параметр `previous` внутри lambda всегда означает исходный destination-вход
после null-предобработки, даже если `Create` выбрал replacement.

Пример:

```csharp
builder.Map<CustomerDto, Customer>()
    .Create((source, previous) =>
        previous.HasValue &&
        previous.Value.TenantId == source.TenantId &&
        !previous.Value.IsFrozen
            ? previous
            : new(source.Id, source.TenantId))
    .Members((source, previous) => new()
    {
        Name = source.Name,

        Revision = previous.HasValue
            ? previous.Value.Revision + 1
            : 1
    });
```

Если `Create` вернул replacement, `Name` и `Revision` применяются к
replacement, но `previous.Value.Revision` читается из исходного объекта.

Generator самостоятельно раскладывает единый member plan по допустимым фазам:

- для result, создаваемого structured constructor/convention plan, `init` и
  creation-time `required` попадают в object initializer;
- обычные setters и writable fields применяются к выбранному result;
- если result является previous, его `init`-only members сохраняются;
- выражение explicit `init`-rule не вычисляется в ветке, где применить его
  невозможно;
- `required`-member с обычным доступным `set` можно обновлять у previous;
- replacement, созданный constructor/convention plan, получает те же
  creation-time member rules, что и обычный `MapNew`.

Если `ByFactory()` или direct `Create` возвращает уже созданный объект,
применить к нему `init`-only rule невозможно. Явная попытка совместить такую
creation-ветку с соответствующим `Members` rule должна давать diagnostic.
Factory или direct lambda должна инициализировать такой member сама либо
mapping должен быть ручным.

### 7.4. Explicit rules и conventions

Внутри `Members` остаются только member-level операции:

```csharp
.Members((source, _) => new()
{
    Name = source.Name,
    Age = Auto(),
    LegacyValue = Ignore(),
    Address = Map()
});
```

Их семантика:

| Запись | Результат |
|---|---|
| Явное выражение | Вычислить и присвоить member выбранного result |
| `Auto()` | Обязательно найти convention mapping |
| `Ignore()` | Не маппить member и сохранить значение выбранного result |
| `Map()` | Выполнить nested mapping и присвоить возвращённый результат |
| Member не указан | Применить эффективный `MemberMatching` |

При `MemberMatching.Auto` явные rules дополняют или переопределяют convention
rules. При `MemberMatching.Explicit` неуказанные members не маппятся.

`Ignore()` для нового result оставляет значение, полученное конструктором,
factory или default initialization. Для previous он сохраняет текущее значение
выбранного result.

Nested `Map()` всегда берёт previous дочернего mapping из соответствующего
member внешнего `previous`, а выбранный `result` определяет только место, куда
будет записано возвращённое значение. Концептуально генерируется:

```csharp
var address = previous.HasValue
    ? context.Mapper.Map<AddressSource, Address>(
        source.Address,
        previous.Value.Address,
        context)
    : context.Mapper.Map<AddressSource, Address>(
        source.Address,
        context);

result.Address = address;
```

Если внешний `previous` отсутствует, выполняется обычный nested `MapNew`. Если
он существует, вызывается nested `MapExisting` с child из
`previous.Value`, даже когда previous-aware `Create` выбрал replacement.
Replacement задаёт новый внешний result, но не подменяет историю mapping-а
своими текущими member values. Возвращённый nested result затем присваивается
member-у replacement.

Если child внешнего `previous` равен `null`, должен вызываться именно nested
`Map(source, null)`, а не `Map(source)`. Благодаря этому вложенный mapping
сохраняет различие двух публичных операций и применяет свой обычный
`NullDestinationHandling`.

Nested `MapExisting` может изменить child старого object graph и вернуть тот же
экземпляр. Это является обычной семантикой соответствующей вложенной mapping-
пары. Если нужно всегда создавать новый child, это настраивается в ней; если
нужно сохранить значение member-а выбранного replacement, пользователь
указывает `Ignore()` или явное выражение вместо `Map()`.

То же правило применяется к creation-time `init` member. Nested mapping можно
вычислить из внешнего `previous` до создания result и поместить возвращённое
значение непосредственно в object initializer; читать member ещё не созданного
replacement для этого не требуется.

### 7.5. Почему `Skip()` не нужен

Полный статический отказ от implicit member mapping уже выражается настройкой:

```csharp
builder.Map<Source, Destination>()
    .MemberMatching(MemberMatching.Explicit);
```

Если `Members` отсутствует, ни один body-member не маппится. При существующем
previous и отсутствии previous-aware `Create` он останется result без
изменений.

Для динамического алгоритма, который в runtime иногда должен выполнить полный
no-op, используется `MapManually`. Отдельный `Skip()` не добавляется.

## 8. Полностью ручной mapping

### 8.1. `MappingContext` и единственная перегрузка

Тип текущей mapping-операции является частью общего `MappingContext`, а не
destination-specific previous-объекта:

```csharp
public enum MappingOperation
{
    MapNew = 0,
    MapExisting
}

public abstract class MappingContext
{
    public MappingOperation Operation { get; }

    public IContextualMapper Mapper { get; }
}
```

`MappingOperation` описывает ровно одну выполняемую операцию и поэтому не
переиспользует flags-enum `MappingMode`. `Operation` доступен пользователю
только для чтения; его значение устанавливает mapper.

`MapManually` находится на обычном pair-builder и имеет одну универсальную
перегрузку:

```csharp
MapManually(
    Func<
        TSource?,
        Previous<TDestination>,
        MappingContext,
        TDestination> mapping);
```

`TSource?` здесь означает исходное runtime-значение source, включая `null`,
когда `TSource` допускает его. Точный generated nullable-контракт должен
сохранять фактическую nullability mapping-пары.

Source-only перегрузки нет. Если сведения о вызове и mapping context не нужны,
пользователь намеренно игнорирует оба дополнительных параметра:

```csharp
.MapManually((source, _, _) =>
    new Destination(source!.Id, source.Name));
```

`Previous<TDestination>` и `MappingContext` передаются раздельно, поскольку
отвечают на разные вопросы. `Previous` описывает наличие фактического
destination instance, а `MappingContext` — весь mapping-вызов, включая его
операцию и contextual mapper для ручных nested mappings.
`MappingContext` является последним параметром, как и в generated
`ITypeMapper.Map(...)` contract.

### 8.2. Почему одного `Previous<T>` недостаточно

В manual mapping не выполняются `NullSourceHandling` и
`NullDestinationHandling`. Поэтому пользователь должен различать:

- `Map(source)`;
- `Map(source, null)`;
- `Map(source, destination)`.

Два первых вызова не имеют экземпляра destination, но являются разными
операциями. Форма вызова хранится в `MappingContext.Operation`, а наличие
экземпляра — независимо от неё в `Previous<TDestination>`.

Точные состояния:

| Вызов | `context.Operation` | `previous` |
|---|---|---|
| `Map(source)` | `MapNew` | `None` |
| `Map(source, null)` | `MapExisting` | `None` |
| `Map(source, destination)` | `MapExisting` | `Some(destination)` |

`Operation` и `Previous` хранят два независимых факта: какая публичная
операция вызвана и существует ли фактический destination instance. Поэтому
для различения explicit `null` не требуется отдельная generic call-обёртка.

### 8.3. Семантика

```csharp
builder.Map<Source, Destination>()
    .MapManually((source, previous, context) =>
    {
        if (source is null)
            return HandleNullSource(previous, context);

        if (context.Operation == MappingOperation.MapNew)
            return Create(source);

        if (!previous.TryGetValue(out var destination))
            return HandleExplicitNullDestination(source);

        Update(destination, source, context);
        return destination;
    });
```

В lambda передаётся тот же `MappingContext`, который получил текущий
generated mapper. Поэтому nested mapping вручную вызывается через уже
существующий contextual contract с обязательной передачей этого же context:

```csharp
var address = previous.TryGetValue(out var destination)
    ? context.Mapper.Map<AddressDto, Address>(
        source.Address,
        destination.Address,
        context)
    : context.Mapper.Map<AddressDto, Address>(
        source.Address,
        context);
```

Создавать новый context или начинать отдельную mapping chain для такого вызова
не нужно.

`Operation` всегда описывает текущий вызов в mapping chain, а не корневую
операцию. При входе во вложенный `Map(...)` contextual mapper устанавливает
операцию вложенного overload-а, а при выходе восстанавливает предыдущее
значение. Поэтому внутри nested `MapManually` видна его собственная операция,
а продолжившийся после него внешний manual mapping снова видит свою.

`MapManually` полностью определяет результат во всех включённых
`MappingMode`-операциях. Внутри разрешён обычный C#:

- expression- и block-lambdas;
- условия, switch, циклы и несколько `return`;
- mutation;
- constructors и factories;
- record `with`;
- вызовы других методов и mapper-ов.

При выполнении `MapManually`:

- `NullSourceHandling` не применяется;
- `NullDestinationHandling` не применяется;
- convention construction не применяется;
- convention member mapping не применяется;
- `Create` и `Members` не выполняются;
- `Auto()`, `Ignore()`, `Map()`, `ByConvention()` и `ByFactory()` не являются
  DSL-маркерами и недоступны;
- ручные nested mappings доступны через `context.Mapper.Map(...)`;
- во вложенный вызов передаётся полученный lambda экземпляр `MappingContext`;
- lambda возвращает настоящий `TDestination`;
- `MappingMode` по-прежнему определяет, какую публичную операцию можно вызвать.

Для одной пары разрешён ровно один `MapManually`. Его смешивание с `Create`,
`Members` или declarative constructor/member-specific configuration является
ошибкой конфигурации и должно диагностироваться. Унаследованные общие settings,
не имеющие эффекта в manual mapping, не запускают скрытый declarative pipeline.

### 8.4. Использование context за пределами `MapManually`

`MappingContext` участвует не только в manual mapping. Declarative pipeline
использует его внутренне для каждого automatic nested `Map()` и передаёт тот
же экземпляр по всей mapping chain. `Operation` при этом поддерживается для
каждого текущего outer или nested вызова по правилу выше.

Однако пользовательским параметром `MappingContext` пока остаётся только в
`MapManually`. Добавлять его в `Create` или `Members` не нужно:

- declarative lambdas намеренно получают уже нормализованные source и
  previous после null handling;
- доступ к `context.Operation` позволил бы снова различать `Map(source)` и
  нормализованный `Map(source, null)`, обходя эту модель;
- declarative nested mapping уже выражается специальным `Map()` marker.

Если в будущем в context появятся конкретные пользовательские данные или
новые extension points, необходимость доступа к ним из declarative DSL должна
быть согласована отдельно. Гипотетическая польза не является основанием
усложнять текущие сигнатуры `Create` и `Members`.

## 9. Null handling

### 9.1. Declarative mapping

Для `Create` и `Members` null handling выполняется до mapping DSL.

Порядок остаётся таким:

1. Проверить source и применить эффективный `NullSourceHandling`.
2. Для `MapExisting` проверить destination и применить эффективный
   `NullDestinationHandling`.
3. Сформировать нормализованный `Previous<TDestination>`.
4. Выбрать `result` через configured/default `Create` policy.
5. Применить `Members` и effective member conventions.

Когда declarative lambda начинает выполняться, source уже прошёл
`NullSourceHandling`.

Для reference destination целевая семантика `NullDestinationHandling`:

| Настройка | Поведение |
|---|---|
| `Throw` | Бросить исключение до `Create` и `Members` |
| `TreatAsMissing` | Считать explicit `null` отсутствующим previous и перейти в no-previous ветку |

`TreatAsMissing` — более точное целевое имя для текущего `CreateNew`: configured
`Create` может использовать factory или cache и не обязан возвращать новый
instance.

После `TreatAsMissing` следующие вызовы намеренно неразличимы внутри
declarative DSL:

```csharp
Map(source)
Map(source, null)
```

В обоих случаях `Create` / `Members` получают `Previous.None`. Именно поэтому
для `Members` достаточно `Previous<TDestination>` без доступа к
`MappingContext.Operation`.

`NullSourceHandling` сохраняет текущие варианты и precedence. В частности,
если effective policy возвращает результат или бросает исключение, ни
`Create`, ни `Members` не выполняются.

### 9.2. Manual mapping

Для `MapManually` обе null-handling настройки полностью обходятся. В lambda
передаются исходный source, фактический previous и `MappingContext`, чей
`Operation` сохраняет исходную форму вызова.

Это не fallback и не специальный mode настройки. Полная обработка `null`
является частью ручного алгоритма пользователя.

## 10. Точный declarative алгоритм

Концептуально `Map(source)` работает так:

```csharp
ApplyNullSourceHandling(source);

var previous = Previous<Destination>.None;

var result = RunNoPreviousCreate(source, previous);

ApplyMembers(source, previous, result);

return result;
```

`RunNoPreviousCreate` вызывает любую configured `Create`-перегрузку, поскольку
previous отсутствует. Если `Create` не настроен, structured surface выполняет
convention construction, а direct surface не имеет default creation и является
ошибочной конфигурацией для такой reachable ветки.

`Map(source, destination)` после null-предобработки работает так:

```csharp
ApplyNullSourceHandling(source);
var previous = ApplyNullDestinationHandling(destination);

Destination result;

if (!previous.HasValue)
{
    result = RunNoPreviousCreate(source, previous);
}
else if (previousAwareCreateConfigured)
{
    result = RunCreate(source, previous);
}
else
{
    result = previous.Value;
}

ApplyMembers(source, previous, result);

return result;
```

`RunCreate` никогда не подменяется другой configured lambda. Structured plan
lowering и direct lambda в итоге дают один настоящий `Destination result`. Для
пары существует не более одного `Create`.

Если `Members` не настроен, `ApplyMembers` применяет только effective
`MemberMatching` conventions. Если generated member surface отсутствует, эта
стадия не содержит применимых members.

## 11. Условия генерации pair API

API должен отражать реальные возможности destination и не показывать
бесполезные методы.

После применения общей destination-type policy действуют независимые правила:

| Возможности destination | Доступный API |
|---|---|
| Для любой поддерживаемой mapping-пары | Обе runtime `Map`-операции в contract, ровно одна форма `Create` и `MapManually` на pair-builder |
| Есть хотя бы один поддерживаемый доступный constructor | Structured `Create`, возвращающий generated `DestinationCreation` |
| Поддерживаемого constructor surface нет | Direct `Create`, возвращающий настоящий `TDestination` |
| Есть хотя бы один поддерживаемый body-member | Generated `Members` независимо от формы `Create` |

Отсутствие members и constructors не убирает declarative surface. Такая пара
получает direct `Create`; `MapExisting` всё равно может вернуть previous без
изменений, а `MapNew` требует configured direct lambda. Единственным общим gate
для публичной операции остаётся эффективный `MappingMode`.

Под «есть member» понимается member, реально включаемый в generated
`DestinationMembers`, а не любой symbol типа. Static members, indexers,
get-only properties, readonly fields и другие неподдерживаемые формы не
считаются.

Под «есть constructor» понимается instance-constructor, который generator
может использовать для создания данного destination. Недоступные и
неподдерживаемые constructors не считаются. Constructor abstract-типа сам по
себе не делает тип создаваемым. Built-in, enum и отдельно определённые общей
type policy scalar-категории получают direct surface, даже если metadata типа
технически содержит public constructors: Morphant намеренно не моделирует их
как structural constructor DSL.

Direct `Create` семантически соответствует structured-ветке
`new(ByFactory(...))`: он получает уже созданный instance, после чего Morphant
применяет обычные setter-rules и member conventions. Direct result не является
окончательным результатом в смысле `MapManually`.

Например, interface не имеет constructor surface, но может независимо иметь
writable body-members:

```csharp
builder.Map<Source, IDestination>()
    .Create(source => factory.Create(source.Id))
    .Members((source, _) => new()
    {
        Name = source.Name
    });
```

Здесь direct lambda получает экземпляр, а declarative member plan продолжает
иметь самостоятельную ценность. Для `init` и creation-time `required` действуют
те же ограничения, что для structured factory-ветки: их должен заполнить код,
который вернул уже созданный instance.

Отдельного служебного creation type для scalar, opaque value object,
factory-only class, interface или abstract destination не создаётся. Их direct
surface сохраняет standard null handling и declarative member stage, поэтому
`MapManually` нужен только для действительно ручного алгоритма.

## 12. Основные сценарии

### 12.1. Полностью convention mapping

```csharp
builder.Map<Source, Destination>();
```

Поведение:

- `Map(source)` создаёт destination по convention;
- `Map(source, destination)` использует destination как result;
- body-members маппятся по effective conventions.

### 12.2. Явный constructor и единый member plan

```csharp
builder.Map<UserDto, User>()
    .Create(source => new(
        id: source.Id,
        tenantId: Auto()))
    .Members((source, _) => new()
    {
        Name = source.Name,
        Email = source.Email,
        RequiredCode = source.Code
    });
```

В `MapNew` выполняются `Create` и `Members`. В обычном `MapExisting` source-only
`Create` не выполняется, previous становится result, а применимые member rules
обновляют его. `RequiredCode` настраивается только в `Members`, независимо от
того, является ли он `set`- или `init`-member destination.

### 12.3. Условное переиспользование или replacement

```csharp
builder.Map<CustomerDto, Customer>()
    .Create((source, previous) =>
        previous.HasValue &&
        previous.Value.TenantId == source.TenantId &&
        !previous.Value.IsFrozen
            ? previous
            : new(
                source.Id,
                source.TenantId))
    .Members((source, previous) => new()
    {
        Name = source.Name,
        Revision = previous.HasValue
            ? previous.Value.Revision + 1
            : 1
    });
```

Previous-aware `Create` является полным выбором result для обоих публичных
вызовов. `Members` применяется уже к выбранному result.

### 12.4. Всегда создавать replacement

```csharp
builder.Map<Source, Destination>()
    .Create((source, _) => new(source.Id))
    .Members((source, _) => new()
    {
        Name = source.Name
    });
```

Двухпараметрический `Create` намеренно игнорирует previous и получает result в
обеих операциях.

### 12.5. Factory плюс members

```csharp
builder.Map<OrderDto, Order>()
    .Create(source =>
        new(ByFactory(() => orderFactory.Create(source.Id))))
    .Members((source, _) => new()
    {
        Number = source.Number
    });
```

Factory выполняется только в no-previous ветке source-only `Create`. При
обычном `MapExisting` используется previous и применяется `Number`.

### 12.6. Direct factory-only destination плюс members

```csharp
builder.Map<OrderDto, IOrder>()
    .Create((source, previous) =>
        previous.HasValue && CanReuse(previous.Value, source)
            ? previous.Value
            : orderFactory.Create(source.Id))
    .Members((source, _) => new()
    {
        Number = source.Number
    });
```

У interface нет constructor surface, поэтому `Create` возвращает настоящий
`IOrder`. Возврат `previous.Value` сохраняет existing instance; factory даёт
replacement. В обеих ветках применимый member plan выполняется после выбора
result.

### 12.7. Scalar и opaque value object

```csharp
builder.Map<Order, decimal>()
    .Create(source =>
        source.Items.Sum(x => x.Price * x.Count));

builder.Map<string, OrderNumber>()
    .Create(OrderNumber.Parse);
```

Для destination без structural constructor surface direct `Create` сохраняет
обычный declarative pipeline без искусственного creation-plan и без перехода к
`MapManually`.

### 12.8. Immutable или сложный ручной mapping

```csharp
builder.Map<SnapshotDto, Snapshot>()
    .MapManually((source, previous, _) =>
    {
        if (source is null)
            return default!;

        if (previous.TryGetValue(out var destination) &&
            destination.Version == source.Version)
        {
            return destination with
            {
                Name = source.Name
            };
        }

        return new Snapshot(
            source.Id,
            source.Name,
            source.Version);
    });
```

Никакого generated `with`-DSL для этого не требуется.

## 13. Ошибочные и конфликтующие конфигурации

В целевом дизайне diagnostics должны покрыть как минимум:

- повторный `Create` для одной pair, включая вызовы разных перегрузок;
- повторный `Members`;
- повторный `MapManually`;
- смешивание `MapManually` с `Create` или `Members`;
- pair-specific constructor/member settings, несовместимые с manual mapping;
- factory или direct creation вместе с explicit `init`-rule, который невозможно
  применить к уже созданному result;
- reachable no-previous branch direct surface без configured `Create`;
- невозможный explicit constructor/member marker;
- duplicate registration той же canonical pair.

Diagnostics остаются отдельной реализационной фазой, но отсутствие готового
diagnostic не должно вводить скрытый fallback на другой mapping algorithm.

## 14. Зафиксированные законы дизайна

1. `Map(source)` и `Map(source, destination)` остаются двумя публичными
   mapping-операциями; effective `MappingMode` управляет их доступностью.
2. Declarative `Create` и `Members` выполняются только после null handling.
3. Source-only `Create` выполняется только при отсутствии previous.
4. Previous-aware `Create` выполняется и с `Previous.None`, и с
   `Previous.Some`.
5. Если `Create` отсутствует, structured surface создаёт no-previous result по
   convention, direct surface не имеет default creation, а существующий previous
   в обеих формах сам становится result.
6. Для одной pair разрешён не более чем один `Create` любой перегрузки.
7. `Create` настраивает result selection и constructor parameters, но никогда
   не body-members.
8. `Members` является единственным declarative API для всех body-members,
   включая `init` и `required`.
9. У `Members` есть только одна universal previous-aware перегрузка.
10. `Members` применяется к выбранному result.
11. `previous` в `Members` всегда означает исходный нормализованный input, а не
    выбранный result.
12. Неприменимое `init`-выражение в existing-result ветке не вычисляется.
13. Member, не указанный в `Members`, следует effective `MemberMatching`.
14. `MemberMatching.Explicit` является статическим способом полностью
    отключить implicit member mapping; отдельного `Skip()` нет.
15. Nested `Map()` использует соответствующий member внешнего `previous` как
    child destination; при отсутствии outer previous выполняется nested
    `MapNew`, а возвращённое значение присваивается member-у выбранного result.
16. `MapManually` является методом обычного pair-builder, а не отдельным
    builder-типом.
17. У `MapManually` есть только одна перегрузка с
    `Previous<TDestination>` и `MappingContext`.
18. `MapManually` полностью заменяет declarative pipeline и не запускает
    null-handling settings.
19. `MappingContext.Operation` сообщает текущую публичную операцию, а
    `Previous<TDestination>` независимо сообщает наличие фактического
    destination instance.
20. `MapManually` и ровно одна форма `Create` доступны для каждой поддерживаемой
    mapping-пары; `Members` генерируется независимо при наличии поддерживаемых
    body-members.
21. Наличие поддерживаемого constructor surface выбирает structured `Create`,
    его отсутствие — direct `Create`; пользовательского mode и пары с обеими
    формами нет.
22. Direct `Create` семантически соответствует уже созданному factory-result:
    после него выполняются применимые `Members` и member conventions.
23. Возвращённый `Map` result всегда авторитетен.
24. Никаких скрытых fallback между manual и declarative mapping либо между
    разными configured lambdas нет.
25. В structured surface `Previous<TDestination>` неявно преобразуется в
    `DestinationCreation`, поэтому возврат самого `previous` выбирает existing
    result. Direct surface возвращает настоящий `TDestination` и использует
    `previous.Value`; отдельный direct plan или implicit unwrap не вводится.
    Произвольный готовый `TDestination` в structured surface выражается только
    явной factory-веткой.
26. Для `ByConventionMarker` генерируется один creation-plan constructor с
    необязательным `DestinationConstructorMembers`.
27. Generated properties `DestinationMembers` имеют только `init`; мутация уже
    созданного member-plan не входит в declarative DSL.
28. `MapManually` получает текущий `MappingContext` отдельным последним
    параметром и использует его для ручных nested mappings.
29. `MappingContext.Operation` описывает текущий outer или nested вызов;
    contextual mapper восстанавливает операцию вызывающего mapping после
    возврата из вложенного.
30. Declarative pipeline использует `MappingContext` внутренне, но `Create` и
    `Members` не получают его пользовательским lambda-параметром.

## 15. Детали, которые ещё нужно закрепить перед реализацией

Фундаментальная семантика выше согласована. Отдельного решения при
проектировании generated surface требуют:

- окончательное имя generated creation- и member-plan типов;
- точная nullable-аннотация `Previous<T>` и manual source;
- граница поддерживаемых control-flow constructs внутри declarative `Create`
  и `Members` lambdas;
- порядок миграции текущего `Template()` implementation и тестов;
- обновление `IMPLEMENTATION_PLAN.md`, XML-документации и user-facing docs;
- diagnostic IDs, сообщения и точная фаза их добавления.

До отдельного согласования эти детали не должны молча определяться удобством
текущей реализации.
